namespace InfraControl.Application.Integrations.Zabbix.Ports;

/// <summary>
/// F27 Fase 2.1: cliente de SAÍDA para a API JSON-RPC 2.0 do Zabbix.
/// Autenticação por <c>user.login</c> com cache de sessão (Bearer no
/// Zabbix ≥ 8, campo <c>auth</c> ≤ 7 — a F27 §1.2.1 confirmou que o 8.0
/// rejeita o campo <c>auth</c>). Todas as listagens aceitam limite
/// aplicado no servidor (paginação por <c>limit</c>); o histórico de um
/// item exige período e volume (Fase 2.1). Tokens/credenciais nunca
/// aparecem em exceção ou log.
/// </summary>
public interface IZabbixClient
{
    bool IsConfigured { get; }

    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZabbixHostDto>> ListHostsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZabbixHostGroupDto>> ListHostGroupsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZabbixTemplateDto>> ListTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZabbixItemDto>> ListItemsAsync(
        string hostId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZabbixTriggerDto>> ListTriggersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZabbixProblemDto>> ListProblemsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZabbixHistoryValueDto>> GetHistoryAsync(
        string itemId,
        DateTime fromUtc,
        DateTime toUtc,
        int limit,
        CancellationToken cancellationToken = default);
}
