namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>Criação de task (F7-T001).</summary>
public sealed record CreateTaskViewModel(
    string Title,
    string Kind,
    string? Description = null);
