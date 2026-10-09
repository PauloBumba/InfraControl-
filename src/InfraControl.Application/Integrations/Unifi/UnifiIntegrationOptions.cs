namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// F20-T001/ADR-028 §2: cliente REST de saída para o UniFi Controller.
/// Valores vazios (default) = integração desligada — credenciais vêm só de
/// env (nunca reais no repositório) e vivem na camada de Infrastructure.
/// </summary>
public sealed class UnifiIntegrationOptions
{
    public const string SectionName = "Integrations:Unifi";

    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Site do Controller (default do UniFi: "default").</summary>
    public string Site { get; set; } = "default";

    /// <summary>
    /// Fase 1 (F26-T001): aceita certificado TLS inválido (self-signed —
    /// docs/35 §2). Default <c>false</c> = validação estrita do runtime;
    /// ligar só para controlador local de teste via env
    /// <c>UNIFI_ALLOW_INVALID_TLS</c>.
    /// </summary>
    public bool AllowInvalidTlsCertificate { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);
}
