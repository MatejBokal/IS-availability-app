using System.ComponentModel.DataAnnotations;

namespace AvailabilityCollector.Models;

public enum NotificationType
{
    MonthUnlocked = 0,
    DeadlineReminder = 1,
    MissingSubmissionReminder = 2,
    Custom = 3
}

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!; // FK to AspNetUsers

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = default!;

    [Required]
    [MaxLength(2000)]
    public string Body { get; set; } = default!;

    public NotificationType Type { get; set; } = NotificationType.Custom;

    public bool IsRead { get; set; } = false;
    public DateTime? ReadAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; } // For scheduled reminders

    [MaxLength(7)]
    public string? RelatedMonthKey { get; set; } // e.g., "02-2026" if related to a month
}
