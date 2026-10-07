namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>
/// Registro de impedimento (F23-T004/P4b — `POST /tasks/{id}/impediments`):
/// descrição obrigatória ≤300 (400 pt-BR do domínio); não altera o estado
/// do workflow.
/// </summary>
public sealed record AddTaskImpedimentViewModel(string Description);

/// <summary>
/// Atualização do impedimento (`PUT /tasks/impediments/{id}`): campos
/// nulos mantêm o valor atual (descrição e/ou resolução — `resolved`
/// marca/reabre).
/// </summary>
public sealed record UpdateTaskImpedimentViewModel(
    string? Description = null,
    bool? Resolved = null);
