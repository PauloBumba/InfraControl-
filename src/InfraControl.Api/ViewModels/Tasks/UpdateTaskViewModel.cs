namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>Atualização de task — título/descrição (F7-T001).</summary>
public sealed record UpdateTaskViewModel(
    string Title,
    string? Description = null);
