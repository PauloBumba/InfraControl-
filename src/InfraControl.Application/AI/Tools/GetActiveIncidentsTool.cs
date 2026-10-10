using InfraControl.Application.Incidents;
using InfraControl.Application.Incidents.Queries;
using MediatR;

namespace InfraControl.Application.AI.Tools;

/// <summary>
/// Tool <c>get_active_incidents</c> (FAI-T008/S7 · docs/34 §21/§52):
/// lista os incidentes ATIVOS (Open/Investigating/Identified/
/// Monitoring — nunca Resolved) via query MediatR existente
/// (<see cref="SearchIncidentsQuery"/> — caminho Tool → Application
/// Service, §20; nada de banco direto). Nível Read: qualquer role
/// autenticada, on-the-ground do pipeline MediatR reforça
/// <c>IAuthorizedRequest.RequiredRoles</c> = Read.
/// </summary>
public sealed class GetActiveIncidentsTool(IMediator mediator) : IAiTool
{
    private static readonly string[] ActiveStatuses =
        ["Open", "Investigating", "Identified", "Monitoring"];

    public AiToolDescriptor Descriptor { get; } = new(
        Name: "get_active_incidents",
        Description: "Lista incidentes ativos (abertos/em investigação/identificados/em monitoramento) com severidade e prazo de abertura.",
        Access: AiToolAccess.Read,
        Timeout: TimeSpan.FromSeconds(10),
        MaxResults: 50,
        InputSchema: """{"limit?": "int — máximo de incidentes retornados (≤ 50)"}""",
        OutputSchema: """{"findings[]": [{"id", "title", "status", "detail=severity", "occurredAt=OpenedAt"}], "total", "source", "timestamp"}""",
        AuditPolicy: "AI.Tool.* (S6 — auditoria de domínio)",
        Source: "Incidents/SearchIncidentsQuery");

    public async Task<AiToolResult> ExecuteAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(arguments.GetInt("limit") ?? Descriptor.MaxResults, 1, Descriptor.MaxResults);

        var findings = new List<AiToolFinding>();
        var total = 0;
        foreach (var status in ActiveStatuses)
        {
            var page = await mediator.Send(
                new SearchIncidentsQuery(Status: status, Page: 1, PageSize: limit),
                cancellationToken);
            total += page.TotalCount;
            findings.AddRange(page.Items.Select(incident => new AiToolFinding(
                Id: incident.Id.ToString(),
                Title: incident.Title,
                Status: incident.Status,
                Detail: $"severidade {incident.Severity}",
                OccurredAt: incident.OpenedAt)));
        }

        var top = findings
            .OrderByDescending(finding => finding.OccurredAt)
            .Take(limit)
            .ToList();

        return new AiToolResult(
            Descriptor.Name,
            top,
            total,
            Descriptor.Source,
            DateTimeOffset.UtcNow);
    }
}
