using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Monitoring.Ping;

/// <summary>Resultado do ping de um device (F5-T003). Up null = sem IP.</summary>
public sealed record PingStatusDto(
    Guid DeviceId,
    string Name,
    string? Ip,
    bool? Up,
    long? LatencyMs,
    string? Error);

public sealed record PingDeviceQuery(Guid Id) : IQuery<PingStatusDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.MonitoringRead.ToString()];
}

public sealed class PingDeviceQueryHandler(
    IDeviceRepository repository,
    IProviderRegistry<IMonitoringProvider> providers)
    : IRequestHandler<PingDeviceQuery, PingStatusDto>
{
    public async Task<PingStatusDto> Handle(PingDeviceQuery request, CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.Id}' não encontrado.");

        return await PingDeviceQueryHandler.PingAsync(device, providers, cancellationToken);
    }

    internal static async Task<PingStatusDto> PingAsync(
        Domain.Entities.Device device,
        IProviderRegistry<IMonitoringProvider> providers,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(device.Ip))
            return new PingStatusDto(device.Id, device.Name, device.Ip, null, null,
                "Device sem IP cadastrado.");

        var provider = providers.Resolve(CheckKind.Ping.ToString());
        var result = await provider.CheckAsync(device.Ip, cancellationToken);

        return new PingStatusDto(
            device.Id,
            device.Name,
            device.Ip,
            result.Succeeded,
            result.Succeeded ? (long)result.Duration.TotalMilliseconds : null,
            result.Succeeded ? null : result.Error ?? "Host inacessível.");
    }
}

/// <summary>Up/down de todos os devices com IP, estilo Zabbix (F5-T003).</summary>
public sealed record GetPingStatusQuery : IQuery<IReadOnlyList<PingStatusDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.MonitoringRead.ToString()];
}

public sealed class GetPingStatusQueryHandler(
    IDeviceRepository repository,
    IProviderRegistry<IMonitoringProvider> providers)
    : IRequestHandler<GetPingStatusQuery, IReadOnlyList<PingStatusDto>>
{
    public async Task<IReadOnlyList<PingStatusDto>> Handle(
        GetPingStatusQuery request,
        CancellationToken cancellationToken)
    {
        var devices = await repository.GetAllOrderedByNameAsync(cancellationToken);
        var results = new List<PingStatusDto>(devices.Count);

        foreach (var device in devices)
            results.Add(await PingDeviceQueryHandler.PingAsync(device, providers, cancellationToken));

        return results;
    }
}
