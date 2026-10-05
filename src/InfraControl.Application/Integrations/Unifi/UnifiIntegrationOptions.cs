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

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);
}
