using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// F26 Fase 8: dashboard de infraestrutura UniFi. Persistido (sites,
/// switches/APs, vínculos, audit) × ao vivo (estados, clientes, portas,
/// alcance do controller) fica EXPLICITAMENTE separado no DTO — zero
/// valores demonstrativos; erro ≠ zero (8.2/8.3).
/// </summary>
public sealed record UnifiDashboardDto(
    int Sites,
    int SitesMissing,
    int Switches,
    int AccessPoints,
    IReadOnlyDictionary<string, int> DeviceStates,
    int Clients,
    UnifiPortsSummaryDto? Ports,
    int Unlinked,
    DateTime? LastSyncAtUtc,
    bool? LastSyncSucceeded,
    int RecentSyncErrors,
    bool ControllerReachable,
    string? ControllerError,
    bool Stale);

public sealed record GetUnifiDashboardQuery : IQuery<UnifiDashboardDto>
{
}

public sealed class GetUnifiDashboardQueryHandler(
    IUniFiSitesRepository sitesRepository,
    IDeviceRepository deviceRepository,
    IExternalDeviceReferenceRepository referenceRepository,
    IUnifiApiClient unifiApiClient,
    IAuditReader auditReader,
    ISender sender)
    : IRequestHandler<GetUnifiDashboardQuery, UnifiDashboardDto>
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    public async Task<UnifiDashboardDto> Handle(
        GetUnifiDashboardQuery request, CancellationToken cancellationToken)
    {
        var sites = await sitesRepository.ListAsync(cancellationToken);
        var devices = await deviceRepository.GetAllOrderedByNameAsync(cancellationToken);

        var switches = devices.Count(d => d.Type == DeviceType.Switch);
        var accessPoints = devices.Count(d => d.Type == DeviceType.AccessPoint);

        var correlation = await sender.Send(new GetUniFiCorrelationQuery(), cancellationToken);
        var unlinked = correlation.Unlinked.Count;

        var succeeded = await auditReader.SearchAsync(
            "SyncUnifiInventoryCommand", "Succeeded", null, null, 1, 1, cancellationToken);
        var failed = await auditReader.SearchAsync(
            "SyncUnifiInventoryCommand", "Failed", null, null, 1, 1, cancellationToken);
        var lastSucceeded = succeeded.Items.FirstOrDefault();
        var lastFailed = failed.Items.FirstOrDefault();
        var lastSync = lastFailed is not null &&
                       (lastSucceeded is null || lastFailed.OccurredAt > lastSucceeded.OccurredAt)
            ? lastFailed
            : lastSucceeded;
        bool? lastSyncSucceeded = lastSync is null ? null : lastSync == lastSucceeded;

        var errors = await auditReader.SearchAsync(
            "SyncUnifiInventoryCommand", "Failed",
            DateTime.UtcNow.AddDays(-7), null, 1, 100, cancellationToken);

        string? controllerError = null;
        IReadOnlyDictionary<string, int> states = new Dictionary<string, int>();
        var clients = 0;
        UnifiPortsSummaryDto? ports = null;
        try
        {
            var liveDevices = await unifiApiClient.ListDevicesAsync(cancellationToken);
            states = liveDevices
                .GroupBy(d => string.IsNullOrWhiteSpace(d.State) ? "desconhecido" : d.State)
                .ToDictionary(g => g.Key, g => g.Count());
            clients = (await unifiApiClient.ListConnectedClientsAsync(cancellationToken)).Count;
            ports = await unifiApiClient.GetPortsSummaryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            controllerError = exception.Message;
        }

        var lastSyncAt = lastSync?.OccurredAt;
        var stale = lastSyncAt is null ||
                    DateTime.UtcNow - lastSyncAt.Value > StaleAfter;

        return new UnifiDashboardDto(
            Sites: sites.Count(s => s.MissingSinceUtc is null),
            SitesMissing: sites.Count(s => s.MissingSinceUtc is not null),
            Switches: switches,
            AccessPoints: accessPoints,
            DeviceStates: states,
            Clients: clients,
            Ports: ports,
            Unlinked: unlinked,
            LastSyncAtUtc: lastSyncAt,
            LastSyncSucceeded: lastSyncSucceeded,
            RecentSyncErrors: errors.TotalCount,
            ControllerReachable: controllerError is null,
            ControllerError: controllerError,
            Stale: stale);
    }
}
