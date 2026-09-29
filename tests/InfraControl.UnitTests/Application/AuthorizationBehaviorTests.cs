using FluentAssertions;
using InfraControl.Application.Common.Behaviors;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.Application.Common.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Authorization;

/// <summary>
/// ADR-016: 2ª camada de autorização (MediatR). A semântica é OU — basta uma
/// das roles exigidas estar concedida. A pirâmide é o que dá: `RoleAccess.Read`
/// lista as 3 roles, então exigir TODAS (AND) deixaria a leitura impossível
/// para quem não é Admin. Regressão aqui é silenciosa: o endpoint continua
/// respondendo, só que 401 para quem deveria ver.
/// </summary>
public sealed class AuthorizationBehaviorTests
{
    private sealed record Request(IReadOnlyCollection<string> RequiredRoles) : IAuthorizedRequest;

    private static AuthorizationBehavior<Request, string> Behavior(
        bool authenticated, params string[] roles)
        => new(new StubUser(authenticated, roles));

    private sealed class StubUser(bool authenticated, IReadOnlyCollection<string> roles)
        : ICurrentUserContext
    {
        public bool IsAuthenticated { get; } = authenticated;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public Guid? UserId => Guid.NewGuid();
        public string? UserName => "stub";
    }

    private static async Task<bool> AllowedAsync(Request request, bool authenticated, params string[] roles)
    {
        var behavior = Behavior(authenticated, roles);
        try
        {
            await behavior.Handle(request, () => Task.FromResult("ok"), CancellationToken.None);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    [Theory]
    [InlineData(UserRole.Viewer, true, false, false)]
    [InlineData(UserRole.Operator, true, true, false)]
    [InlineData(UserRole.Admin, true, true, true)]
    public async Task Role_Should_Reach_Its_Level_And_Below(
        UserRole role, bool canRead, bool canOperate, bool canAdminister)
    {
        string[] granted = [role.ToString()];

        (await AllowedAsync(new Request(RoleAccess.Read), true, granted))
            .Should().Be(canRead, $"{role} e leitura");
        (await AllowedAsync(new Request(RoleAccess.Operate), true, granted))
            .Should().Be(canOperate, $"{role} e operação");
        (await AllowedAsync(new Request(RoleAccess.Administer), true, granted))
            .Should().Be(canAdminister, $"{role} e administração");
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("Operate")]
    [InlineData("Administer")]
    public async Task Authenticated_Without_Roles_Should_Be_Denied(string level)
    {
        var request = new Request(level switch
        {
            "Read" => RoleAccess.Read,
            "Operate" => RoleAccess.Operate,
            _ => RoleAccess.Administer
        });

        (await AllowedAsync(request, authenticated: true, roles: []))
            .Should().BeFalse("não ter role não é ter acesso");
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("Operate")]
    [InlineData("Administer")]
    public async Task Unauthenticated_Should_Be_Denied_Even_Without_Required_Roles(string _)
    {
        (await AllowedAsync(new Request([]), authenticated: false))
            .Should().BeFalse("worker ou chamador sem cookie não pode ler");
    }

    [Fact]
    public async Task Multiple_Roles_Should_Grant_Access_Individually()
    {
        // usuário com Viewer+Operator lê e opera, mas não administra
        string[] granted = [UserRole.Viewer.ToString(), UserRole.Operator.ToString()];

        (await AllowedAsync(new Request(RoleAccess.Read), true, granted)).Should().BeTrue();
        (await AllowedAsync(new Request(RoleAccess.Operate), true, granted)).Should().BeTrue();
        (await AllowedAsync(new Request(RoleAccess.Administer), true, granted)).Should().BeFalse();
    }

    [Fact]
    public async Task Denied_Request_Should_Name_The_Required_Roles()
    {
        var behavior = Behavior(authenticated: true, roles: [UserRole.Viewer.ToString()]);
        var act = () => behavior.Handle(
            new Request(RoleAccess.Administer), () => Task.FromResult("ok"), CancellationToken.None);

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*Admin*");
    }

    [Fact]
    public async Task Not_An_Authorized_Request_Should_Not_Be_Blocked()
    {
        var behavior = new AuthorizationBehavior<NotAuthorized, string>(
            new StubUser(authenticated: false, []));

        var result = await behavior.Handle(
            new NotAuthorized(), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    private sealed record NotAuthorized : IQuery<string>;
}
