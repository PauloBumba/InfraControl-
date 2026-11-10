namespace InfraControl.Api.ViewModels.Projects;

/// <summary>Solicitação formal de conclusão do projeto (spec §6).</summary>
public sealed class SubmitProjectConclusionViewModel
{
    public string Description { get; set; } = string.Empty;
    public string ActivitiesPerformed { get; set; } = string.Empty;
    public string ResultsAchieved { get; set; } = string.Empty;
    public string? ProblemsEncountered { get; set; }
    public string? SolutionsImplemented { get; set; }
    public string? EvidenceSummary { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ReturnProjectConclusionViewModel
{
    /// <summary>"reject" (reprovar) ou "requestchanges" (solicitar ajustes).</summary>
    public string? Decision { get; set; }
    public string Justification { get; set; } = string.Empty;
}
