using System.ComponentModel.DataAnnotations;

namespace AvailabilityCollector.Models;

public class AppSettings
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = default!;

    [MaxLength(500)]
    public string? Value { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
