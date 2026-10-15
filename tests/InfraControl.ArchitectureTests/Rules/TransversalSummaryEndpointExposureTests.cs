using System.Reflection;
using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using InfraControl.Application.AI.Reporting;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// Fase E: exposição HTTP do resumo IA do relatório transversal —
/// <c>POST /api/v1/reports/transversal/ai-summary</c>. Policy Operate
/// casando com o RequiredRoles do handler.
/// </summary>
public sealed class TransversalSummaryEndpointExposureTests : BaseArchitectureTest
{
    [Fact]
    public void The_Ai_Summary_Route_Should_Exist_With_Operate()
    {
        var controller = ApiAssembly
            .GetTypes()
            .SingleOrDefault(type =>
                typeof(ControllerBase).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsClass: true }
                && type.FullName == "InfraControl.Api.Controllers.Reports.ReportsController");
        controller.Should().NotBeNull("o ReportsController não pode sumir");

        var action = controller!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name == "SummarizeTransversal");
        action.Should().NotBeNull("a action SummarizeTransversal não pode sumir");

        var post = action!.GetCustomAttributes(inherit: true)
            .OfType<HttpPostAttribute>().FirstOrDefault();
        post.Should().NotBeNull("a action precisa continuar sendo POST");
        post!.Template.Should().Be("transversal/ai-summary");

        var authorize = action.GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>().FirstOrDefault();
        authorize.Should().NotBeNull("a action exige autenticação");
        authorize!.Policy.Should().Be("Operate");
    }

    [Fact]
    public void The_Summary_Command_Should_Require_Operate_Roles()
    {
        var command = new SummarizeTransversalReportCommand(null, null);

        command.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Operate);
    }
}
