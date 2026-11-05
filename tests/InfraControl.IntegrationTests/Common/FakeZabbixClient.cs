using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// IZabbixClient não configurado para integration tests — o
/// ZabbixEventContextReader encurta para null sem sair para a API.
/// </summary>
public sealed class FakeZabbixClient : IZabbixClient
{
    public bool IsConfigured => false;

    public Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult("0.0.0-test");

    public Task<IReadOnlyList<ZabbixHostDto>> ListHostsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ZabbixHostDto>>([]);

    public Task<IReadOnlyList<ZabbixHostGroupDto>> ListHostGroupsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ZabbixHostGroupDto>>([]);

    public Task<IReadOnlyList<ZabbixTemplateDto>> ListTemplatesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ZabbixTemplateDto>>([]);

    public Task<IReadOnlyList<ZabbixItemDto>> ListItemsAsync(
        string hostId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ZabbixItemDto>>([]);

    public Task<IReadOnlyList<ZabbixTriggerDto>> ListTriggersAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ZabbixTriggerDto>>([]);

    public Task<IReadOnlyList<ZabbixProblemDto>> ListProblemsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ZabbixProblemDto>>([]);

    public Task<ZabbixEventContextDto?> GetEventContextAsync(
        string eventId, CancellationToken cancellationToken = default)
        => Task.FromResult<ZabbixEventContextDto?>(null);

    public Task<IReadOnlyList<ZabbixHistoryValueDto>> GetHistoryAsync(
        string itemId, DateTime fromUtc, DateTime toUtc, int limit,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ZabbixHistoryValueDto>>([]);
}
