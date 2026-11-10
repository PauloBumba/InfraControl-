namespace InfraControl.Application.Common.Interfaces;

public sealed record AuditEntrySummary(
    Guid Id,
    string Action,
    string Resource,
    string Outcome,
    string? ActorId,
    string? ActorName,
    string? CorrelationId,
    string? Error,
    DateTime OccurredAt)
{
    /// <summary>
    /// F50-T004 — descrição detalhada do evento (quem executou o quê, em qual
    /// recurso e com qual resultado), montada pelo handler a partir do que já
    /// existe. Nulla no repositório (não é coluna) — só o handler de busca
    /// preenche; consumidores diretos do reader (ex.: dashboard UniFi) não usam.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

public interface IAuditReader
{
    Task<PagedResult<AuditEntrySummary>> SearchAsync(
        string? action,
        string? outcome,
        DateTime? from,
        DateTime? to,
        string? actor,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
