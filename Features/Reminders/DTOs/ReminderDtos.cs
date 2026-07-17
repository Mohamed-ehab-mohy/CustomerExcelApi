namespace CustomerExcelApi.Features.Reminders.DTOs;

public sealed record CreateReminderRequest
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime MeetingTime { get; init; }
    public int NotifyBeforeMinutes { get; init; } = 10;
    public int RepeatEveryMinutes { get; init; } = 5;
    public int MaxRetryCount { get; init; } = 12;
}

public sealed record ReminderResponse
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime MeetingTime { get; init; }
    public DateTime NextReminderTime { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetryCount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public sealed record ReminderListResponse
{
    public IReadOnlyList<ReminderResponse> Reminders { get; init; } = Array.Empty<ReminderResponse>();
    public int TotalCount { get; init; }
}
