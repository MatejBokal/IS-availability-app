using System.ComponentModel.DataAnnotations;

namespace AvailabilityCollector.Models;

public class AvailabilityMonth
{
    public int Id { get; set; }

    // "02-2026"
    [Required]
    [MaxLength(7)]
    public string MonthKey { get; set; } = default!;

    public bool IsUnlocked { get; set; }
    public DateTime? LockDateTimeUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
