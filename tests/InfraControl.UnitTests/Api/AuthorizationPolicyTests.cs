using System.Security.Claims;
using FluentAssertions;
using InfraControl.Api.Authorization;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// ADR-016: a role é a fonte única da verdade. Existem apenas 2 policies
/// nomeadas (Operate, Administer); leitura é <c>[Authorize]</c> sem policy.
/// A garantia testada aqui é a equivalência de acesso: Operator opera,
/// Viewer não; só Admin administra.
/// </summary>
public sealed class AuthorizationPolicyTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddRolePolicies());
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal UserWith(params UserRole[] roles)
        => new(new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role.ToString())),
            authenticationType: "Test"));

    [Fact]
    public async Task Only_The_Canonical_Policies_ShouldBe_Registered()
    {
        await using var provider = CreateProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        (await policies.GetPolicyAsync(AuthorizationPolicies.Operate)).Should().NotBeNull();
        (await policies.GetPolicyAsync(AuthorizationPolicies.Administer)).Should().NotBeNull();

        // nome de permissão deixou de ser policy: exigiria claim de permissão
        foreach (var retired in new[] { "InventoryRead", "SlaManage", "IdentityUsersManage" })
        {
            (await policies.GetPolicyAsync(retired)).Should().BeNull(
                $"'{retired}' era policy de permissão e não pode existir no ADR-016");
        }
    }

    [Theory]
    [InlineData(UserRole.Viewer, false)]
    [InlineData(UserRole.Operator, true)]
    [InlineData(UserRole.Admin, true)]
    public async Task Operate_Should_Allow_Operator_And_Admin(UserRole role, bool expected)
    {
        await using var provider = CreateProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            UserWith(role), null, AuthorizationPolicies.Operate);

        result.Succeeded.Should().Be(expected);
    }

    [Theory]
    [InlineData(UserRole.Viewer, false)]
    [InlineData(UserRole.Operator, false)]
    [InlineData(UserRole.Admin, true)]
    public async Task Administer_Should_Allow_Only_Admin(UserRole role, bool expected)
    {
        await using var provider = CreateProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            UserWith(role), null, AuthorizationPolicies.Administer);

        result.Succeeded.Should().Be(expected);
    }

    [Fact]
    public async Task Anonymouse_Should_Not_Pass_Either_Policy()
    {
        await using var provider = CreateProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        (await authorization.AuthorizeAsync(anonymous, null, AuthorizationPolicies.Operate))
            .Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(anonymous, null, AuthorizationPolicies.Administer))
            .Succeeded.Should().BeFalse();
    }

    [Fact]
    public void RoleAccess_Should_Form_A_Strict_Hierarchy()
    {
        // Ler ⊃ Operar ⊃ Administrar: quem administra também opera e lê
        RoleAccess.Read.Should().Contain(RoleAccess.Operate);
        RoleAccess.Operate.Should().Contain(RoleAccess.Administer);

        RoleAccess.Administer.Should().BeEquivalentTo([UserRole.Admin.ToString()]);
        RoleAccess.Operate.Should().BeEquivalentTo(
            [UserRole.Operator.ToString(), UserRole.Admin.ToString()]);
        RoleAccess.Read.Should().BeEquivalentTo(Enum.GetNames<UserRole>());
    }
}
