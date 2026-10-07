namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>Novo comentário em uma task (F7-T005).</summary>
public sealed record AddTaskCommentViewModel(string Content);

/// <summary>PUT de comentário (ADR-030 §1).</summary>
public sealed record UpdateTaskCommentViewModel(string Content);
