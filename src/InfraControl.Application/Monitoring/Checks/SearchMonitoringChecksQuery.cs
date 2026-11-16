using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Monitoring.Checks;

/// <summary>
/// ADR-068: lista TODOS os monitoring checks com nome do device — fonte do
/// multi-seleção do formulário de SLO ("o que este SLO mede"). Inclui
/// checks desabilitadas (o operador decide; check desligada só para de
/// gerar polls novos).
/// </summary>
public sealed record SearchMonitoringChecksQuery(string? Term = null)
    : IQuery<IReadOnlyList<MonitoringCheckOptionDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed record MonitoringCheckOptionDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string DeviceIp,
    CheckKind Kind,
    bool Enabled);

public sealed class SearchMonitoringChecksQueryHandler(
    IMonitoringCheckRepository checkRepository,
    IDeviceRepository deviceRepository)
    : IRequestHandler<SearchMonitoringChecksQuery, IReadOnlyList<MonitoringCheckOptionDto>>
{
    public async Task<IReadOnlyList<MonitoringCheckOptionDto>> Handle(
        SearchMonitoringChecksQuery request, CancellationToken cancellationToken)
    {
        var checks = await checkRepository.GetAllAsync(cancellationToken);
        var devices = await deviceRepository.GetAllOrderedByNameAsync(cancellationToken);
        var deviceById = devices.ToDictionary(d => d.Id);

        var term = request.Term?.Trim();
        return checks
            .Select(check =>
            {
                deviceById.TryGetValue(check.DeviceId, out var device);
                return new MonitoringCheckOptionDto(
                    check.Id,
                    check.DeviceId,
                    device?.Name ?? "—",
                    device?.Ip ?? string.Empty,
                    check.Kind,
                    check.Enabled);
            })
            .Where(option =>
                string.IsNullOrEmpty(term)
                || option.DeviceName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || option.DeviceIp.Contains(term, StringComparison.OrdinalIgnoreCase)
                || option.Kind.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
