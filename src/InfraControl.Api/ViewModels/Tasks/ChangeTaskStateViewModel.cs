namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>Transição de estado da task (F7-T007 — Workflow).
/// F52-T002: <c>ResolutionCode</c> é obrigatório nos alvos Resolved/Closed
/// (Fixed/Workaround/Duplicate/NoAction/Other); ignorado nas demais.</summary>
public sealed record ChangeTaskStateViewModel(string State, string? ResolutionCode = null);
