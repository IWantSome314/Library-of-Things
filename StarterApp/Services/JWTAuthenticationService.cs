using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using StarterApp.Database.Models;

namespace StarterApp.Services;

public class JWTAuthenticationService : IJWTAuthenticationService
{

    private User? _currentUser;
    private readonly HttpClient _httpClient;
    private string? _jwtToken;
    private DateTime _tokenExpiration;
    private List<string> _currentUserRoles = new();
    public event EventHandler<bool>? AuthenticationStateChanged;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_jwtToken) && _tokenExpiration > DateTime.UtcNow;
    public User? CurrentUser => _currentUser;
    public List<string> CurrentUserRoles => _currentUserRoles;

    public JWTAuthenticationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.Token))
                return new AuthenticationResult(false, "invalid token recieved");
            
            _jwtToken = tokenResponse.Token;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            var jwtHandler = new JwtSecurityTokenHandler();
            var jwt = jwtHandler.ReadJwtToken(_jwtToken);
            _tokenExpiration = jwt.ValidTo;

            _currentUserRoles = jwt.Claims
                .Where(c => c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            var firstName = jwt.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value ?? string.Empty;
            var lastName = jwt.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value ?? string.Empty;
            var userEmail = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? email;

            _currentUser = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = userEmail,
                IsActive = true
            };

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
        public string Token { get; set; } = string.Empty;
    }


    public Task LogoutAsync()
    {
        _jwtToken = null;
        _tokenExpiration = DateTime.MinValue;
        _currentUser = null;
        _currentUserRoles.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = null;
        AuthenticationStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (!IsAuthenticated)
        {
            return false;
        }

        try
        {
            var payload = new { currentPassword, newPassword };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/auth/change-password", content);
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
}

