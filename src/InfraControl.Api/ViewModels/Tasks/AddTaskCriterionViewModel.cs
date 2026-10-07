namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>Novo critério de aceite em uma task (F23-T003 — §12).</summary>
public sealed record AddTaskCriterionViewModel(string Description);

/// <summary>
/// PUT de critério (F23-T003): descrição e/ou marcação de atendido;
/// `null` mantém o valor atual.
/// </summary>
public sealed record UpdateTaskCriterionViewModel(
    string? Description = null,
    bool? Done = null);
