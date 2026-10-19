using System.Text.Json;
using FluentAssertions;
using InfraControl.Api.ViewModels.Projects;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// ADR-061 §2 (H2): <c>ReviewProjectEvidenceViewModel.Decision</c> aceita
/// a string que o front envia ("Validated"/"Rejected"). Sem o
/// <c>[JsonStringEnumConverter]</c> (molde PDI) o STJ espera número e TODO
/// POST de revisão de evidência de projeto caía em 400 de model binding.
/// </summary>
public sealed class ProjectEvidenceViewModelBindingTests
{
    [Theory]
    [InlineData("\"Validated\"", Domain.Enums.ProjectEvidenceReviewDecision.Validated)]
    [InlineData("\"Rejected\"", Domain.Enums.ProjectEvidenceReviewDecision.Rejected)]
    public void Decision_Should_Deserialize_From_The_Enum_Name(string json, Domain.Enums.ProjectEvidenceReviewDecision expected)
    {
        var model = JsonSerializer.Deserialize<ReviewProjectEvidenceViewModel>(
            $$"""{"decision": {{json}}, "rejectionReason": null}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        model.Should().NotBeNull();
        model!.Decision.Should().Be(expected);
    }

    [Fact]
    public void Decision_Should_Still_Reject_An_Unknown_Value()
    {
        var act = () => JsonSerializer.Deserialize<ReviewProjectEvidenceViewModel>(
            """{"decision": "Maybe"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        act.Should().Throw<JsonException>();
    }
}
