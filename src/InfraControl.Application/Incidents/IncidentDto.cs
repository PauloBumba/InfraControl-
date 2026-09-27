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
    Guid[] AlertIds)
{
    internal static IncidentDto From(
        Domain.Entities.Incident incident,
        int alertCount = 0,
        IReadOnlyCollection<Guid>? alertIds = null)
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
            alertIds?.ToArray() ?? []);
}
