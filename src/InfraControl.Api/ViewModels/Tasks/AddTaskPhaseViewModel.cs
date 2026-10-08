namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>
/// Nova fase/etapa (F23-T004/P4d — `POST /tasks/{id}/phases`): nome
/// obrigatório ≤120 (400 pt-BR do domínio); a primeira fase da task já
/// nasce como a atual.
/// </summary>
public sealed record AddTaskPhaseViewModel(string Name);

/// <summary>
/// Atualização da fase (`PUT /tasks/phases/{id}`): campos nulos mantêm
/// o valor atual. `current` limpa o ponteiro dos irmãos (máx. 1 fase
/// atual por task) e `completed` carimba/limpa a conclusão.
/// </summary>
public sealed record UpdateTaskPhaseViewModel(
    string? Name = null,
    bool? Current = null,
    bool? Completed = null);
