using InfraControl.Domain.Enums;

namespace InfraControl.Api.ViewModels.Projects;

public sealed class ReviewProjectEvidenceViewModel
{
    public ProjectEvidenceReviewDecision Decision { get; set; }
    public string? RejectionReason { get; set; }
}
