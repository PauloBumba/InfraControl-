using System.Text.Json.Serialization;
using InfraControl.Domain.Enums;

namespace InfraControl.Api.ViewModels.Projects;

/// <summary>
/// ADR-059 §2 (H2): conversor explícito no molde de PDI — o front envia
/// "Validated"/"Rejected" (contrato de enum = string); sem o converter o
/// STJ espera número e todo POST de revisão caía em 400 de binding.
/// </summary>
public sealed class ReviewProjectEvidenceViewModel
{
    [JsonConverter(typeof(JsonStringEnumConverter<ProjectEvidenceReviewDecision>))]
    public ProjectEvidenceReviewDecision Decision { get; set; }
    public string? RejectionReason { get; set; }
}
