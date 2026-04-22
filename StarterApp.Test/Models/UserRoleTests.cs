using StarterApp.Database.Models;

namespace StarterApp.Test.Models;

public class UserRoleTests
{
    [Fact]
    public void MarkAsDeleted_SetsInactiveAndDeletedAt()
    {
        var userRole = new UserRole(1, 2);

        userRole.MarkAsDeleted();

        Assert.False(userRole.IsActive);
        Assert.NotNull(userRole.DeletedAt);
    }

    [Fact]
    public void Restore_ClearsDeletedAtAndSetsActive()
    {
        var userRole = new UserRole(1, 2);
        userRole.MarkAsDeleted();

        userRole.Restore();

        Assert.True(userRole.IsActive);
        Assert.Null(userRole.DeletedAt);
    }

    [Fact]
    public void UpdateTimestamps_RefreshesUpdatedAt()
    {
        var userRole = new UserRole(1, 2) { UpdatedAt = DateTime.UtcNow.AddDays(-1) };
        var before = userRole.UpdatedAt;

        userRole.UpdateTimestamps();

        Assert.True(userRole.UpdatedAt > before);
    }

    [Fact]
    public void Equals_ReturnsTrueForEquivalentObjects()
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-5);
        var updatedAt = DateTime.UtcNow;
        var deletedAt = DateTime.UtcNow.AddMinutes(1);

        var left = new UserRole
        {
            Id = 10,
            UserId = 1,
            RoleId = 2,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            DeletedAt = deletedAt,
            IsActive = false
        };

        var right = new UserRole
        {
            Id = 10,
            UserId = 1,
            RoleId = 2,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            DeletedAt = deletedAt,
            IsActive = false
        };

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentObject()
    {
        var left = new UserRole(1, 2);
        var right = new UserRole(1, 3);

        Assert.False(left.Equals(right));
        Assert.False(left.Equals(null));
    }
}
