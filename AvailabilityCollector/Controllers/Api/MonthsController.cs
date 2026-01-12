using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers.Api;

[ApiController]
[Route("api/months")]
[Authorize]
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

    [HttpPost("{monthKey}/unlock")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UnlockMonthResponse>> UnlockMonth(string monthKey)
    {
        // Validate monthKey format (MM-yyyy)
        if (!System.Text.RegularExpressions.Regex.IsMatch(monthKey, @"^\d{2}-\d{4}$"))
        {
            return BadRequest(new { error = "Invalid monthKey format. Expected format: MM-yyyy (e.g., '02-2026')" });
        }

        var month = await _context.AvailabilityMonths
            .FirstOrDefaultAsync(m => m.MonthKey == monthKey);

        if (month == null)
        {
            month = new AvailabilityMonth
            {
                MonthKey = monthKey,
                IsUnlocked = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.AvailabilityMonths.Add(month);
        }
        else
        {
            month.IsUnlocked = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new UnlockMonthResponse(month.MonthKey, month.IsUnlocked, month.LockDateTimeUtc));
    }
}
