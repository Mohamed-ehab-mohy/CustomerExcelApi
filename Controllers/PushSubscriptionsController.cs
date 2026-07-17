using System.Text.Json;
using CustomerExcelApi.Data;
using CustomerExcelApi.Entities;
using CustomerExcelApi.Services.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerExcelApi.Controllers;

[ApiController]
[Route("api/push-subscriptions")]
public sealed class PushSubscriptionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;

    public PushSubscriptionsController(AppDbContext db, INotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe(
        [FromBody] PushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == request.Endpoint, cancellationToken);

        if (existing is not null)
        {
            existing.P256dhKey = request.Keys.P256dh;
            existing.AuthKey = request.Keys.Auth;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        var subscription = new PushSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Endpoint = request.Endpoint,
            P256dhKey = request.Keys.P256dh,
            AuthKey = request.Keys.Auth,
            CreatedAt = DateTime.UtcNow
        };

        _db.PushSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok();
    }

    [HttpDelete]
    public async Task<IActionResult> Unsubscribe(
        [FromQuery] string endpoint,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var subscription = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, cancellationToken);

        if (subscription is null) return NotFound();

        _db.PushSubscriptions.Remove(subscription);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetSubscriptions(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var subscriptions = await _db.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Id, s.Endpoint, s.CreatedAt })
            .ToListAsync(cancellationToken);

        return Ok(subscriptions);
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestPush(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var hasSubscription = await _db.PushSubscriptions
            .AnyAsync(s => s.UserId == userId, cancellationToken);

        if (!hasSubscription)
            return BadRequest(new { error = "No push subscription found. Subscribe first." });

        await _notificationService.SendAsync(new NotificationMessage
        {
            UserId = userId,
            Title = "Test Notification",
            Body = "If you see this, WebPush is working!",
            ReminderId = Guid.NewGuid(),
            MeetingTime = DateTime.UtcNow
        }, cancellationToken);

        return Ok(new { message = "Test notification sent" });
    }

    private Guid GetUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userId) &&
            Guid.TryParse(userId, out var id))
            return id;

        return Guid.Empty;
    }
}

public sealed class PushSubscriptionRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public PushSubscriptionKeys Keys { get; set; } = new();
}

public sealed class PushSubscriptionKeys
{
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}
