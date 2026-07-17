using Microsoft.Extensions.Logging;

namespace CustomerExcelApi.Services.Notifications;

public sealed class WebPushNotificationProvider : INotificationProvider
{
    private readonly ILogger<WebPushNotificationProvider> _logger;

    public WebPushNotificationProvider(ILogger<WebPushNotificationProvider> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "WebPush → User {UserId}: {Title} (Reminder {ReminderId})",
            message.UserId, message.Title, message.ReminderId);

        return Task.FromResult(true);
    }
}
