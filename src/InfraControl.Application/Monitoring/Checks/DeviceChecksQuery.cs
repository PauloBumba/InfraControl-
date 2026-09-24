using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.Monitoring.Checks;

/// <summary>Um check ativo do device (F5-T001).</summary>
public sealed record MonitoringCheckDto(Guid Id, string Check, bool Enabled);

/// <summary>Checks monitoráveis de um device, vindos do DeviceProfile.</summary>
public sealed record DeviceChecksDto(
    Guid DeviceId,
    string DeviceName,
    IReadOnlyList<MonitoringCheckDto> Checks);

/// <summary>Lista os MonitoringCheck de um device (F5-T001).</summary>
public sealed record GetDeviceChecksQuery(Guid Id)
    : IQuery<DeviceChecksDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.MonitoringRead.ToString()];
}

public sealed class GetDeviceChecksQueryHandler(
    IDeviceRepository deviceRepository,
    IMonitoringCheckRepository checkRepository)
    : IRequestHandler<GetDeviceChecksQuery, DeviceChecksDto>
{
    public async Task<DeviceChecksDto> Handle(
        GetDeviceChecksQuery request,
        CancellationToken cancellationToken)
    {
        var device = await deviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.Id}' não encontrado.");

        var checks = await checkRepository.GetByDeviceAsync(device.Id, cancellationToken);

        return new DeviceChecksDto(
            device.Id,
            device.Name,
            checks
                .Select(check => new MonitoringCheckDto(
                    check.Id, check.Kind.ToString(), check.Enabled))
                .ToArray());
    }
}
