using System.Text.RegularExpressions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Common.Audit.Queries;

public sealed record SearchAuditEntriesQuery(
    string? Action = null,
    string? Outcome = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Actor = null,
    int Page = 1,
    int PageSize = 20) : BaseQuery<PagedResult<AuditEntrySummary>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(1);

    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class SearchAuditEntriesQueryHandler(IAuditReader reader)
    : IRequestHandler<SearchAuditEntriesQuery, PagedResult<AuditEntrySummary>>
{
    public async Task<PagedResult<AuditEntrySummary>> Handle(
        SearchAuditEntriesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await reader.SearchAsync(
            request.Action?.Trim(),
            request.Outcome?.Trim(),
            request.From,
            request.To,
            request.Actor?.Trim(),
            page,
            pageSize,
            cancellationToken);

        // F50-T004 — descrição detalhada por evento, montada do que já vem do
        // reader (nada de coluna nova): quem executou o quê, em qual recurso e
        // com qual resultado; a falha carrega o erro. `with` preserva o resto.
        return result with
        {
            Items = result.Items.Select(Describe).ToList()
        };
    }

    private static AuditEntrySummary Describe(AuditEntrySummary entry)
    {
        var actor = entry.ActorName ?? entry.ActorId ?? "Sistema";
        var outcome = entry.Outcome switch
        {
            "Succeeded" => "sucesso",
            "Failed" => string.IsNullOrWhiteSpace(entry.Error)
                ? "falha"
                : $"falha: {entry.Error}",
            _ => entry.Outcome
        };

        return entry with
        {
            Description =
                $"{actor} executou {Humanize(entry.Action)} em {entry.Resource} — {outcome}"
        };
    }

    // Espelho do humanizer do front (audit.ts): camelCase → com espaços,
    // sem partir siglas (AI.chat → AI.chat, HTTPError → HTTP Error).
    private static string Humanize(string action) =>
        Regex.Replace(
            Regex.Replace(action, "([a-z0-9])([A-Z])", "$1 $2"),
            "([A-Z]+)([A-Z][a-z])",
            "$1 $2");
}
