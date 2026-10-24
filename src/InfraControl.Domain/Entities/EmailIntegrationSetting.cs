using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F41-T001: configuração administrativa do canal de e-mail SMTP
/// persistida no InfraControl (linha única — singleton). A senha vive
/// cifrada (ICredentialProtector/AES-GCM) e nunca aparece em DTO, log ou
/// resposta. Enquanto não existir linha, o canal usa a configuração de env
/// (Notifications:Channels:Email) — mesmo fallback do Zabbix/WhatsApp.
/// Cobre os dois gatilhos: canal Email das notificações (F9-T005) e
/// IIdentityEmailSender (convite F1-T017 / forgot-password).
/// </summary>
public sealed class EmailIntegrationSetting : Entity
{
    public const int MaxLength = 512;
    public const int MaxNameLength = 256;
    public const int MaxErrorLength = 1000;

    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; } = 587;
    public string Username { get; private set; } = string.Empty;
    public string PasswordEncrypted { get; private set; } = string.Empty;
    public string From { get; private set; } = string.Empty;
    public string? FromName { get; private set; }
    public bool UseSsl { get; private set; } = true;

    /// <summary>
    /// Null = seguir o fanout legado (Notifications:Channels:Default);
    /// valor explícito da plataforma só afeta o canal Email.
    /// </summary>
    public bool? NotificationsEnabled { get; private set; }

    public DateTime? LastTestedAtUtc { get; private set; }
    public bool LastTestOk { get; private set; }
    public string? LastTestError { get; private set; }

    private EmailIntegrationSetting() { }

    public static EmailIntegrationSetting Create(
        string host,
        int port,
        string username,
        string passwordEncrypted,
        string from,
        string? fromName,
        bool useSsl)
    {
        var setting = new EmailIntegrationSetting();
        setting.Update(host, port, username, passwordEncrypted, from, fromName, useSsl, setting.NotificationsEnabled);
        return setting;
    }

    /// <summary>
    /// Atualiza os campos de configuração. <paramref name="passwordEncrypted"/>
    /// nulo ou vazio = mantém a senha já cifrada (rotatória opcional).
    /// </summary>
    public void Update(
        string host,
        int port,
        string username,
        string? passwordEncrypted,
        string from,
        string? fromName,
        bool useSsl,
        bool? notificationsEnabled)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException(
                "O host SMTP é obrigatório.", nameof(host));

        if (host.Length > MaxLength)
            throw new ArgumentException(
                $"O host pode ter no máximo {MaxLength} caracteres.",
                nameof(host));

        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(
                nameof(port), port, "A porta SMTP deve estar entre 1 e 65535.");

        if (username.Length > MaxLength)
            throw new ArgumentException(
                $"O usuário pode ter no máximo {MaxLength} caracteres.",
                nameof(username));

        if (string.IsNullOrWhiteSpace(from))
            throw new ArgumentException(
                "O e-mail remetente (From) é obrigatório.", nameof(from));

        if (from.Length > MaxNameLength)
            throw new ArgumentException(
                $"O remetente pode ter no máximo {MaxNameLength} caracteres.",
                nameof(from));

        if (fromName is { Length: > MaxNameLength })
            throw new ArgumentException(
                $"O nome do remetente pode ter no máximo {MaxNameLength} caracteres.",
                nameof(fromName));

        Host = host.Trim();
        Port = port;
        Username = username.Trim();
        From = from.Trim();
        FromName = string.IsNullOrWhiteSpace(fromName) ? null : fromName.Trim();
        UseSsl = useSsl;
        NotificationsEnabled = notificationsEnabled;

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
    /// Registra o resultado de um teste de envio (painel exibe estado e
    /// última tentativa). Sucesso limpa o erro; falha grava o erro
    /// recente truncado, sem segredos.
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
}
