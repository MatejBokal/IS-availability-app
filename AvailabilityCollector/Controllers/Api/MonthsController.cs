using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers.Api;

[ApiController]
[Route("api/months")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MonthsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MonthsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public record MonthDto(string MonthKey, bool IsUnlocked, DateTime? LockDateTimeUtc);
    public record UnlockMonthResponse(string MonthKey, bool IsUnlocked, DateTime? LockDateTimeUtc);

    [HttpGet("unlocked")]
    public async Task<ActionResult<List<MonthDto>>> GetUnlockedMonths()
    {
        var now = DateTime.UtcNow;
        var unlockedMonths = await _context.AvailabilityMonths
            .Where(m => m.IsUnlocked && (m.LockDateTimeUtc == null || m.LockDateTimeUtc > now))
            .OrderBy(m => m.MonthKey)
            .Select(m => new MonthDto(m.MonthKey, m.IsUnlocked, m.LockDateTimeUtc))
            .ToListAsync();

        return Ok(unlockedMonths);
    }

    [HttpGet("available")]
    [Authorize(Roles = "Worker,Admin")]
    public async Task<ActionResult<List<MonthDto>>> GetAvailableMonths()
    {
        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var nextMonth = currentMonth.AddMonths(1);

        // Load all months that were ever unlocked (or have submissions)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var userSubmissions = await _context.AvailabilitySubmissions
            .Where(s => s.UserId == userId)
            .Select(s => s.AvailabilityMonthId)
            .ToListAsync();

        var allMonths = await _context.AvailabilityMonths.ToListAsync();

        // Filter months that are at least 1 month in advance OR have user submissions
        var availableMonths = allMonths
            .Where(m =>
            {
                try
                {
                    var monthDate = DateTime.ParseExact(m.MonthKey, "MM-yyyy", null);
                    // Include months that are at least next month, or months where user has submissions
                    return monthDate >= nextMonth || userSubmissions.Contains(m.Id);
                }
                catch
                {
                    return false;
                }
            })
            .OrderBy(m =>
            {
                try
                {
                    return DateTime.ParseExact(m.MonthKey, "MM-yyyy", null);
                }
                catch
                {
                    return DateTime.MaxValue;
                }
            })
            .Select(m => new MonthDto(m.MonthKey, m.IsUnlocked, m.LockDateTimeUtc))
            .ToList();

        return Ok(availableMonths);
    }

    [HttpPost("{monthKey}/unlock")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UnlockMonthResponse>> UnlockMonth(string monthKey)
    {
        // Validate monthKey format (MM-yyyy)
        if (!System.Text.RegularExpressions.Regex.IsMatch(monthKey, @"^\d{2}-\d{4}$"))
        {
            return BadRequest(new { error = "Invalid monthKey format. Expected format: MM-yyyy (e.g., '02-2026')" });
        }

        // Get auto-lock day of month setting (default to 1 = lock on month start)
        var autoLockDaySetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AutoLockDayOfMonth");
        
        var autoLockDay = 1; // Default: lock on month start (day 1 of previous month)
        if (autoLockDaySetting != null && int.TryParse(autoLockDaySetting.Value, out var day))
        {
            autoLockDay = day;
        }

        // Calculate lock date based on setting
        var monthParts = monthKey.Split('-');
        var year = int.Parse(monthParts[1]);
        var monthNum = int.Parse(monthParts[0]);
        var previousMonth = new DateTime(year, monthNum, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        
        // Use the specified day of the previous month, but ensure it's valid (handle months with fewer days)
        var daysInPreviousMonth = DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month);
        var lockDay = Math.Min(autoLockDay, daysInPreviousMonth);
        var lockDateTime = new DateTime(previousMonth.Year, previousMonth.Month, lockDay, 23, 59, 59, DateTimeKind.Utc);

        var month = await _context.AvailabilityMonths
            .FirstOrDefaultAsync(m => m.MonthKey == monthKey);

        if (month == null)
        {
            month = new AvailabilityMonth
            {
                MonthKey = monthKey,
                IsUnlocked = true,
                LockDateTimeUtc = lockDateTime,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.AvailabilityMonths.Add(month);
        }
        else
        {
            month.IsUnlocked = true;
            month.LockDateTimeUtc = lockDateTime;
        }

        await _context.SaveChangesAsync();

        return Ok(new UnlockMonthResponse(month.MonthKey, month.IsUnlocked, month.LockDateTimeUtc));
    }
}
