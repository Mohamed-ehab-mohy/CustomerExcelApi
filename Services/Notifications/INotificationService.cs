namespace CustomerExcelApi.Services.Notifications;

public sealed class NotificationMessage
{
    public Guid UserId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public Guid ReminderId { get; init; }
    public DateTime MeetingTime { get; init; }
}

public interface INotificationProvider
{
    Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
