namespace InfraControl.Api.ViewModels.Checklists;

/// <summary>Novo item do checklist genérico (F50-T007/ADR-067).</summary>
public sealed record AddChecklistItemViewModel(string Description);

/// <summary>
/// PUT de item do checklist (ADR-067): descrição e/ou marcação de
/// atendido; `null` mantém o valor atual (mesmo molde do
/// `UpdateTaskCriterionViewModel`).
/// </summary>
public sealed record UpdateChecklistItemViewModel(
    string? Description = null,
    bool? Done = null);
