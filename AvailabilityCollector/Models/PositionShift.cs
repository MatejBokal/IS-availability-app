using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AvailabilityCollector.Models;

public class PositionShift
{
    public int Id { get; set; }

    public int ShiftMatrixId { get; set; }
    public ShiftMatrix ShiftMatrix { get; set; } = default!;

    public int PositionId { get; set; }
    [ForeignKey("PositionId")]
    public Position Position { get; set; } = default!;

    public List<ShiftEntry> ShiftEntries { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
