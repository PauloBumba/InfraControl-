using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// ADR-062: configuração administrativa do canal WhatsApp (Evolution API)
/// persistida no InfraControl (linha única — singleton). A ApiKey vive
/// cifrada (ICredentialProtector/AES-GCM) e nunca aparece em DTO, log ou
/// resposta. O <see cref="GroupId"/> é o destino escolhido na UI (grupo
/// @g.us) e é a fonte da verdade do broadcast (ADR-059 §3). Enquanto não
/// existir linha, o canal usa a config de env (Notifications:Channels:WhatsApp)
/// — mesmo fallback do Zabbix/UniFi. Ativação nullable mantém o fanout de env
/// nas instalações legadas até escolha explícita na UI. Campos de estado
/// alimentam a tela de Integração → WhatsApp (connection_state, último teste).
/// </summary>
public sealed class WhatsAppIntegrationSetting : Entity
{
    public const int MaxLength = 512;
    public const int MaxApiKeyLength = 2048;
    public const int MaxInstanceIdLength = 128;
    public const int MaxGroupIdLength = 128;
    public const int MaxPhoneLength = 64;
    public const int MaxStateLength = 32;
    public const int MaxErrorLength = 1000;

    public string BaseUrl { get; private set; } = string.Empty;
    public string ApiKeyEncrypted { get; private set; } = string.Empty;
    public string InstanceId { get; private set; } = string.Empty;
    public string? GroupId { get; private set; }
    public string? Phone { get; private set; }
    public bool? NotificationsEnabled { get; private set; }
    public string? ConnectionState { get; private set; }
    public DateTime? LastTestedAtUtc { get; private set; }
    public bool LastTestOk { get; private set; }
    public string? LastTestError { get; private set; }

    // ADR-069: cartão visual (PNG) — toggle por tipo, janela do gráfico e
    // cores por tipo (null = paleta semântica). Defaults = comportamento F33.
    public bool CardIncidentEnabled { get; private set; } = true;
    public bool CardWarningEnabled { get; private set; } = true;
    public bool CardResolutionEnabled { get; private set; } = true;
    public int CardWindowHours { get; private set; } = 2;
    public string? CardIncidentColor { get; private set; }
    public string? CardWarningColor { get; private set; }
    public string? CardResolutionColor { get; private set; }

    private WhatsAppIntegrationSetting() { }

    public static WhatsAppIntegrationSetting Create(
        string baseUrl,
        string apiKeyEncrypted,
        string instanceId,
        string? groupId = null,
        string? phone = null,
        bool? notificationsEnabled = null)
    {
        var setting = new WhatsAppIntegrationSetting();
        setting.Update(baseUrl, apiKeyEncrypted, instanceId, groupId, phone, notificationsEnabled);
        return setting;
    }

    /// <summary>
    /// Atualiza os campos de configuração. <paramref name="apiKeyEncrypted"/>
    /// nulo/vazio = mantém a ApiKey já cifrada (rotatória opcional);
    /// <paramref name="groupId"/>/<paramref name="phone"/> nulos = limpa
    /// (para trocar de destino explicitamente envie valor ou vazio).
    /// </summary>
    public void Update(
        string baseUrl,
        string? apiKeyEncrypted,
        string instanceId,
        string? groupId = null,
        string? phone = null,
        bool? notificationsEnabled = null)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException(
                "A URL da Evolution API deve ser absoluta (http/https).",
                nameof(baseUrl));

        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException(
                "O nome da instância WhatsApp é obrigatório.",
                nameof(instanceId));

        if (instanceId.Length > MaxInstanceIdLength)
            throw new ArgumentException(
                $"O nome da instância pode ter no máximo {MaxInstanceIdLength} caracteres.",
                nameof(instanceId));

        BaseUrl = uri.ToString().TrimEnd('/');
        InstanceId = instanceId.Trim();

