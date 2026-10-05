using FluentAssertions;
using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers.Integrations;
using InfraControl.Api.Controllers.Monitoring;
using InfraControl.Api.Controllers.Notifications;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// ADR-030 §3 — 1ª camada (HTTP) e 2ª camada (MediatR) declaram o MESMO
/// nível nas ações mutantes. RED (gap medido em 2026-10-04): (1)
/// RunCheck*/RunAllChecks exigiam só Read — Viewer disparava execução de
/// check; (2) UniFi sync e (3) preferences de notificação eram escrita
/// coberta apenas na 2ª camada.
/// </summary>
public sealed class AuthorizationGapTests
{
    private static string? PolicyOf(Type controller, string action)
    {
        var method = controller.GetMethod(action);
        method.Should().NotBeNull($"{controller.Name}.{action} deve existir.");
        return method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault()
            ?.Policy;
    }

    [Fact]
    public void RunCheckCommands_ShouldRequireOperateOnTheSecondLayer()
    {
        new RunCheckCommand(Guid.NewGuid(), "Ping").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new RunAllChecksCommand("Ping").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    [Theory]
    [InlineData(nameof(MonitoringController.RunCheck))]
    [InlineData(nameof(MonitoringController.RunAllChecks))]
    public void MonitoringMutations_ShouldDeclareOperatePolicy(string action)
        => PolicyOf(typeof(MonitoringController), action)
            .Should().Be(AuthorizationPolicies.Operate);

    [Fact]
    public void UnifiSync_ShouldDeclareOperatePolicy()
        => PolicyOf(typeof(UnifiIntegrationController), nameof(UnifiIntegrationController.Sync))
            .Should().Be(AuthorizationPolicies.Operate);

    [Theory]
    [InlineData(nameof(NotificationsController.SetPreference))]
    [InlineData(nameof(NotificationsController.DeletePreference))]
    public void NotificationPreferenceMutations_ShouldDeclareOperatePolicy(string action)
        => PolicyOf(typeof(NotificationsController), action)
            .Should().Be(AuthorizationPolicies.Operate);
}
