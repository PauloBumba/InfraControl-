namespace InfraControl.Api.ViewModels.Incidents;

/// <summary>
/// CRUD-Incidentes (2026-10-12) — edição dos campos descritivos. O status
/// não entra aqui (ciclo por estado via <c>UpdateIncidentStatusViewModel</c>,
/// ADR-030); a exclusão não existe por decisão de arquitetura.
/// </summary>
public sealed record UpdateIncidentViewModel(
    string Title,
    string? Description,
    string Severity);
