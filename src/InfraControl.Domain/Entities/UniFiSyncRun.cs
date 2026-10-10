using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F26 Fase 9.1: execução de sincronização UniFi — início, término,
/// resultado, contagens e erro SANITIZADO (nunca credenciais). Uma linha
/// por execução (sincronizações repetidas = eventos distintos
/// justificados — 9.4). Duração = FinishedAt − StartedAt.
/// </summary>
public sealed class UniFiSyncRun : Entity
{
    public const int MessageMaxLength = 1000;

    public DateTime StartedAtUtc { get; private set; }
    public DateTime FinishedAtUtc { get; private set; }
    public bool Succeeded { get; private set; }
    public int Created { get; private set; }
    public int Updated { get; private set; }
    public int References { get; private set; }
    public int Errors { get; private set; }
    public int Sites { get; private set; }
    public string? Message { get; private set; }

    private UniFiSyncRun() { }

    public static UniFiSyncRun Start(DateTime startedAtUtc) => new()
    {
        StartedAtUtc = startedAtUtc,
        FinishedAtUtc = startedAtUtc,
        Succeeded = false,
    };

    public void CompleteSuccess(
        DateTime finishedAtUtc, int created, int updated, int references, int errors, int sites)
    {
        FinishedAtUtc = finishedAtUtc;
        Succeeded = true;
        Created = created;
        Updated = updated;
        References = references;
        Errors = errors;
        Sites = sites;
        Touch();
    }

    public void CompleteFailure(DateTime finishedAtUtc, string message)
    {
        FinishedAtUtc = finishedAtUtc;
        Succeeded = false;
        Message = Sanitize(message);
        Touch();
    }

    /// <summary>9.1: erros sem expor credenciais (senha/token no texto).</summary>
    public static string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;
        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            message,
            "(pass(word)?|senha|token|secret|authorization)\\s*[:=]\\s*\\S+",
            "$1=[REDACTED]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return sanitized.Length <= MessageMaxLength
            ? sanitized
            : sanitized[..MessageMaxLength];
    }
}
