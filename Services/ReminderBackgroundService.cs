using CustomerExcelApi.Data;
using CustomerExcelApi.Entities;
using CustomerExcelApi.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CustomerExcelApi.Services;

public sealed class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderBackgroundService> _logger;
    private readonly TimeSpan _pollingInterval;
    private const int BatchSize = 100;

    public ReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollingInterval = TimeSpan.FromSeconds(30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderBackgroundService started. Polling every {Interval}s", _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reminders, will retry in {Interval}s", _pollingInterval.TotalSeconds);
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("ReminderBackgroundService stopped");
    }

    private async Task ProcessDueRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;

        var dueReminders = await db.Reminders
            .AsNoTracking()
            .Where(r => r.Status == ReminderStatus.Pending && r.NextReminderTime <= now)
            .OrderBy(r => r.NextReminderTime)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (dueReminders.Count == 0) return;

        _logger.LogInformation("Processing {Count} due reminders", dueReminders.Count);

        var reminderIds = dueReminders.Select(r => r.Id).ToList();

        var remindersToUpdate = await db.Reminders
            .Where(r => reminderIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        foreach (var reminder in remindersToUpdate)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var message = new NotificationMessage
            {
                UserId = reminder.UserId,
                Title = reminder.Title,
                Body = $"Meeting at {reminder.MeetingTime:HH:mm} — {reminder.Message}",
                ReminderId = reminder.Id,
                MeetingTime = reminder.MeetingTime
            };

            try
            {
                await notificationService.SendAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification for Reminder {ReminderId}", reminder.Id);
            }

            reminder.RetryCount++;
            reminder.UpdatedAt = now;

            if (reminder.RetryCount >= reminder.MaxRetryCount)
            {
                reminder.Status = ReminderStatus.Expired;
                _logger.LogInformation("Reminder {ReminderId} expired after {RetryCount} retries", reminder.Id, reminder.RetryCount);
            }
            else
            {
                reminder.NextReminderTime = now.AddMinutes(reminder.RepeatEveryMinutes);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Processed {Count} reminders", remindersToUpdate.Count);
    }
}
