using System.ComponentModel.DataAnnotations;

namespace AvailabilityCollector.Models;

public class Position
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
