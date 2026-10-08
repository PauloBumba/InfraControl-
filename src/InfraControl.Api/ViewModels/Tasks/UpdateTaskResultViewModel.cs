namespace InfraControl.Api.ViewModels.Tasks;

/// <summary>
/// PUT do resultado da task (F23-T003 — §5) — `null`/vazio limpa o
/// valor; acima de 2000 caracteres → 400 pt-BR (domínio).
/// </summary>
public sealed record UpdateTaskResultViewModel(string? Result = null);
