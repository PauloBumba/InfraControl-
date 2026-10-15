using System.Reflection;
using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using InfraControl.Application.AI.Automation;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// Fase F (ADR-055): exposição HTTP do Automation Agent LEVEL 1 —
/// <c>POST /api/v1/automation/recommendations</c>. Policy Operate casando
/// com o RequiredRoles do handler.
/// </summary>
public sealed class AutomationRecommendationsEndpointExposureTests : BaseArchitectureTest
{
    [Fact]
    public void The_Recommend_Route_Should_Exist_With_Operate()
    {
        var controller = ApiAssembly
            .GetTypes()
            .SingleOrDefault(type =>
                typeof(ControllerBase).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsClass: true }
                && type.FullName == "InfraControl.Api.Controllers.AI.AutomationRecommendationsController");
        controller.Should().NotBeNull("o AutomationRecommendationsController não pode sumir");

        var action = controller!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name == "Recommend");
        action.Should().NotBeNull("a action Recommend não pode sumir");

        var post = action!.GetCustomAttributes(inherit: true)
            .OfType<HttpPostAttribute>().FirstOrDefault();
        post.Should().NotBeNull("a action Recommend precisa continuar sendo POST");
        post!.Template.Should().Be("recommendations");

        var authorize = action.GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>().FirstOrDefault();
        authorize.Should().NotBeNull("a action Recommend exige autenticação");
        authorize!.Policy.Should().Be("Operate");
    }

    [Fact]
    public void The_Recommendation_Command_Should_Require_Operate_Roles()
    {
        var command = new GenerateAutomationRecommendationsCommand();

        command.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
    }
}
