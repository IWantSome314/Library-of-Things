using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Maui.Storage;
using StarterApp.Database.Models;

namespace StarterApp.Services;

public class JWTAuthenticationService : IJWTAuthenticationService
{
    private User? _currentUser;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _tokenExpirationUtc;
    private List<string> _currentUserRoles = new();

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

    public async Task InitializeAsync()
    {
        var storedAccessToken = await SecureStorage.Default.GetAsync(AccessTokenStorageKey);
        var storedRefreshToken = await SecureStorage.Default.GetAsync(RefreshTokenStorageKey);
        var storedExpiry = await SecureStorage.Default.GetAsync(TokenExpiryStorageKey);

        _accessToken = storedAccessToken;
        _refreshToken = storedRefreshToken;

        if (DateTime.TryParse(storedExpiry, out var parsedExpiry))
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

    public async Task<bool> EnsureValidTokenAsync()
    {
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

    public async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        var endpoint = BuildEndpoint("/auth/token");
        try
        {
            var payload = new { email, password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/auth/token", content);
            if (!response.IsSuccessStatusCode)
                return new AuthenticationResult(false, "Invalid email or password");
            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody, _jsonOptions);
            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken) || string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
                return new AuthenticationResult(false, "Invalid token payload received");

            await SetTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken, tokenResponse.ExpiresAtUtc);
            HydrateUserFromJwt(_accessToken!);

            AuthenticationStateChanged?.Invoke(this, true);
            return new AuthenticationResult(true, "Login Successful");
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

    private class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }


    public Task LogoutAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        _tokenExpirationUtc = DateTime.MinValue;
        _currentUser = null;
        _currentUserRoles.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = null;
        SecureStorage.Default.Remove(AccessTokenStorageKey);
        SecureStorage.Default.Remove(RefreshTokenStorageKey);
        SecureStorage.Default.Remove(TokenExpiryStorageKey);
        AuthenticationStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var isTokenValid = await EnsureValidTokenAsync();
        if (!isTokenValid)
        {
            return false;
        }

        try
        {
            var payload = new { currentPassword, newPassword };
            var response = await PostAuthenticatedAsync("/auth/change-password", payload);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string BuildEndpoint(string path)
    {
        if (_httpClient.BaseAddress == null)
        {
            return path;
        }

        return new Uri(_httpClient.BaseAddress, path).ToString();
    }

    // --- Role Methods ---
    public bool HasRole(string roleName) => _currentUserRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    public bool HasAnyRole(params string[] roleNames) => roleNames.Any(r => HasRole(r));

    public bool HasAllRoles(params string[] roleNames) => roleNames.All(r => HasRole(r));

    private void HydrateUserFromJwt(string accessToken)
    {
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

    private async Task<HttpResponseMessage> PostAuthenticatedAsync<TPayload>(string path, TPayload payload)
    {
        await EnsureValidTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        ApplyAuthorizationHeader(request);

        var response = await _httpClient.SendAsync(request);
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var refreshed = await RefreshAccessTokenAsync();
        if (!refreshed)
        {
            return response;
        }

        response.Dispose();
        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        ApplyAuthorizationHeader(retryRequest);
        return await _httpClient.SendAsync(retryRequest);
    }

    private async Task<bool> RefreshAccessTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
        {
            return false;
        }

        await _refreshLock.WaitAsync();
        try
        {
            if (_tokenExpirationUtc > DateTime.UtcNow.AddSeconds(60) && !string.IsNullOrWhiteSpace(_accessToken))
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

            var body = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body, _jsonOptions);
            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken) || string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            {
                return false;
            }

            await SetTokensAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken, tokenResponse.ExpiresAtUtc);
            HydrateUserFromJwt(tokenResponse.AccessToken);
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

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        await SecureStorage.Default.SetAsync(AccessTokenStorageKey, _accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenStorageKey, _refreshToken);
        await SecureStorage.Default.SetAsync(TokenExpiryStorageKey, _tokenExpirationUtc.ToString("O"));
    }

    private void ApplyAuthorizationHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }
}

