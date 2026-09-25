using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.RemoteAccess.Sessions;

/// <summary>Sessão de acesso remoto registrada (F11-T001).</summary>
public sealed record RemoteSessionDto(
    Guid Id,
    Guid UserId,
    string Protocol,
    string Status,
    string? FailureReason,
    DateTime StartedAt,
    DateTime? EndedAt);

/// <summary>Histórico de sessões de um device.</summary>
public sealed record DeviceSessionsDto(
    Guid DeviceId,
    string DeviceName,
    IReadOnlyList<RemoteSessionDto> Sessions);

/// <summary>
/// Sessões do device, mais recente primeiro (F11-T001, ADR-013).
/// O DTO não transporta credenciais (docs/10).
/// </summary>
public sealed record GetDeviceSessionsQuery(Guid Id, int Limit = 50)
    : IQuery<DeviceSessionsDto>, IAuthorizedRequest, IAuditableRequest
{
    public const int MinLimit = 1;
    public const int MaxLimit = 500;

    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.RemoteSessionsRead.ToString()];

    public string AuditResource => "RemoteSession";
}

public sealed class GetDeviceSessionsQueryHandler(
    IDeviceRepository deviceRepository,
    IRemoteSessionRepository sessionRepository)
    : IRequestHandler<GetDeviceSessionsQuery, DeviceSessionsDto>
{
    public async Task<DeviceSessionsDto> Handle(
        GetDeviceSessionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Limit is < GetDeviceSessionsQuery.MinLimit or > GetDeviceSessionsQuery.MaxLimit)
            throw new ArgumentException(
                $"O limite deve estar entre {GetDeviceSessionsQuery.MinLimit} e {GetDeviceSessionsQuery.MaxLimit}.",
                nameof(request.Limit));

        var device = await deviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.Id}' não encontrado.");

        var sessions = await sessionRepository.GetRecentByDeviceAsync(
            device.Id, request.Limit, cancellationToken);

        return new DeviceSessionsDto(
            device.Id,
            device.Name,
            sessions
                .Select(session => new RemoteSessionDto(
                    session.Id,
                    session.UserId,
                    session.Protocol.ToString(),
                    session.Status.ToString(),
                    session.FailureReason,
                    session.StartedAt,
                    session.EndedAt))
                .ToArray());
    }
}
