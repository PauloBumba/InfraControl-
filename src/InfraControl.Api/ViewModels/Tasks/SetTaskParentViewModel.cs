namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>
/// Vínculo de subtarefa (F23-T004/P4a) — `PUT /tasks/{id}/parent`:
/// `null` desvincula (task vira raiz); 400 para auto-relação/pai que é
/// subtarefa e 404 para task/pai inexistente.
/// </summary>
public sealed record SetTaskParentViewModel(Guid? ParentId = null);
