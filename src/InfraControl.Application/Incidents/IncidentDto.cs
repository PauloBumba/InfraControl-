namespace InfraControl.Application.Incidents;

/// <summary>Incident com contagem/vínculos de alerts (F6-T003).</summary>
public sealed record IncidentDto(
    Guid Id,
    string Title,
    string? Description,
    string Severity,
    string Status,
    Guid? DeviceId,
    DateTime OpenedAt,
    DateTime? ResolvedAt,
    int AlertCount,
    Guid[] AlertIds,
    string? RootCause = null,
    string? ResolutionCode = null,
    string? ResolutionNote = null,
    Guid? AssignedToId = null,
    string? AssignedToName = null)
{
    internal static IncidentDto From(
        Domain.Entities.Incident incident,
        int alertCount = 0,
        IReadOnlyCollection<Guid>? alertIds = null,
        string? assignedToName = null)
        => new(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity.ToString(),
            incident.Status.ToString(),
            incident.DeviceId,
            incident.CreateDateTime,
            incident.ResolvedAt,
            alertCount,
            alertIds?.ToArray() ?? [],
            incident.RootCause,
            incident.ResolutionCode?.ToString(),
            incident.ResolutionNote,
            incident.AssignedToId,
            assignedToName);
}
