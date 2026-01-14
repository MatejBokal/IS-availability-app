using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Services;

public class NotificationReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationReminderService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1); // Check once per day

    public NotificationReminderService(IServiceProvider serviceProvider, ILogger<NotificationReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit on startup before first check
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendRemindersAsync();
                await CleanupOldNotificationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in NotificationReminderService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckAndSendRemindersAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

        // Get days before lock setting
        var daysBeforeLockSetting = await context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "DaysBeforeLockForReminder");
        
        var daysBeforeLock = 5; // Default: 5 days
        if (daysBeforeLockSetting != null && int.TryParse(daysBeforeLockSetting.Value, out var days))
        {
            daysBeforeLock = days;
        }

        var now = DateTime.UtcNow;
        var allMonths = await context.AvailabilityMonths
            .Where(m => m.IsUnlocked && m.LockDateTimeUtc.HasValue && m.LockDateTimeUtc.Value > now)
            .ToListAsync();

        foreach (var month in allMonths)
        {
            if (!month.LockDateTimeUtc.HasValue)
                continue;

            var daysUntilLock = (month.LockDateTimeUtc.Value - now).Days;

            // Day X: Send deadline reminder to all workers
            if (daysUntilLock == daysBeforeLock)
            {
                await notificationService.CreateDeadlineReminderNotificationsAsync(
                    month.MonthKey,
                    month.LockDateTimeUtc.Value
                );
                _logger.LogInformation("Sent deadline reminders for month {MonthKey}", month.MonthKey);
            }
            // Days X-1, X-2, ..., 1: Send missing submission reminders
            else if (daysUntilLock < daysBeforeLock && daysUntilLock > 0)
            {
                // Get all workers with notifications enabled
                var workerUsers = await userManager.GetUsersInRoleAsync("Worker");
                var workersWithNotifications = workerUsers
                    .Where(u => u.EnableNotifications && u.IsActive)
                    .Select(u => u.Id)
                    .ToList();

                // Get users who have already submitted
                var submittedUserIds = await context.AvailabilitySubmissions
                    .Where(s => s.AvailabilityMonthId == month.Id)
                    .Select(s => s.UserId)
                    .ToListAsync();

                // Find users who haven't submitted
                var usersWithoutSubmission = workersWithNotifications
                    .Except(submittedUserIds)
                    .ToList();

                if (usersWithoutSubmission.Any())
                {
                    await notificationService.CreateMissingSubmissionReminderNotificationsAsync(
                        month.MonthKey,
                        month.LockDateTimeUtc.Value,
                        usersWithoutSubmission
                    );
                    _logger.LogInformation("Sent missing submission reminders for month {MonthKey} to {Count} users", 
                        month.MonthKey, usersWithoutSubmission.Count);
                }
            }
        }
    }

    private async Task CleanupOldNotificationsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-10);
        var oldNotifications = await context.Notifications
            .Where(n => n.CreatedAtUtc < cutoffDate)
            .ToListAsync();

        if (oldNotifications.Any())
        {
            context.Notifications.RemoveRange(oldNotifications);
            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} old notifications", oldNotifications.Count);
        }
    }
}
