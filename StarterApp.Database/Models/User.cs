using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StarterApp.Database.Models;

[Table("users")]
[PrimaryKey(nameof(Id))]
public class User
{
    public int Id { get; set; }
    [Required]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    public string LastName { get; set; } = string.Empty;
    [Required]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    [Required]
    public string PasswordSalt { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public List<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public List<Item> OwnedItems { get; set; } = new List<Item>();
    public List<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public List<RentalRequest> RentalRequests { get; set; } = new List<RentalRequest>();
    public List<Review> Reviews { get; set; } = new List<Review>();
    
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}