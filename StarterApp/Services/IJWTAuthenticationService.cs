using StarterApp.Database.Models;

namespace StarterApp.Services;

public interface IJWTAuthenticationService
{
    event EventHandler<bool>? AuthenticationStateChanged;

    bool IsAuthenticated { get; }
    User? CurrentUser { get; }
    List<string> CurrentUserRoles { get; }

    Task<AuthenticationResult> LoginAsync(string email, string password);
    Task<AuthenticationResult> RegisterAsync(string firstName, string lastName, string email, string password);
    Task LogoutAsync();
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword);

    bool HasRole(string roleName);
    bool HasAnyRole(params string[] roleNames);
    bool HasAllRoles(params string[] roleNames);
}