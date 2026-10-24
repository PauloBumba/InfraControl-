using System.Reflection;
using FluentAssertions;
using InfraControl.Api.Authorization;
using InfraControl.ArchitectureTests.Common;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// ADR-016: a role é a fonte única da verdade. Nenhum controller pode
/// reintroduzir literal de role nem nome de policy de permissão — as duas
/// coisas já causaram drift ([Authorize(Roles = "Admin,User,Viewer")] e
/// [Authorize(Policy = "InventoryRead")] conviveram no mesmo código).
/// </summary>
public sealed class RoleAuthorizationStandardizationTests : BaseArchitectureTest
{
    private static Type[] ControllerTypes() => ApiAssembly
        .GetTypes()
        .Where(type => typeof(ControllerBase).IsAssignableFrom(type)
                       && type is { IsAbstract: false, IsClass: true })
        .OrderBy(type => type.FullName)
        .ToArray();

    public static TheoryData<string> ControllerNames()
    {
        var data = new TheoryData<string>();
        foreach (var type in ControllerTypes())
            data.Add(type.FullName!);
        return data;
    }

    private static AuthorizeAttribute[] AttributesOf(Type controller) => controller
        .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
        .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>()))
        .ToArray();

    [Fact]
    public void Controllers_Should_Exist_For_The_Rule_Below_To_Meaningfully_Apply()
        => ControllerTypes().Should().HaveCountGreaterThan(20,
            "os controllers são a superfície onde o drift de autorização aparecia");

    [Theory]
    [MemberData(nameof(ControllerNames))]
    public void Controller_Should_Not_Use_Role_Literals(string controllerTypeName)
    {
        foreach (var attribute in AttributesOf(ApiAssembly.GetType(controllerTypeName)!))
        {
            attribute.Roles.Should().BeNullOrEmpty(
                $"{controllerTypeName} não pode usar [Authorize(Roles = \"...\")]: "
                + "use [Authorize] (leitura) ou AuthorizationPolicies.Operate/Administer");
        }
    }

    [Theory]
    [MemberData(nameof(ControllerNames))]
    public void Controller_Should_Not_Use_Permission_Policies(string controllerTypeName)
    {
        foreach (var attribute in AttributesOf(ApiAssembly.GetType(controllerTypeName)!)
                     .Where(attribute => attribute.Policy is not null))
        {
            attribute.Policy.Should().BeOneOf(
                AuthorizationPolicies.Operate,
                AuthorizationPolicies.Administer,
                $"'{attribute.Policy}' não é uma policy canônica do ADR-016");
        }
    }

    [Fact]
    public void Public_Endpoints_Should_Be_The_Explicit_Allowlist()
    {
        var candidates = ControllerTypes()
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.DeclaringType == controller) // action do slice
                .Where(method => !method.IsSpecialName) // property/event não é endpoint
                .Where(method => !method.GetCustomAttributes<AuthorizeAttribute>().Any()
                                 && !controller
                                     .GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any())
                .Select(method => (Controller: controller, Method: method)))
            .ToArray();

        string NameOf((Type Controller, MethodInfo Method) entry)
            => $"{entry.Controller.Name}.{entry.Method.Name}";

        // nenhum endpoint pode ficar sem [Authorize] nem AllowAnonymous
        candidates
            .Where(entry => !entry.Method.GetCustomAttributes<AllowAnonymousAttribute>().Any())
            .Select(NameOf)
            .Distinct()
            .Should().BeEmpty("endpoint sem [Authorize] nem AllowAnonymous ficaria público");

        // e o que é público tem de ser a lista explícita e conhecida
        var allowAnonymous = candidates
            .Where(entry => entry.Method.GetCustomAttributes<AllowAnonymousAttribute>().Any())
            .Select(NameOf)
            .Distinct()
            .ToArray();

        // lista verificada: fluxo de autenticação (entrar, renovar token, Google),
        // recuperação de senha e aceite de convite — nada mais é público
        allowAnonymous.Should().BeEquivalentTo(
        [
            "AuthenticationController.Register",
            "AuthenticationController.Login",
            "AuthenticationController.GoogleLoginStart",
            "AuthenticationController.GoogleLoginCallback",
            "AuthenticationController.Refresh",
            "AuthenticationController.GoogleLogin",
            "AuthenticationController.ForgotPassword",
            "AuthenticationController.ResetPassword",
            "InvitationAcceptanceController.Accept",
            "InvitationAcceptanceController.SetPassword",
            "InvitationAcceptanceController.Reject",
            // F16-T001/ADR-023: webhook de entrada do Zabbix — guard é o
            // token X-Zabbix-Token no controller (404 desligado/401 inválido)
            "ZabbixIntegrationController.Problems"
        ], "endpoint público exige AllowAnonymous explícito e na lista conhecida (ADR-016/ADR-023)");
    }

    [Fact]
    public void Role_Hierarchy_Should_Be_Declared_Only_Once()
    {
        // RoleAccess é a fonte única: policies HTTP e MediatR a consomem em vez
        // de reconstruir a hierarquia.
        typeof(RoleAccess).Assembly.Should().BeSameAs(typeof(UserRole).Assembly);
        RoleAccess.Administer.Should().BeEquivalentTo([UserRole.Admin.ToString()]);
    }
}
