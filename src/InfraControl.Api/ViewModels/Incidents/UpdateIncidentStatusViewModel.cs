namespace InfraControl.Api.ViewModels.Incidents;

/// <summary>
/// Troca de status do incidente (F6-T003). F52-T001: na transição →
/// Resolved, <c>resolutionCode</c> (Fixed|Workaround|Duplicate|NoAction|
/// Other) e <c>resolutionNote</c> são obrigatórios no handler.
/// </summary>
public sealed record UpdateIncidentStatusViewModel(
    string Status,
    string? ResolutionCode = null,
    string? ResolutionNote = null);
