using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AvailabilityCollector.Models;

public class ShiftEntry
{
    public int Id { get; set; }

    public int PositionShiftId { get; set; }
    public PositionShift PositionShift { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Days { get; set; } = default!; // "pon,tor,sre,cet,pet"

    [Required]
    [MaxLength(20)]
    public string ShiftTime { get; set; } = default!; // "8:00-15:00"

    public int NumberOfPeople { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
