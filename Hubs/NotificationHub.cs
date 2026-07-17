using Microsoft.AspNetCore.SignalR;

namespace CustomerExcelApi.Hubs;

public sealed class NotificationHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }

    public override async Task OnConnectedAsync()
    {
        if (Context.GetHttpContext()?.Request.Query.TryGetValue("userId", out var userId) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }

        await base.OnConnectedAsync();
    }
}
