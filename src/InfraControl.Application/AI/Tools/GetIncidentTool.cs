using InfraControl.Application.Incidents.Queries;
using MediatR;

namespace InfraControl.Application.AI.Tools;

/// <summary>
/// Tool <c>get_incident</c> (ADR-053 §4 · docs/34 §21/§52): lê UM
/// incidente pelo id via query MediatR existente
/// (<see cref="GetIncidentQuery"/> — Tool → Application Service, §20).
/// Nível Read; o pipeline MediatR reforça
/// <c>IAuthorizedRequest.RequiredRoles</c> = Read.
/// </summary>
public sealed class GetIncidentTool(IMediator mediator) : IAiTool
{
    public AiToolDescriptor Descriptor { get; } = new(
        Name: "get_incident",
        Description: "Lê um incidente pelo id com título, severidade, status, descrição, prazo de abertura e contagem de alerts vinculados.",
        Access: AiToolAccess.Read,
        Timeout: TimeSpan.FromSeconds(10),
        MaxResults: 1,
        InputSchema: """{"id": "guid — id do incidente"}""",
        OutputSchema: """{"findings[]": [{"id", "title", "status", "detail=severity+device", "occurredAt=OpenedAt"}], "total", "source", "timestamp"}""",
        AuditPolicy: "AI.Tool.* (S6 — auditoria de domínio)",
        Source: "Incidents/GetIncidentQuery");

    public async Task<AiToolResult> ExecuteAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var rawId = arguments.Get("id");
        if (!Guid.TryParse(rawId, out var incidentId))
        {
            throw new AiToolArgumentException(
                "Argumento 'id' (guid) é obrigatório para get_incident.");
        }

        var incident = await mediator.Send(
            new GetIncidentQuery(incidentId), cancellationToken);

        var finding = new AiToolFinding(
            Id: incident.Id.ToString(),
            Title: incident.Title,
            Status: incident.Status,
            Detail: $"severidade {incident.Severity}"
                + (incident.DeviceId is Guid deviceId ? $" · device {deviceId}" : string.Empty)
                + (incident.RootCause is { Length: > 0 } rootCause ? $" · causa: {rootCause}" : string.Empty),
            OccurredAt: incident.OpenedAt);

        return new AiToolResult(
            Descriptor.Name,
            [finding],
            1,
            Descriptor.Source,
            DateTimeOffset.UtcNow);
    }
}
