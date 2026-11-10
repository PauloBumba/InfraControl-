namespace InfraControl.Api.ViewModels.Monitoring;

/// <summary>
/// F50-T003 — anotação do operador no alerta: PUT de substituição
/// (texto grava, null/branco limpa — ver <c>Alert.SetOperatorNote</c>).
/// </summary>
public sealed record SetAlertNoteViewModel(string? Note);
