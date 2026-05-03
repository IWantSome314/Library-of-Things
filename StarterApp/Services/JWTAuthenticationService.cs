using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Storage;
using StarterApp.Database.Models;

namespace StarterApp.Services;

// File purpose:
// Implements app authentication using JWT access/refresh tokens.
// Core responsibilities: login/register, token persistence, auto-refresh, and exposing current user/roles.
public class JWTAuthenticationService : IAuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private User? _currentUser;
    private List<string> _currentUserRoles = new();
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _tokenExpirationUtc;
    private bool _isInitialized;

    private const string AccessTokenStorageKey = "auth_access_token";
    private const string RefreshTokenStorageKey = "auth_refresh_token";
    private const string TokenExpiryStorageKey = "auth_access_token_expiry_utc";

    public event EventHandler<bool>? AuthenticationStateChanged;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken) && _tokenExpirationUtc > DateTime.UtcNow;
    public User? CurrentUser => _currentUser;
    public List<string> CurrentUserRoles => _currentUserRoles;

    public JWTAuthenticationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        await InitializeAsync();

        var endpoint = BuildEndpoint("/auth/token");

        try
        {
            var payload = new { email, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/auth/token", content);

            if (!response.IsSuccessStatusCode)
            {
                return new AuthenticationResult(false, "Invalid email or password");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody, _jsonOptions);

            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken) || string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            {
                return new AuthenticationResult(false, "Invalid token payload received");
            }

            // Persist tokens first, then derive user/roles from JWT claims for app state.
            await SetTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken, tokenResponse.ExpiresAtUtc);
            HydrateUserFromJwt(_accessToken!);

            AuthenticationStateChanged?.Invoke(this, true);
            return new AuthenticationResult(true, "Login successful");
        }
        catch (HttpRequestException)
        {
            return new AuthenticationResult(false, $"Login failed: cannot reach auth server at {endpoint}");
        }
        catch (Exception ex)
        {
            return new AuthenticationResult(false, $"Login failed: {ex.Message}");
        }
    }

    public async Task<AuthenticationResult> RegisterAsync(string firstName, string lastName, string email, string password)
    {
        await InitializeAsync();

        var endpoint = BuildEndpoint("/auth/register");

        try
        {
            var payload = new { firstName, lastName, email, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/auth/register", content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return new AuthenticationResult(false, string.IsNullOrWhiteSpace(body) ? "Registration failed" : body);
            }

            return new AuthenticationResult(true, "Registration successful");
        }
        catch (HttpRequestException)
        {
            return new AuthenticationResult(false, $"Registration failed: cannot reach auth server at {endpoint}");
        }
        catch (Exception ex)
        {
            return new AuthenticationResult(false, $"Registration failed: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        _tokenExpirationUtc = DateTime.MinValue;
        _currentUser = null;
        _currentUserRoles.Clear();
        SecureStorage.Default.Remove(AccessTokenStorageKey);
        SecureStorage.Default.Remove(RefreshTokenStorageKey);
        SecureStorage.Default.Remove(TokenExpiryStorageKey);

        AuthenticationStateChanged?.Invoke(this, false);
        await Task.CompletedTask;
    }

    public bool HasRole(string roleName) => _currentUserRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    public bool HasAnyRole(params string[] roleNames) => roleNames.Any(HasRole);

    public bool HasAllRoles(params string[] roleNames) => roleNames.All(HasRole);

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        await InitializeAsync();
        var valid = await EnsureValidTokenAsync();
        if (!valid)
        {
            return false;
        }

        try
        {
            var payload = new { currentPassword, newPassword };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/change-password")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                return response.IsSuccessStatusCode;
            }

            var refreshed = await RefreshAccessTokenAsync();
            if (!refreshed)
            {
                return false;
            }

            using var retry = new HttpRequestMessage(HttpMethod.Post, "/auth/change-password")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var retryResponse = await _httpClient.SendAsync(retry);
            return retryResponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        // Load persisted auth state once on app startup/resume.
        _accessToken = await SecureStorage.Default.GetAsync(AccessTokenStorageKey);
        _refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenStorageKey);
        var expiry = await SecureStorage.Default.GetAsync(TokenExpiryStorageKey);

        if (DateTime.TryParse(expiry, out var parsedExpiry))
        {
            _tokenExpirationUtc = DateTime.SpecifyKind(parsedExpiry, DateTimeKind.Utc);
        }
        else
        {
            _tokenExpirationUtc = DateTime.MinValue;
        }

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            return;
        }

        if (_tokenExpirationUtc <= DateTime.UtcNow)
        {
            // Expired token path: attempt silent refresh to avoid forcing immediate re-login.
            var refreshed = await RefreshAccessTokenAsync();
            if (!refreshed)
            {
                await LogoutAsync();
            }
            return;
        }

        HydrateUserFromJwt(_accessToken);
        AuthenticationStateChanged?.Invoke(this, true);
    }

    /// <summary>
    /// Exposes the active Access Token for the AuthenticationInterceptor.
    /// Ensures the token is valid (automatically refreshing if necessary) before returning it.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        await InitializeAsync();
        
        var isValid = await EnsureValidTokenAsync();
        return isValid ? _accessToken : null;
    }

    public async Task<bool> EnsureValidTokenAsync()
    {
        await InitializeAsync();

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            return false;
        }

        if (_tokenExpirationUtc > DateTime.UtcNow.AddSeconds(60))
        {
            return true;
        }

        return await RefreshAccessTokenAsync();
    }

    public async Task<bool> ForceRefreshTokenAsync()
    {
        await InitializeAsync();
        return await RefreshAccessTokenAsync(forceRefresh: true);
    }

    private async Task<bool> RefreshAccessTokenAsync(bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
        {
            return false;
        }

        // A single lock prevents multiple parallel requests from racing token refresh calls.
        await _refreshLock.WaitAsync();
        try
        {
            if (!forceRefresh && _tokenExpirationUtc > DateTime.UtcNow.AddSeconds(60) && !string.IsNullOrWhiteSpace(_accessToken))
            {
                return true;
            }

            var payload = new { refreshToken = _refreshToken };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/auth/refresh", content);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody, _jsonOptions);

            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken) || string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            {
                return false;
            }

            await SetTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken, tokenResponse.ExpiresAtUtc);
            HydrateUserFromJwt(_accessToken!);
            AuthenticationStateChanged?.Invoke(this, true);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task SetTokensAsync(string accessToken, string refreshToken, DateTime expiresAtUtc)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _tokenExpirationUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);

        await SecureStorage.Default.SetAsync(AccessTokenStorageKey, _accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenStorageKey, _refreshToken);
        await SecureStorage.Default.SetAsync(TokenExpiryStorageKey, _tokenExpirationUtc.ToString("O"));
    }

    private void HydrateUserFromJwt(string accessToken)
    {
        // JWT claims are treated as source-of-truth for current identity and role checks in the UI.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        _currentUserRoles = jwt.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var firstName = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName || c.Type == "given_name")?.Value ?? string.Empty;
        var lastName = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.FamilyName || c.Type == "family_name")?.Value ?? string.Empty;
        var email = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email || c.Type == "email")?.Value ?? string.Empty;
        var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value;

        _currentUser = new User
        {
            Id = int.TryParse(sub, out var id) ? id : 0,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            IsActive = true
        };
    }

    private string BuildEndpoint(string path)
    {
        if (_httpClient.BaseAddress == null)
        {
            return path;
        }

        return new Uri(_httpClient.BaseAddress, path).ToString();
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }
}
