using System.ComponentModel.DataAnnotations;

namespace AvailabilityCollector.Models;

public class ShiftMatrix
{
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<PositionShift> PositionShifts { get; set; } = new();
}
