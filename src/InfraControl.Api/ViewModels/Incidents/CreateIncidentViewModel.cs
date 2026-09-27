namespace InfraControl.Api.ViewModels.Incidents;

/// <summary>Criação manual de incidente (F6-T003).</summary>
public sealed record CreateIncidentViewModel(
    string Title,
    string Severity,
    string? Description = null,
    Guid? DeviceId = null);
