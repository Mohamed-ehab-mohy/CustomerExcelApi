namespace CustomerExcelApi.Entities;

public enum ReminderStatus
{
    Pending = 0,
    Read = 1,
    Expired = 2,
    Cancelled = 3
}

public sealed class Reminder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime MeetingTime { get; set; }
    public DateTime NextReminderTime { get; set; }
    public int NotifyBeforeMinutes { get; set; }
    public int RepeatEveryMinutes { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
