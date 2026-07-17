using CustomerExcelApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CustomerExcelApi.Services.Notifications;

public sealed class SignalRNotificationProvider : INotificationProvider
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationProvider> _logger;

    public SignalRNotificationProvider(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationProvider> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                type = "reminder",
                reminderId = message.ReminderId,
                title = message.Title,
                body = message.Body,
                meetingTime = message.MeetingTime
            };

            await _hubContext.Clients
                .Group($"user-{message.UserId}")
                .SendAsync("ReminderNotification", payload, cancellationToken);

            _logger.LogInformation(
                "SignalR → User {UserId}: {Title} (Reminder {ReminderId})",
                message.UserId, message.Title, message.ReminderId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed for User {UserId}, Reminder {ReminderId}", message.UserId, message.ReminderId);
            return false;
        }
    }
}
