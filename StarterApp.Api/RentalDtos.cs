using System.ComponentModel.DataAnnotations;

namespace StarterApp.Api;

public sealed class CreateRentalRequestDto
{
    public int ItemId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
}

public sealed class RentalRequestSummaryResponse
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public int RequestorUserId { get; set; }
    public string RequestorName { get; set; } = string.Empty;
    public int OwnerUserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
