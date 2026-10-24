namespace InfraControl.Application.Integrations.Zabbix;

/// <summary>Interface de um host no Zabbix (F27 Fase 2.1/3.1).</summary>
public sealed record ZabbixInterfaceDto(
    string Ip,
    int Port,
    int Type,
    bool Main);

/// <summary>Host do Zabbix mapeado para o contrato interno (Fase 3.1).</summary>
public sealed record ZabbixHostDto(
    string HostId,
    string Host,
    string Name,
    bool Enabled,
    bool Discovered,
    IReadOnlyList<ZabbixInterfaceDto> Interfaces,
    IReadOnlyList<string> GroupIds,
    IReadOnlyList<string> TemplateIds);

public sealed record ZabbixHostGroupDto(string GroupId, string Name);

public sealed record ZabbixTemplateDto(string TemplateId, string Name);

/// <summary>
/// Problema aberto/fechado do Zabbix (problem.get). <c>ObjectId</c> é o
/// triggerid (dedup de correlação com trigger.get); <c>HostNames</c> vem
/// do join via trigger.get — o problem.get do Zabbix 8.0 REMOVEU
/// <c>selectHosts</c> (confirmado ao vivo, F27 Fase 10.1).
/// </summary>
public sealed record ZabbixProblemDto(
    string EventId,
    string Name,
    int Severity,
    DateTime ClockUtc,
    string? RecoveryEventId,
    string? ObjectId = null,
    IReadOnlyList<string>? HostNames = null);

public sealed record ZabbixEventContextDto(
    string? TriggerId,
    IReadOnlyList<string> HostIds,
    IReadOnlyList<string> HostGroupIds,
    IReadOnlyList<ZabbixEventTagDto> Tags);

public sealed record ZabbixEventTagDto(string Tag, string Value);

public sealed record ZabbixItemDto(
    string ItemId,
    string HostId,
    string Name,
    string Key,
    int Type,
    bool Enabled,
    bool Unsupported,
    string Unit = "");

public sealed record ZabbixTriggerDto(
    string TriggerId,
    string Description,
    int Priority,
    bool Enabled,
    bool Value);

/// <summary>Valor histórico de um item (history.get) com limite aplicado.</summary>
public sealed record ZabbixHistoryValueDto(
    string ItemId,
    DateTime ClockUtc,
    string Value);
