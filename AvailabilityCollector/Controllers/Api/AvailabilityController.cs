using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers.Api;

[ApiController]
[Route("api/availability/my")]
[Authorize(Roles = "Worker")]
public class AvailabilityController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private const int MinDurationMinutes = 240; // 4 hours - hardcoded for now

    public AvailabilityController(ApplicationDbContext context)
    {
        _context = context;
    }

    public record AvailabilityEntryDto(string Date, string Type, string? StartTime, string? EndTime);
    public record AvailabilitySubmissionDto(string MonthKey, DateTime? SubmittedAtUtc, List<AvailabilityEntryDto> Entries);
    public record CreateAvailabilityRequest(string MonthKey, List<AvailabilityEntryDto> Entries);

    [HttpGet]
    public async Task<ActionResult<AvailabilitySubmissionDto>> GetMyAvailability([FromQuery] string monthKey)
    {
        if (string.IsNullOrEmpty(monthKey))
        {
            return BadRequest(new { error = "monthKey is required" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var month = await _context.AvailabilityMonths
            .FirstOrDefaultAsync(m => m.MonthKey == monthKey);

        if (month == null)
        {
            return NotFound(new { error = "Month not found" });
        }

        var submission = await _context.AvailabilitySubmissions
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AvailabilityMonthId == month.Id);

        if (submission == null)
        {
            return NotFound(new { error = "No submission found for this month" });
        }

        var entries = submission.Entries.Select(e => new AvailabilityEntryDto(
            e.Date.ToString("yyyy-MM-dd"),
            e.Type.ToString(),
            e.StartTime?.ToString("HH:mm"),
            e.EndTime?.ToString("HH:mm")
        )).ToList();

        return Ok(new AvailabilitySubmissionDto(
            monthKey,
            submission.SubmittedAtUtc,
            entries
        ));
    }

    [HttpPost("submissions")]
    public async Task<ActionResult> CreateAvailabilitySubmission(CreateAvailabilityRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        // Validate and get month
        var month = await _context.AvailabilityMonths
            .FirstOrDefaultAsync(m => m.MonthKey == request.MonthKey);

        if (month == null)
        {
            return BadRequest(new { error = "Month not found. Admin must unlock the month first." });
        }

        // Check if month is unlocked and not locked yet
        if (!month.IsUnlocked)
        {
            return BadRequest(new { error = "Month is not unlocked for submissions" });
        }

        if (month.LockDateTimeUtc.HasValue && month.LockDateTimeUtc.Value <= DateTime.UtcNow)
        {
            return BadRequest(new { error = "Submission deadline has passed for this month" });
        }

        // Check if submission already exists
        var existingSubmission = await _context.AvailabilitySubmissions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AvailabilityMonthId == month.Id);

        if (existingSubmission != null)
        {
            return Conflict(new { error = "Submission already exists for this month. Use PUT to update." });
        }

        // Validate entries
        var validationError = ValidateEntries(request.Entries);
        if (validationError != null)
        {
            return BadRequest(new { error = validationError });
        }

        // Create submission
        var submission = new AvailabilitySubmission
        {
            UserId = userId,
            AvailabilityMonthId = month.Id,
            SubmittedAtUtc = DateTime.UtcNow,
            Status = "Submitted",
            Entries = new List<AvailabilityEntry>()
        };

        foreach (var entryDto in request.Entries)
        {
            var entry = new AvailabilityEntry
            {
                Date = DateOnly.Parse(entryDto.Date),
                Type = Enum.Parse<AvailabilityType>(entryDto.Type),
                StartTime = entryDto.StartTime != null ? TimeOnly.Parse(entryDto.StartTime) : null,
                EndTime = entryDto.EndTime != null ? TimeOnly.Parse(entryDto.EndTime) : null
            };
            submission.Entries.Add(entry);
        }

        _context.AvailabilitySubmissions.Add(submission);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMyAvailability), new { monthKey = request.MonthKey }, 
            new { message = "Submission created successfully", submissionId = submission.Id });
    }

    private string? ValidateEntries(List<AvailabilityEntryDto> entries)
    {
        foreach (var entry in entries)
        {
            // Validate date format
            if (!DateOnly.TryParse(entry.Date, out _))
            {
                return $"Invalid date format: {entry.Date}. Expected format: yyyy-MM-dd";
            }

            // Validate type
            if (!Enum.TryParse<AvailabilityType>(entry.Type, out var type))
            {
                return $"Invalid type: {entry.Type}. Valid values: Unavailable, FullDay, TimeRange";
            }

            // Type-specific validation
            if (type == AvailabilityType.Unavailable)
            {
                if (entry.StartTime != null || entry.EndTime != null)
                {
                    return "Unavailable entries cannot have start or end times";
                }
            }
            else if (type == AvailabilityType.FullDay)
            {
                if (entry.StartTime != null || entry.EndTime != null)
                {
                    return "FullDay entries cannot have start or end times";
                }
            }
            else if (type == AvailabilityType.TimeRange)
            {
                if (string.IsNullOrEmpty(entry.StartTime) || string.IsNullOrEmpty(entry.EndTime))
                {
                    return "TimeRange entries require both startTime and endTime";
                }

                if (!TimeOnly.TryParse(entry.StartTime, out var startTime) ||
                    !TimeOnly.TryParse(entry.EndTime, out var endTime))
                {
                    return $"Invalid time format. Expected format: HH:mm (e.g., '13:00')";
                }

                if (endTime <= startTime)
                {
                    return "EndTime must be greater than StartTime";
                }

                // Validate minimum duration (4 hours)
                var duration = endTime - startTime;
                if (duration.TotalMinutes < MinDurationMinutes)
                {
                    return $"TimeRange duration must be at least {MinDurationMinutes} minutes ({MinDurationMinutes / 60} hours)";
                }
            }
        }

        return null;
    }
}
