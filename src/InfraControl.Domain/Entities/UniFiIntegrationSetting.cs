using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F26 Fase 2.2: configuração administrativa da integração UniFi
/// persistida no InfraControl (linha única — singleton). A senha vive
/// cifrada (ICredentialProtector/AES-GCM, F11-T006) e nunca aparece em
/// DTO, log ou resposta. Enquanto não existir linha, a integração usa a
/// configuração de env (IOptions/Integrations:Unifi) — fallback da F20.
/// REGRA 8 intacta: isto é CONFIG, não telemetria.
/// </summary>
public sealed class UniFiIntegrationSetting : Entity
{
    public const int MaxLength = 512;

    public string BaseUrl { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string PasswordEncrypted { get; private set; } = string.Empty;
    public string Site { get; private set; } = "default";
    public bool AllowInvalidTlsCertificate { get; private set; }

    private UniFiIntegrationSetting() { }

    public static UniFiIntegrationSetting Create(
        string baseUrl,
        string username,
        string passwordEncrypted,
        string site,
        bool allowInvalidTlsCertificate)
    {
        var setting = new UniFiIntegrationSetting();
        setting.Update(baseUrl, username, passwordEncrypted, site, allowInvalidTlsCertificate);
        return setting;
    }

    /// <summary>
    /// Atualiza os campos. <paramref name="passwordEncrypted"/> nulo ou
    /// vazio = mantém a senha já cifrada (rotatória opcional).
    /// </summary>
    public void Update(
        string baseUrl,
        string username,
        string? passwordEncrypted,
        string site,
        bool allowInvalidTlsCertificate)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException(
                "A URL do controlador deve ser absoluta (http/https).",
                nameof(baseUrl));

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException(
                "O usuário do controlador é obrigatório.",
                nameof(username));

        if (username.Length > MaxLength)
            throw new ArgumentException(
                $"O usuário pode ter no máximo {MaxLength} caracteres.",
                nameof(username));

        var siteTrimmed = string.IsNullOrWhiteSpace(site) ? "default" : site.Trim();
        if (siteTrimmed.Length > MaxLength)
            throw new ArgumentException(
                $"O site pode ter no máximo {MaxLength} caracteres.",
                nameof(site));

        BaseUrl = uri.ToString().TrimEnd('/');
        Username = username.Trim();

        if (!string.IsNullOrEmpty(passwordEncrypted))
        {
            if (passwordEncrypted.Length > MaxLength * 4)
                throw new ArgumentException(
                    "A senha cifrada excede o tamanho permitido.",
                    nameof(passwordEncrypted));
            PasswordEncrypted = passwordEncrypted;
        }

        Site = siteTrimmed;
        AllowInvalidTlsCertificate = allowInvalidTlsCertificate;
        Touch();
    }
}
