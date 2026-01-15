using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers.Api;

[ApiController]
[Route("api/availability/my")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Worker,Admin")]
public class AvailabilityController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AvailabilityController(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<int> GetMinDurationMinutesAsync()
    {
        var setting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "MinTimeRangeHours");
        
        if (setting != null && double.TryParse(setting.Value, out var hours))
        {
            return (int)(hours * 60);
        }
        
        return 240; // Default: 4 hours
    }

    private async Task<(TimeOnly StartTime, TimeOnly EndTime)> GetAllowedTimeWindowAsync()
    {
        var startTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedStartTime");
        
        var endTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedEndTime");
        
        var startTime = TimeOnly.Parse("07:00"); // Default: 07:00
        var endTime = TimeOnly.Parse("23:00"); // Default: 23:00
        
        if (startTimeSetting != null && !string.IsNullOrEmpty(startTimeSetting.Value) && 
            TimeOnly.TryParse(startTimeSetting.Value, out var parsedStartTime))
        {
            startTime = parsedStartTime;
        }
        
        if (endTimeSetting != null && !string.IsNullOrEmpty(endTimeSetting.Value) && 
            TimeOnly.TryParse(endTimeSetting.Value, out var parsedEndTime))
        {
            endTime = parsedEndTime;
        }
        
        return (startTime, endTime);
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

        // Check if lock after initial submission is enabled
        var lockAfterSubmissionSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "LockAfterInitialSubmission");
        
        var lockAfterSubmission = false;
        if (lockAfterSubmissionSetting != null && bool.TryParse(lockAfterSubmissionSetting.Value, out var lockSetting))
        {
            lockAfterSubmission = lockSetting;
        }

        // If lock after initial submission is enabled and submission already exists, prevent creating new one
        if (lockAfterSubmission && existingSubmission != null && existingSubmission.SubmittedAtUtc != default)
        {
            return BadRequest(new { error = "Submission already exists and editing is not allowed after initial submission. This setting is enabled by the administrator." });
        }

        if (existingSubmission != null && !lockAfterSubmission)
        {
            return Conflict(new { error = "Submission already exists for this month. Use PUT to update." });
        }

        // Validate entries
        var validationError = await ValidateEntriesAsync(request.Entries);
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

    [HttpPut("submissions/{id}")]
    public async Task<ActionResult> UpdateAvailabilitySubmission(int id, CreateAvailabilityRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        // Find submission and verify ownership
        var submission = await _context.AvailabilitySubmissions
            .Include(s => s.Entries)
            .Include(s => s.AvailabilityMonth)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (submission == null)
        {
            return NotFound(new { error = "Submission not found or you don't have permission to update it" });
        }

        // Verify monthKey matches
        if (submission.AvailabilityMonth.MonthKey != request.MonthKey)
        {
            return BadRequest(new { error = "MonthKey mismatch. Cannot change month of existing submission." });
        }

        // Check if month is still unlocked and not locked yet
        if (!submission.AvailabilityMonth.IsUnlocked)
        {
            return BadRequest(new { error = "Month is not unlocked for submissions" });
        }

        if (submission.AvailabilityMonth.LockDateTimeUtc.HasValue && submission.AvailabilityMonth.LockDateTimeUtc.Value <= DateTime.UtcNow)
        {
            return BadRequest(new { error = "Submission deadline has passed for this month" });
        }

        // Check if lock after initial submission is enabled
        var lockAfterSubmissionSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "LockAfterInitialSubmission");
        
        var lockAfterSubmission = false;
        if (lockAfterSubmissionSetting != null && bool.TryParse(lockAfterSubmissionSetting.Value, out var lockSetting))
        {
            lockAfterSubmission = lockSetting;
        }

        // If lock after initial submission is enabled and submission already exists, prevent editing
        if (lockAfterSubmission && submission.SubmittedAtUtc != default)
        {
            return BadRequest(new { error = "Editing is not allowed after initial submission. This setting is enabled by the administrator." });
        }

        // Validate entries
        var validationError = await ValidateEntriesAsync(request.Entries);
        if (validationError != null)
        {
            return BadRequest(new { error = validationError });
        }

        // Delete old entries
        _context.AvailabilityEntries.RemoveRange(submission.Entries);
        submission.Entries.Clear();

        // Add new entries
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

        submission.SubmittedAtUtc = DateTime.UtcNow;
        submission.Status = "Submitted";

        await _context.SaveChangesAsync();

        return Ok(new { message = "Submission updated successfully", submissionId = submission.Id });
    }

    [HttpDelete("submissions/{id}")]
    public async Task<ActionResult> DeleteAvailabilitySubmission(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        // Find submission and verify ownership
        var submission = await _context.AvailabilitySubmissions
            .Include(s => s.AvailabilityMonth)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (submission == null)
        {
            return NotFound(new { error = "Submission not found or you don't have permission to delete it" });
        }

        // Check if month is still unlocked (can't delete after lock)
        if (submission.AvailabilityMonth.LockDateTimeUtc.HasValue && submission.AvailabilityMonth.LockDateTimeUtc.Value <= DateTime.UtcNow)
        {
            return BadRequest(new { error = "Cannot delete submission after the deadline has passed" });
        }

        // Delete submission (entries will cascade delete)
        _context.AvailabilitySubmissions.Remove(submission);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Submission deleted successfully" });
    }

    private async Task<string?> ValidateEntriesAsync(List<AvailabilityEntryDto> entries)
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

                // Validate minimum duration (from settings)
                var minDurationMinutes = await GetMinDurationMinutesAsync();
                var duration = endTime - startTime;
                if (duration.TotalMinutes < minDurationMinutes)
                {
                    var hours = minDurationMinutes / 60.0;
                    return $"TimeRange duration must be at least {minDurationMinutes} minutes ({hours} hours)";
                }

                // Validate time window (from settings)
                var (allowedStartTime, allowedEndTime) = await GetAllowedTimeWindowAsync();
                if (startTime < allowedStartTime)
                {
                    return $"StartTime ({startTime:HH:mm}) must be at or after the allowed start time ({allowedStartTime:HH:mm})";
                }
                if (endTime > allowedEndTime)
                {
                    return $"EndTime ({endTime:HH:mm}) must be at or before the allowed end time ({allowedEndTime:HH:mm})";
                }
            }
        }

        return null;
    }
}
