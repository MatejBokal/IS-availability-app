using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers;

[Authorize(Roles = "Worker")]
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
        ViewBag.MinDurationMinutes = 240; // 4 hours - hardcoded for now

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

        ViewBag.MonthKey = monthKey;
        ViewBag.DaysInMonth = daysInMonth;
        ViewBag.FirstDay = firstDay;
        ViewBag.Holidays = holidays;
        ViewBag.Submission = submission;

        return View();
    }
}
