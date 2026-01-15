using System.ComponentModel.DataAnnotations;

namespace AvailabilityCollector.Models;

public class Holiday
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = default!;
    
    [Required]
    public DateOnly Date { get; set; }
    
    public int Year { get; set; } // Store year for filtering
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
