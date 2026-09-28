namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>Transição de estado da task (F7-T007 — Workflow).</summary>
public sealed record ChangeTaskStateViewModel(string State);
