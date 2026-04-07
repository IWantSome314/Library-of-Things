using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StarterApp.Database.Models;

[Table("rental_request")]
[PrimaryKey(nameof(Id))]
public class RentalRequest
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public int RequestorUserId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    [Column(TypeName = "numeric(10,2)")]
    public decimal TotalPrice { get; set; }

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Item Item { get; set; } = null!;

    public User RequestorUser { get; set; } = null!;
}