namespace InfraControl.Application.Integrations.Zabbix;

/// <summary>
/// F16-T001/ADR-023: webhook de entrada do Zabbix. Token vazio (default) =
/// integração desligada (o endpoint responde 404); a comparação é ordinal e
/// acontece no controller — é a única autenticação do endpoint público.
/// F27 Fase 2.1/2.2: a MESMA seção também carrega as credenciais de SAÍDA
/// para a API do Zabbix (ApiUrl/Username/Password) — fallback de env da
/// configuração administrativa persistida (banco → env, padrão UniFi).
/// </summary>
public sealed class ZabbixIntegrationOptions
{
    public const string SectionName = "Integrations:Zabbix";
    public const string TokenHeaderName = "X-Zabbix-Token";

    public string Token { get; set; } = string.Empty;

    /// <summary>URL da API JSON-RPC (ex.: http://zabbix-web:8080).</summary>
    public string ApiUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool IsApiConfigured =>
        !string.IsNullOrWhiteSpace(ApiUrl) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);
}
