namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>
/// PUT de identificação da task (F23-T001) — comando de substituição:
/// os 3 campos vão juntos; `dueDate`/`responsibleId` nulos limpam o valor
/// e `priority` é obrigatória (400 pt-BR quando vazia).
/// </summary>
public sealed record SetTaskIdentificationViewModel(
    string Priority,
    DateOnly? DueDate = null,
    Guid? ResponsibleId = null);
