using System.Reflection;
using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using InfraControl.Application.AI.Knowledge;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// Fase D (ADR-054): exposição HTTP da geração de rascunho com IA —
/// incidente e task. As duas policies têm de ser Operate e casar com o
/// RequiredRoles dos handlers.
/// </summary>
public sealed class KnowledgeDraftEndpointExposureTests : BaseArchitectureTest
{
    [Theory]
    [InlineData("InfraControl.Api.Controllers.AI.IncidentKnowledgeController",
        "GenerateKnowledge", "{id:guid}/generate-knowledge")]
    [InlineData("InfraControl.Api.Controllers.AI.TaskKnowledgeAiController",
        "GenerateKnowledgeWithAi", "{taskId:guid}/generate-knowledge/ai")]
    public void The_Generate_Knowledge_Route_Should_Exist_With_Operate(
        string controllerFullName, string actionName, string expectedTemplate)
    {
        var controller = ApiAssembly
            .GetTypes()
            .SingleOrDefault(type =>
                typeof(ControllerBase).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsClass: true }
                && type.FullName == controllerFullName);
        controller.Should().NotBeNull($"{controllerFullName} não pode sumir");

        var action = controller!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name == actionName);
        action.Should().NotBeNull($"a action {actionName} não pode sumir");

        var post = action!.GetCustomAttributes(inherit: true)
            .OfType<HttpPostAttribute>().FirstOrDefault();
        post.Should().NotBeNull($"{actionName} precisa continuar sendo POST");
        post!.Template.Should().Be(expectedTemplate);

        var authorize = action.GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>().FirstOrDefault();
        authorize.Should().NotBeNull($"{actionName} exige autenticação");
        authorize!.Policy.Should().Be("Operate");
    }

    [Fact]
    public void Both_Draft_Commands_Should_Require_Operate_Roles()
    {
        var incidentCommand = new GenerateIncidentKnowledgeDraftCommand(Guid.NewGuid());
        var taskCommand = new GenerateTaskKnowledgeDraftCommand(Guid.NewGuid());

        incidentCommand.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
        taskCommand.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
    }
}
