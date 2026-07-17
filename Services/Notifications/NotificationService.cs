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
        try
        {
            var sent = await _signalR.SendAsync(message, cancellationToken);
            if (sent) return;

            _logger.LogDebug("SignalR failed for User {UserId}, falling back to WebPush", message.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed for User {UserId}, falling back to WebPush", message.UserId);
        }

        try
        {
            await _webPush.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Both SignalR and WebPush failed for User {UserId}", message.UserId);
        }
    }
}
