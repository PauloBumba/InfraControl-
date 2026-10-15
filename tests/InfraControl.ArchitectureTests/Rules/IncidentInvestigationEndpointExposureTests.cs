using System.Reflection;
using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using InfraControl.Application.AI.Investigation;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// Fase C (ADR-053): exposição HTTP da investigação de incidente —
/// <c>POST /api/v1/incidents/{id}/investigate</c>. Policy Operate tem
/// de bater com o RequiredRoles do handler (ADR-030 §3).
/// </summary>
public sealed class IncidentInvestigationEndpointExposureTests : BaseArchitectureTest
{
    [Fact]
    public void The_Investigate_Route_Should_Exist_With_Operate()
    {
        var controller = ApiAssembly
            .GetTypes()
            .SingleOrDefault(type =>
                typeof(ControllerBase).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsClass: true }
                && type.FullName == "InfraControl.Api.Controllers.AI.IncidentInvestigationController");
        controller.Should().NotBeNull("o controller de investigação não pode sumir");

        var action = controller!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name == "Investigate");
        action.Should().NotBeNull("a action Investigate não pode sumir");

        var post = action!.GetCustomAttributes(inherit: true)
            .OfType<HttpPostAttribute>().FirstOrDefault();
        post.Should().NotBeNull("a action Investigate precisa continuar sendo POST");
        post!.Template.Should().Be("{id:guid}/investigate");

        var authorize = action.GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>().FirstOrDefault();
        authorize.Should().NotBeNull("a action Investigate exige autenticação");
        authorize!.Policy.Should().Be(AuthorizationPoliciesOperate);
    }

    private const string AuthorizationPoliciesOperate = "Operate";

    [Fact]
    public void The_Investigation_Handler_Should_Require_Operate_Roles()
    {
        var command = new InvestigateIncidentCommand(Guid.NewGuid());

        command.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
    }
}