        if (!string.IsNullOrEmpty(apiKeyEncrypted))
        {
            if (apiKeyEncrypted.Length > MaxApiKeyLength)
                throw new ArgumentException(
                    "A ApiKey cifrada excede o tamanho permitido.",
                    nameof(apiKeyEncrypted));
            ApiKeyEncrypted = apiKeyEncrypted;
        }

        GroupId = NormalizeGroupId(groupId);
        Phone = NormalizePhone(phone);
        NotificationsEnabled = notificationsEnabled;

        Touch();
    }

    /// <summary>
    /// Define/limpa o grupo destino (escolha na UI). Grupo precisa terminar
    /// em @g.us quando preenchido.
    /// </summary>
    public void SetGroupId(string? groupId)
    {
        GroupId = NormalizeGroupId(groupId);
        Touch();
    }

    /// <summary>
    /// ADR-069: atualiza o modelo do cartão visual. Cores nulas/vazias
    /// voltam à paleta semântica; janela fora de 1–24h é rejeitada.
    /// </summary>
    public void UpdateCardSettings(
        bool incidentEnabled,
        bool warningEnabled,
        bool resolutionEnabled,
        int windowHours,
        string? incidentColor,
        string? warningColor,
        string? resolutionColor)
    {
        if (windowHours is < 1 or > 24)
            throw new ArgumentOutOfRangeException(
                nameof(windowHours), "A janela do gráfico deve ser entre 1 e 24 horas.");

        CardIncidentEnabled = incidentEnabled;
        CardWarningEnabled = warningEnabled;
        CardResolutionEnabled = resolutionEnabled;
        CardWindowHours = windowHours;
        CardIncidentColor = NormalizeCardColor(incidentColor);
        CardWarningColor = NormalizeCardColor(warningColor);
        CardResolutionColor = NormalizeCardColor(resolutionColor);

        Touch();
    }

    private static string? NormalizeCardColor(string? color)
    {
        var trimmed = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        if (trimmed is null)
            return null;

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                trimmed, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"))
            throw new ArgumentException(
                "A cor do cartão deve ser hexadecimal (#RGB ou #RRGGBB).",
                nameof(color));

        return trimmed;
    }

    /// <summary>Registra o estado de conexão visto na Evolution (open/connecting/close).</summary>
    public void RecordConnectionState(string? state)
    {
        var trimmed = string.IsNullOrWhiteSpace(state) ? null : state.Trim();
        if (trimmed is not null && trimmed.Length > MaxStateLength)
            trimmed = trimmed[..MaxStateLength];
        ConnectionState = trimmed;
        Touch();
    }

    /// <summary>
    /// Registra o resultado de um envio de teste (tela Integração → WhatsApp).
    /// Sucesso limpa o erro; falha grava o erro recente truncado (sem segredos).
    /// </summary>
    public void RecordTestResult(bool ok, DateTime testedAtUtc, string? error)
    {
        LastTestedAtUtc = testedAtUtc;
        LastTestOk = ok;

        if (ok)
        {
            LastTestError = null;
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

    private static string? NormalizeGroupId(string? groupId)
    {
        var trimmed = string.IsNullOrWhiteSpace(groupId) ? null : groupId.Trim();
        if (trimmed is null)
            return null;

        if (!trimmed.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "O id do grupo deve terminar em @g.us (JID de grupo do WhatsApp).",
                nameof(groupId));

        if (trimmed.Length > MaxGroupIdLength)
            throw new ArgumentException(
                $"O id do grupo pode ter no máximo {MaxGroupIdLength} caracteres.",
                nameof(groupId));

        return trimmed;
    }

    private static string? NormalizePhone(string? phone)
    {
        var trimmed = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (trimmed is null)
            return null;

        if (trimmed.Length > MaxPhoneLength)
            throw new ArgumentException(
                $"O número pode ter no máximo {MaxPhoneLength} caracteres.",
                nameof(phone));

        return trimmed;
    }
}
