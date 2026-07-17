using System.Text.Json;
using CustomerExcelApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerExcelApi.Services.Notifications;

public sealed class WebPushNotificationProvider : INotificationProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebPushNotificationProvider> _logger;
    private readonly string _vapidPublicKey;
    private readonly string _vapidPrivateKey;
    private readonly string _vapidSubject;

    public WebPushNotificationProvider(
        IServiceScopeFactory scopeFactory,
        ILogger<WebPushNotificationProvider> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _vapidPublicKey = configuration["WebPush:VapidPublicKey"] ?? string.Empty;
        _vapidPrivateKey = configuration["WebPush:VapidPrivateKey"] ?? string.Empty;
        _vapidSubject = configuration["WebPush:VapidSubject"] ?? "mailto:admin@example.com";
    }

    public async Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_vapidPublicKey) || string.IsNullOrEmpty(_vapidPrivateKey))
        {
            _logger.LogWarning("WebPush VAPID keys not configured, skipping WebPush for User {UserId}", message.UserId);
            return false;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var subscriptions = await db.PushSubscriptions
                .Where(s => s.UserId == message.UserId)
                .ToListAsync(cancellationToken);

            if (subscriptions.Count == 0)
            {
                _logger.LogDebug("No push subscriptions for User {UserId}", message.UserId);
                return false;
            }

            var payload = JsonSerializer.Serialize(new
            {
                type = "reminder",
                reminderId = message.ReminderId,
                title = message.Title,
                body = message.Body,
                meetingTime = message.MeetingTime
            });

            var vapidDetails = new WebPush.VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);
            var sentCount = 0;
            var removedCount = 0;

            foreach (var sub in subscriptions)
            {
                try
                {
                    var pushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dhKey, sub.AuthKey);
                    using var client = new WebPush.WebPushClient();
                    await client.SendNotificationAsync(pushSubscription, payload, vapidDetails, cancellationToken);
                    sentCount++;
                }
                catch (WebPush.WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    _logger.LogInformation("Removing expired push subscription {Id}", sub.Id);
                    db.PushSubscriptions.Remove(sub);
                    removedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WebPush failed for subscription {Id}", sub.Id);
                }
            }

            if (removedCount > 0 || sentCount > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "WebPush → User {UserId}: sent {Count}/{Total}, removed {Removed} (Reminder {ReminderId})",
                message.UserId, sentCount, subscriptions.Count, removedCount, message.ReminderId);

            return sentCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebPush provider error for User {UserId}, Reminder {ReminderId}", message.UserId, message.ReminderId);
            return false;
        }
    }
}
