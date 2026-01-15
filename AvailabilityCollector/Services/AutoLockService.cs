using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Data;

namespace AvailabilityCollector.Services;

public class AutoLockService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoLockService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

    public AutoLockService(IServiceProvider serviceProvider, ILogger<AutoLockService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AutoLockMonthsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoLockService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task AutoLockMonthsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;

        // Find all months that should be locked (LockDateTimeUtc has passed but still unlocked)
        var monthsToLock = await context.AvailabilityMonths
            .Where(m => m.IsUnlocked && 
                       m.LockDateTimeUtc.HasValue && 
                       m.LockDateTimeUtc.Value <= now)
            .ToListAsync();

        if (monthsToLock.Any())
        {
            foreach (var month in monthsToLock)
            {
                month.IsUnlocked = false;
                _logger.LogInformation("Auto-locked month {MonthKey}", month.MonthKey);
            }

            await context.SaveChangesAsync();
        }
    }
}
