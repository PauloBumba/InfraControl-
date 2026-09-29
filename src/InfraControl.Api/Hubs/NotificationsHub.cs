using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InfraControl.Api.Hubs;

/// <summary>
/// Hub de notificações em tempo real (F9-T004, docs/07 canal SignalR).
/// Cada usuário entra no grupo <c>user:{userId}</c> — o worker de
/// deliveries envia para o grupo do destinatário.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId}";

    public static string UserGroup(string userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        await base.OnConnectedAsync();
    }
}
