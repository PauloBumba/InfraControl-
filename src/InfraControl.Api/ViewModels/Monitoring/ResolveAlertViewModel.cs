namespace InfraControl.Api.ViewModels.Monitoring;

/// <summary>
/// F52-T003 — baixa justificada do alerta: código
/// (Fixed|Workaround|Duplicate|NoAction|Other) + justificativa
/// obrigatória (máx. 500). Espelha <see cref="SetAlertNoteViewModel"/>.
/// </summary>
public sealed record ResolveAlertViewModel(string Code, string Note);
