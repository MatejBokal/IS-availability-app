using System.ComponentModel.DataAnnotations;

namespace AvailabilityCollector.Models;

public class AvailabilitySubmission
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!; // Identity user id

    public int AvailabilityMonthId { get; set; }
    public AvailabilityMonth AvailabilityMonth { get; set; } = default!;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    // Optional: Draft/Submitted
    [MaxLength(20)]
    public string Status { get; set; } = "Submitted";

    public List<AvailabilityEntry> Entries { get; set; } = new();
}
