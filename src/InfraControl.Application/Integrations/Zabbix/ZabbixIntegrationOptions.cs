namespace InfraControl.Application.Integrations.Zabbix;

/// <summary>
/// F16-T001/ADR-023: webhook de entrada do Zabbix. Token vazio (default) =
/// integração desligada (o endpoint responde 404); a comparação é ordinal e
/// acontece no controller — é a única autenticação do endpoint público.
/// </summary>
public sealed class ZabbixIntegrationOptions
{
    public const string SectionName = "Integrations:Zabbix";
    public const string TokenHeaderName = "X-Zabbix-Token";

    public string Token { get; set; } = string.Empty;
}
