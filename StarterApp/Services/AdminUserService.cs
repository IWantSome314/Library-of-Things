using Microsoft.EntityFrameworkCore;
using StarterApp.Database.Data;
using StarterApp.Database.Models;

namespace StarterApp.Services;

public sealed class AdminUserService : IAdminUserService
{
    private readonly AppDbContext _context;

    public AdminUserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<User>> GetActiveUsersWithRolesAsync()
    {
        return await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await _context.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<User?> GetUserByIdWithRolesAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeUserId = null)
    {
        var normalizedEmail = email.Trim();
        var query = _context.Users.AsQueryable();

        if (excludeUserId.HasValue)
        {
            query = query.Where(u => u.Id != excludeUserId.Value);
        }

        return await query.AnyAsync(u => u.Email == normalizedEmail);
    }

    public async Task<User> CreateUserAsync(User user, IReadOnlyCollection<int> roleIds)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        if (roleIds.Count > 0)
        {
            var userRoles = roleIds.Select(roleId => new UserRole(user.Id, roleId));
            _context.UserRoles.AddRange(userRoles);
            await _context.SaveChangesAsync();
        }

        return user;
    }

    public async Task UpdateUserAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteUserAsync(User user)
    {
        user.IsActive = false;
        user.DeletedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync();

        foreach (var userRole in userRoles)
        {
            userRole.MarkAsDeleted();
        }

        _context.Users.Update(user);
        _context.UserRoles.UpdateRange(userRoles);
        await _context.SaveChangesAsync();
    }

    public async Task AddRoleAsync(int userId, int roleId)
    {
        var existingRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (existingRole != null)
        {
            if (!existingRole.IsActive)
            {
                existingRole.IsActive = true;
                existingRole.DeletedAt = null;
                existingRole.UpdatedAt = DateTime.UtcNow;
                _context.UserRoles.Update(existingRole);
                await _context.SaveChangesAsync();
            }

            return;
        }

        _context.UserRoles.Add(new UserRole(userId, roleId));
        await _context.SaveChangesAsync();
    }

    public async Task RemoveRoleAsync(int userId, int roleId)
    {
        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.IsActive);

        if (userRole == null)
        {
            return;
        }

        userRole.MarkAsDeleted();
        _context.UserRoles.Update(userRole);
        await _context.SaveChangesAsync();
    }
}
