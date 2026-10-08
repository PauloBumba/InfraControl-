namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>
/// Criação de task (F7-T001) com identificação básica (F23-T001):
/// prioridade (null = padrão Medium), prazo e responsável opcionais.
/// `parentTaskId` (F23-T004/P4a) cria a task como subtarefa — 404 se o
/// pai não existe, 400 se o pai é ele mesmo subtarefa.
/// </summary>
public sealed record CreateTaskViewModel(
    string Title,
    string Kind,
    string? Description = null,
    string? Priority = null,
    DateOnly? DueDate = null,
    Guid? ResponsibleId = null,
    Guid? ParentTaskId = null);
