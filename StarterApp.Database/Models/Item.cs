using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StarterApp.Database.Models;

[Table("item")]
[PrimaryKey(nameof(Id))]
public class Item
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "numeric(10,2)")]
    public decimal DailyRate { get; set; }

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public string Location { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int OwnerUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public User OwnerUser { get; set; } = null!;
    public List<RentalRequest> RentalRequests { get; set; } = new List<RentalRequest>();
    public List<Review> Reviews { get; set; } = new List<Review>();
}
