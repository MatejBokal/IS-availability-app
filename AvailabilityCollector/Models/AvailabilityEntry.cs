using System.ComponentModel.DataAnnotations;

namespace AvailabilityCollector.Models;

public enum AvailabilityType
{
    Unavailable = 0,
    FullDay = 1,
    TimeRange = 2
}

public class AvailabilityEntry
{
    public int Id { get; set; }

    public int AvailabilitySubmissionId { get; set; }
    public AvailabilitySubmission AvailabilitySubmission { get; set; } = default!;

    [Required]
    public DateOnly Date { get; set; } // EF Core supports DateOnly with SQL Server

    public AvailabilityType Type { get; set; } = AvailabilityType.Unavailable;

    // Use TimeOnly? for time range (nullable)
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
}
