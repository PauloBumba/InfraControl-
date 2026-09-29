using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace InfraControl.Api.Hubs;

/// <summary>
/// Identidade do SignalR = claim <c>ClaimTypes.NameIdentifier</c> emitida
/// no login (F9-T004) — é o que permite o grupo <c>user:{id}</c>.
/// </summary>
public sealed class SignalRUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
