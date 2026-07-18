using Microsoft.Extensions.Logging;

namespace CustomerExcelApi.Services.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly SignalRNotificationProvider _signalR;
    private readonly WebPushNotificationProvider _webPush;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        SignalRNotificationProvider signalR,
        WebPushNotificationProvider webPush,
        ILogger<NotificationService> logger)
    {
        _signalR = signalR;
        _webPush = webPush;
        _logger = logger;
    }

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var signalROk = false;
        var webPushOk = false;

        try
        {
            signalROk = await _signalR.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed for User {UserId}", message.UserId);
        }

        try
        {
            webPushOk = await _webPush.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebPush failed for User {UserId}", message.UserId);
        }

        if (!signalROk && !webPushOk)
        {
            _logger.LogError("Both SignalR and WebPush failed for User {UserId}, Reminder {ReminderId}", message.UserId, message.ReminderId);
        }
    }
}
