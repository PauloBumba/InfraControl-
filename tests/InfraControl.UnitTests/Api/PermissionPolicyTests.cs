using System.Security.Claims;
using FluentAssertions;
using InfraControl.Api.Authorization;
using InfraControl.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// Achado F6-T003: controllers usavam [Authorize(Policy = "InventoryRead")]
/// mas só existiam claims de permissão — a policy nomeada faltava e o
/// endpoint falharia em runtime. Agora toda Permission vira policy.
/// </summary>
public sealed class PermissionPolicyTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPermissionPolicies());
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal UserWith(params Permission[] permissions)
        => new(new ClaimsIdentity(
            permissions.Select(permission =>
                new Claim(PermissionClaim.Type, permission.ToString())),
            authenticationType: "Test"));

    [Fact]
    public async Task EveryPermission_ShouldHaveANamedPolicy()
    {
        await using var provider = CreateProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (Permission permission in Enum.GetValues<Permission>())
        {
            (await policies.GetPolicyAsync(permission.ToString()))
                .Should().NotBeNull($"a policy '{permission}' deve existir");
        }
    }

    [Fact]
    public async Task Policy_ShouldAuthorizeMatchingClaimAndRejectOthers()
    {
        await using var provider = CreateProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var policy = (await policies.GetPolicyAsync(
            Permission.InventoryRead.ToString()))!;

        (await authorization.AuthorizeAsync(
            UserWith(Permission.InventoryRead), policy)).Succeeded.Should().BeTrue();
        (await authorization.AuthorizeAsync(
            UserWith(Permission.MonitoringRead), policy)).Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()), policy))
            .Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task UnknownPolicy_ShouldReturnNull()
    {
        await using var provider = CreateProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        (await policies.GetPolicyAsync("DoesNotExist")).Should().BeNull();
    }
}
