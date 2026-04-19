using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StarterApp.Database.Models;

[Table("review")]
[PrimaryKey(nameof(Id))]
public class Review
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public int ReviewerUserId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Item Item { get; set; } = null!;

    public User ReviewerUser { get; set; } = null!;
}
