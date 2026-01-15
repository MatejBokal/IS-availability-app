using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task CreateMonthUnlockedNotificationsAsync(string monthKey, bool includeAdmins = false)
    {
        var notifications = new List<Notification>();

        // Get all workers with notifications enabled
        var workerUsers = await _userManager.GetUsersInRoleAsync("Worker");
        var workersWithNotifications = workerUsers
            .Where(u => u.EnableNotifications && u.IsActive)
            .ToList();

        foreach (var worker in workersWithNotifications)
        {
            notifications.Add(new Notification
            {
                UserId = worker.Id,
                Title = $"Mesec {monthKey} je odklenjen",
                Body = $"Mesec {monthKey} je bil odklenjen za oddajo razpoložljivosti. Prosimo oddajte svojo razpoložljivost.",
                Type = NotificationType.MonthUnlocked,
                RelatedMonthKey = monthKey,
                CreatedAtUtc = DateTime.UtcNow,
                SentAtUtc = DateTime.UtcNow
            });
        }

        // Get all admins if enabled
        if (includeAdmins)
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in adminUsers)
            {
                notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Title = $"Mesec {monthKey} je odklenjen",
                    Body = $"Mesec {monthKey} je bil odklenjen za oddajo razpoložljivosti.",
                    Type = NotificationType.MonthUnlocked,
                    RelatedMonthKey = monthKey,
                    CreatedAtUtc = DateTime.UtcNow,
                    SentAtUtc = DateTime.UtcNow
                });
            }
        }

        if (notifications.Any())
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
    }

    public async Task CreateCustomNotificationAsync(
        string title,
        string body,
        List<string> userIds)
    {
        var notifications = userIds.Select(userId => new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Type = NotificationType.Custom,
            CreatedAtUtc = DateTime.UtcNow,
            SentAtUtc = DateTime.UtcNow
        }).ToList();

        if (notifications.Any())
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
    }

    public async Task CreateDeadlineReminderNotificationsAsync(string monthKey, DateTime lockDateTime)
    {
        var workerUsers = await _userManager.GetUsersInRoleAsync("Worker");
        var workersWithNotifications = workerUsers
            .Where(u => u.EnableNotifications && u.IsActive)
            .ToList();

        var notifications = workersWithNotifications.Select(worker => new Notification
        {
            UserId = worker.Id,
            Title = $"Opomnik: Razpoložljivost za {monthKey}",
            Body = $"Razpoložljivost za mesec {monthKey} mora biti oddana do {lockDateTime.ToLocalTime():dd.MM.yyyy HH:mm}. Prosimo oddajte svojo razpoložljivost.",
            Type = NotificationType.DeadlineReminder,
            RelatedMonthKey = monthKey,
            CreatedAtUtc = DateTime.UtcNow,
            SentAtUtc = DateTime.UtcNow
        }).ToList();

        if (notifications.Any())
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
    }

    public async Task CreateMissingSubmissionReminderNotificationsAsync(string monthKey, DateTime lockDateTime, List<string> userIdsWithoutSubmission)
    {
        if (!userIdsWithoutSubmission.Any())
            return;

        var notifications = userIdsWithoutSubmission.Select(userId => new Notification
        {
            UserId = userId,
            Title = $"Opomnik: Manjka razpoložljivost za {monthKey}",
            Body = $"Še niste oddali razpoložljivosti za mesec {monthKey}. Prosimo oddajte jo do {lockDateTime.ToLocalTime():dd.MM.yyyy HH:mm}.",
            Type = NotificationType.MissingSubmissionReminder,
            RelatedMonthKey = monthKey,
            CreatedAtUtc = DateTime.UtcNow,
            SentAtUtc = DateTime.UtcNow
        }).ToList();

        if (notifications.Any())
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
    }
}
