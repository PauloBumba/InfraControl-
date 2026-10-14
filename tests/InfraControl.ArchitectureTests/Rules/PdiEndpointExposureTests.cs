using System.Reflection;
using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// Exposição HTTP das rotas de avaliação e dashboard do PDI (docs/30
/// §15.3–§20). Os handlers (commands/queries) e seus testes unit já
/// existiam, mas nenhum controller expunha as rotas que o front invocava —
/// a PdiPage/PdiEvaluationDialog chamavam endpoints que respondiam 404.
/// Este guarda que as 6 rotas continuem publicadas: sumir/renomear a
/// action, trocar o verbo ou a policy quebra aqui (e o front em runtime).
/// A policy tem de bater com o <c>RequiredRoles</c> do handler (ADR-030 §3).
/// </summary>
public sealed class PdiEndpointExposureTests : BaseArchitectureTest
{
    private sealed record ExpectedRoute(
        string Controller,
        string Action,
        string Verb,
        string TemplateFragment,
        string Policy);

    private static readonly ExpectedRoute[] RequiredPdiRoutes =
    [
        // Avaliação (PdiEvaluationController)
        new("InfraControl.Api.Controllers.Operations.PdiEvaluationController",
            "GetEvaluation", "GET", "{userId:guid}/evaluation", "Operate"),
        new("InfraControl.Api.Controllers.Operations.PdiEvaluationController",
            "SaveAssessment", "PUT", "{userId:guid}/assessment", "Operate"),
        new("InfraControl.Api.Controllers.Operations.PdiEvaluationController",
            "FinalizeResult", "PUT", "{userId:guid}/result", "Operate"),
        // Dashboards (PdiDashboardController)
        new("InfraControl.Api.Controllers.Operations.PdiDashboardController",
            "GetDashboard", "GET", "{userId:guid}/dashboard", "Operate"),
        new("InfraControl.Api.Controllers.Operations.PdiDashboardController",
            "GetManagerDashboard", "GET", "manager/dashboard", "Operate"),
        new("InfraControl.Api.Controllers.Operations.PdiDashboardController",
            "GetAdminDashboard", "GET", "admin/dashboard", "Administer"),
    ];

    [Fact]
    public void The_Six_Pdi_Evaluation_And_Dashboard_Routes_Should_Exist()
    {
        foreach (var expected in RequiredPdiRoutes)
        {
            var controller = ApiAssembly
                .GetTypes()
                .SingleOrDefault(type =>
                    typeof(ControllerBase).IsAssignableFrom(type)
                    && type is { IsAbstract: false, IsClass: true }
                    && type.FullName == expected.Controller);
            controller.Should().NotBeNull($"controller {expected.Controller} não pode sumir");

            var action = controller!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(method => method.Name == expected.Action);
            action.Should().NotBeNull(
                $"{expected.Controller}.{expected.Action} (rota que o front PDI consome) não pode sumir");

            var (attribute, template) = expected.Verb switch
            {
                "GET" => (
                    (object?)action!.GetCustomAttributes(inherit: true).OfType<HttpGetAttribute>().FirstOrDefault(),
                    action.GetCustomAttributes(inherit: true).OfType<HttpGetAttribute>().FirstOrDefault()?.Template),
                "PUT" => (
                    (object?)action!.GetCustomAttributes(inherit: true).OfType<HttpPutAttribute>().FirstOrDefault(),
                    action.GetCustomAttributes(inherit: true).OfType<HttpPutAttribute>().FirstOrDefault()?.Template),
                _ => ((object?)null, null),
            };

            attribute.Should().NotBeNull($"{expected.Controller}.{expected.Action} precisa ser {expected.Verb}");
            template.Should().Contain(expected.TemplateFragment,
                $"a rota de {expected.Action} precisa manter o template {expected.TemplateFragment}");

            var authorize = action!
                .GetCustomAttributes(inherit: true)
                .OfType<AuthorizeAttribute>()
                .FirstOrDefault();
            authorize.Should().NotBeNull(
                $"{expected.Controller}.{expected.Action} precisa de [Authorize] (ADR-030 §3 — 1ª camada)");
            authorize!.Policy.Should().Be(expected.Policy,
                $"a policy de {expected.Action} tem de casar com o RequiredRoles do handler");
        }
    }
}
