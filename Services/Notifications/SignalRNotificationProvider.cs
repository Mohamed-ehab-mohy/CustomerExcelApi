using Microsoft.Extensions.Logging;

namespace CustomerExcelApi.Services.Notifications;

public sealed class SignalRNotificationProvider : INotificationProvider
{
    private readonly ILogger<SignalRNotificationProvider> _logger;

    public SignalRNotificationProvider(ILogger<SignalRNotificationProvider> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SignalR → User {UserId}: {Title} (Reminder {ReminderId})",
            message.UserId, message.Title, message.ReminderId);

        return Task.FromResult(true);
    }
}
