namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>
/// Replanejamento da task (F23-T003 — §13): ajusta início/fim/prazo com
/// motivo obrigatório (400 pt-BR quando vazio ou sem mudança real);
/// `null` limpa a data.
/// </summary>
public sealed record ReplanTaskViewModel(
    DateOnly? PlannedStart = null,
    DateOnly? PlannedEnd = null,
    DateOnly? DueDate = null,
    string Reason = "");
