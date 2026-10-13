namespace InfraControl.Application.Projects;

/// <summary>Impedimento/risco do projeto (ADR-043 · docs/38 §3.4).</summary>
public sealed record ProjectImpedimentDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Description,
    string Category,
    string Severity,
    string? MitigationPlan,
    string Status,
    bool IsResolved,
    DateTimeOffset? ResolvedAt);
