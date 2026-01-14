using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers;

[Authorize(Roles = "Worker,Admin")]
public class WorkerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [Route("oddaj-razpolozljivost")]
    public async Task<IActionResult> OddajRazpolozljivost()
    {
        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var nextMonth = currentMonth.AddMonths(1);

        // Load all months that were ever unlocked (or have submissions)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            .ToList();

        ViewBag.CurrentMonthKey = currentMonth.ToString("MM-yyyy");
        ViewBag.NextMonthKey = nextMonth.ToString("MM-yyyy");

        return View(availableMonths);
    }

    [Route("oddaj-razpolozljivost/{monthKey}")]
    public async Task<IActionResult> OddajRazpolozljivostMesec(string monthKey)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        // Parse month to get days
        var monthParts = monthKey.Split('-');
        var year = int.Parse(monthParts[1]);
        var monthNum = int.Parse(monthParts[0]);
        var firstDay = new DateOnly(year, monthNum, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var daysInMonth = lastDay.Day;

        // Get or create month
        var month = await _context.AvailabilityMonths
            .FirstOrDefaultAsync(m => m.MonthKey == monthKey);

        if (month == null)
        {
            month = new AvailabilityMonth
            {
                MonthKey = monthKey,
                IsUnlocked = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.AvailabilityMonths.Add(month);
            await _context.SaveChangesAsync();
        }

        // Get existing submission if any
        var submission = await _context.AvailabilitySubmissions
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AvailabilityMonthId == month.Id);

        // Get holidays for highlighting
        var holidays = await _context.Holidays
            .Where(h => h.Year == year)
            .ToListAsync();

        ViewBag.MonthKey = monthKey;
        ViewBag.Month = month;
        ViewBag.DaysInMonth = daysInMonth;
        ViewBag.FirstDay = firstDay;
        ViewBag.Holidays = holidays;
        ViewBag.Submission = submission;
        // Get minimum time range setting (default to 4 hours)
        var minTimeRangeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "MinTimeRangeHours");
        
        var minDurationMinutes = 240; // Default: 4 hours
        if (minTimeRangeSetting != null && double.TryParse(minTimeRangeSetting.Value, out var hours))
        {
            minDurationMinutes = (int)(hours * 60);
        }
        
        // Get allowed time window settings (default to 07:00 - 23:00)
        var allowedStartTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedStartTime");
        
        var allowedStartTime = "07:00"; // Default
        if (allowedStartTimeSetting != null && !string.IsNullOrEmpty(allowedStartTimeSetting.Value))
        {
            allowedStartTime = allowedStartTimeSetting.Value;
        }

        var allowedEndTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedEndTime");
        
        var allowedEndTime = "23:00"; // Default
        if (allowedEndTimeSetting != null && !string.IsNullOrEmpty(allowedEndTimeSetting.Value))
        {
            allowedEndTime = allowedEndTimeSetting.Value;
        }
        
        ViewBag.MinDurationMinutes = minDurationMinutes;
        ViewBag.AllowedStartTime = allowedStartTime;
        ViewBag.AllowedEndTime = allowedEndTime;

        return View();
    }

    [Route("zgodovina")]
    public async Task<IActionResult> Zgodovina()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var submissions = await _context.AvailabilitySubmissions
            .Include(s => s.AvailabilityMonth)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.AvailabilityMonth.MonthKey)
            .ToListAsync();

        return View(submissions);
    }

    [Route("zgodovina/{monthKey}")]
    public async Task<IActionResult> ZgodovinaMesec(string monthKey)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var month = await _context.AvailabilityMonths
            .FirstOrDefaultAsync(m => m.MonthKey == monthKey);

        if (month == null)
        {
            return NotFound();
        }

        // Get submission
        var submission = await _context.AvailabilitySubmissions
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AvailabilityMonthId == month.Id);

        if (submission == null)
        {
            return NotFound();
        }

        // Parse month to get days
        var monthParts = monthKey.Split('-');
        var year = int.Parse(monthParts[1]);
        var monthNum = int.Parse(monthParts[0]);
        var firstDay = new DateOnly(year, monthNum, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var daysInMonth = lastDay.Day;

        // Get holidays for highlighting
        var holidays = await _context.Holidays
            .Where(h => h.Year == year)
            .ToListAsync();

        // Get minimum time range setting (default to 4 hours)
        var minTimeRangeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "MinTimeRangeHours");
        
        var minDurationMinutes = 240; // Default: 4 hours
        if (minTimeRangeSetting != null && double.TryParse(minTimeRangeSetting.Value, out var hours))
        {
            minDurationMinutes = (int)(hours * 60);
        }
        
        ViewBag.MonthKey = monthKey;
        ViewBag.Month = month;
        ViewBag.DaysInMonth = daysInMonth;
        ViewBag.FirstDay = firstDay;
        ViewBag.Holidays = holidays;
        ViewBag.Submission = submission;
        ViewBag.MinDurationMinutes = minDurationMinutes;

        return View();
    }

    [HttpGet]
    [Authorize(Roles = "Worker,Admin")]
    public IActionResult Nastavitve()
    {
        return View();
    }

    [HttpPost]
    [Route("nastavitve/update-notification-preference")]
    [Authorize(Roles = "Worker,Admin")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateNotificationPreference([FromBody] UpdateNotificationPreferenceRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        user.EnableNotifications = request.EnableNotifications;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "Nastavitve so bile posodobljene" });
    }

    public class UpdateNotificationPreferenceRequest
    {
        public bool EnableNotifications { get; set; }
    }
}
