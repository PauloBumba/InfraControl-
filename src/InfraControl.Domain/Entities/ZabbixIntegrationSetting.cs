using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F27 Fase 2.2: configuração administrativa da integração Zabbix
/// persistida no InfraControl (linha única — singleton). A senha da API
/// vive cifrada (ICredentialProtector/AES-GCM) e nunca aparece em DTO, log
/// ou resposta. Enquanto não existir linha, a integração usa a
/// configuração de env (IOptions/Integrations:Zabbix) — mesmo fallback do
/// UniFi. Os campos de teste/estado alimentam o painel
/// Administração → Integrações → Zabbix (versão detectada, última
/// comunicação e erro recente sem segredos).
/// </summary>
public sealed class ZabbixIntegrationSetting : Entity
{
    public const int MaxLength = 512;
    public const int MaxVersionLength = 32;
    public const int MaxErrorLength = 1000;

    public string ApiUrl { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string PasswordEncrypted { get; private set; } = string.Empty;

    public string? DetectedVersion { get; private set; }
    public DateTime? LastCommunicationAtUtc { get; private set; }
    public DateTime? LastInventorySyncAtUtc { get; private set; }
    public DateTime? LastTestedAtUtc { get; private set; }
    public bool LastTestOk { get; private set; }
    public string? LastTestError { get; private set; }

    private ZabbixIntegrationSetting() { }

    public static ZabbixIntegrationSetting Create(
        string apiUrl,
        string username,
        string passwordEncrypted)
    {
        var setting = new ZabbixIntegrationSetting();
        setting.Update(apiUrl, username, passwordEncrypted);
        return setting;
    }

    /// <summary>
    /// Atualiza os campos de configuração. <paramref name="passwordEncrypted"/>
    /// nulo ou vazio = mantém a senha já cifrada (rotatória opcional).
    /// </summary>
    public void Update(
        string apiUrl,
        string username,
        string? passwordEncrypted)
    {
        if (!Uri.TryCreate(apiUrl?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException(
                "A URL da API do Zabbix deve ser absoluta (http/https).",
                nameof(apiUrl));

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException(
                "O usuário da API do Zabbix é obrigatório.",
                nameof(username));

        if (username.Length > MaxLength)
            throw new ArgumentException(
                $"O usuário pode ter no máximo {MaxLength} caracteres.",
                nameof(username));

        ApiUrl = uri.ToString().TrimEnd('/');
        Username = username.Trim();

        if (!string.IsNullOrEmpty(passwordEncrypted))
        {
            if (passwordEncrypted.Length > MaxLength * 4)
                throw new ArgumentException(
                    "A senha cifrada excede o tamanho permitido.",
                    nameof(passwordEncrypted));
            PasswordEncrypted = passwordEncrypted;
        }

        Touch();
    }

    /// <summary>
    /// Registra uma sincronização de inventário bem-sucedida (F27 Fase 3.1
    /// — o painel exibe a última sync conforme §2.2).
    /// </summary>
    public void RecordInventorySync(DateTime syncedAtUtc)
    {
        LastInventorySyncAtUtc = syncedAtUtc;
        LastCommunicationAtUtc = syncedAtUtc;
        Touch();
    }

    /// <summary>
    /// Registra o resultado de um teste de conexão (F27 Fase 2.2 — painel
    /// exibe versão detectada, estado e última comunicação). Sucesso
    /// atualiza a versão e a última comunicação; falha preserva a versão
    /// anterior e grava o erro recente (truncado, sem segredos).
    /// </summary>
    public void RecordTestResult(
        bool ok,
        string? detectedVersion,
        DateTime testedAtUtc,
        string? error)
    {
        LastTestedAtUtc = testedAtUtc;
        LastTestOk = ok;

        if (ok)
        {
            LastCommunicationAtUtc = testedAtUtc;
            LastTestError = null;

            if (!string.IsNullOrWhiteSpace(detectedVersion))
            {
                DetectedVersion = detectedVersion.Length > MaxVersionLength
                    ? detectedVersion[..MaxVersionLength]
                    : detectedVersion;
            }
        }
        else
        {
            var trimmed = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
            if (trimmed is not null && trimmed.Length > MaxErrorLength)
                trimmed = trimmed[..MaxErrorLength];
            LastTestError = trimmed;
        }

        Touch();
    }
}
