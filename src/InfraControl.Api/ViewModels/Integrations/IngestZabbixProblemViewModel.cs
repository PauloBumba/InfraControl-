namespace InfraControl.Api.ViewModels.Integrations;

/// <summary>
/// F16-T001/ADR-023: payload do action do Zabbix (macros em docs/27).
/// Validação de negócio (name/severity/status) é do handler — aqui só a
/// forma do JSON; JSON malformado cai no binding 400 do MVC (padrão da casa).
/// </summary>
public sealed class IngestZabbixProblemViewModel
{
    public string? EventId { get; init; }
    public string? Host { get; init; }
    public string? Ip { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? Severity { get; init; }
    public string Status { get; init; } = string.Empty;
}
