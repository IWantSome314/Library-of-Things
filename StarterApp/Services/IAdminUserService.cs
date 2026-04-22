using StarterApp.Database.Models;

namespace StarterApp.Services;

public interface IAdminUserService
{
    Task<List<User>> GetActiveUsersWithRolesAsync();
    Task<List<Role>> GetAllRolesAsync();
    Task<User?> GetUserByIdWithRolesAsync(int userId);
    Task<bool> EmailExistsAsync(string email, int? excludeUserId = null);
    Task<User> CreateUserAsync(User user, IReadOnlyCollection<int> roleIds);
    Task UpdateUserAsync(User user);
    Task SoftDeleteUserAsync(User user);
    Task AddRoleAsync(int userId, int roleId);
    Task RemoveRoleAsync(int userId, int roleId);
}
