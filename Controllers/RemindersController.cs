using CustomerExcelApi.Data;
using CustomerExcelApi.Entities;
using CustomerExcelApi.Features.Reminders.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerExcelApi.Controllers;

[ApiController]
[Route("api/reminders")]
public sealed class RemindersController : ControllerBase
{
    private readonly AppDbContext _db;

    public RemindersController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateReminderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var firstReminderTime = request.MeetingTime.AddMinutes(-request.NotifyBeforeMinutes);

        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            Message = request.Message,
            MeetingTime = request.MeetingTime,
            NextReminderTime = firstReminderTime,
            NotifyBeforeMinutes = request.NotifyBeforeMinutes,
            RepeatEveryMinutes = request.RepeatEveryMinutes,
            RetryCount = 0,
            MaxRetryCount = request.MaxRetryCount,
            Status = ReminderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Reminders.Add(reminder);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = reminder.Id }, MapToResponse(reminder));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var reminder = await _db.Reminders
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);

        if (reminder is null) return NotFound();

        return Ok(MapToResponse(reminder));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var query = _db.Reminders
            .AsNoTracking()
            .Where(r => r.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReminderStatus>(status, true, out var parsed))
        {
            query = query.Where(r => r.Status == parsed);
        }

        var reminders = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(new ReminderListResponse
        {
            Reminders = reminders.Select(MapToResponse).ToList(),
            TotalCount = reminders.Count
        });
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var reminder = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);

        if (reminder is null) return NotFound();

        if (reminder.Status != ReminderStatus.Pending)
            return BadRequest(new { error = $"Reminder is already {reminder.Status}" });

        reminder.Status = ReminderStatus.Read;
        reminder.ReadAt = DateTime.UtcNow;
        reminder.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(MapToResponse(reminder));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var reminder = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);

        if (reminder is null) return NotFound();

        if (reminder.Status != ReminderStatus.Pending)
            return BadRequest(new { error = $"Reminder is already {reminder.Status}" });

        reminder.Status = ReminderStatus.Cancelled;
        reminder.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private Guid GetUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userId) &&
            Guid.TryParse(userId, out var id))
            return id;

        return Guid.Empty;
    }

    private static ReminderResponse MapToResponse(Reminder r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Message = r.Message,
        MeetingTime = r.MeetingTime,
        NextReminderTime = r.NextReminderTime,
        RetryCount = r.RetryCount,
        MaxRetryCount = r.MaxRetryCount,
        Status = r.Status.ToString(),
        CreatedAt = r.CreatedAt,
        ReadAt = r.ReadAt
    };
}
