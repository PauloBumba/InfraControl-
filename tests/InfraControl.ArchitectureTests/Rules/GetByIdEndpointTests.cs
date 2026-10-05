using System.Reflection;
using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// ADR-030 §2 — "GET /{id} obrigatório": toda entidade exposta tem também
/// recuperação pontual. Os 6 endpoints que fecharam o gap em 2026-10-04
/// ficam guardados contra regressão (remover/renomear a action quebra aqui).
/// Entidades append-only/infra ficam de fora por decisão do mesmo ADR.
/// </summary>
public sealed class GetByIdEndpointTests : BaseArchitectureTest
{
    private static readonly (string Controller, string Action)[] RequiredGetById =
    [
        // FullName completo: existem DOIS InvitationsController (Admin + self-service).
        ("InfraControl.Api.Controllers.Admin.UsersController", "Get"),
        ("InfraControl.Api.Controllers.Admin.InvitationsController", "Get"),
        ("InfraControl.Api.Controllers.Notifications.NotificationsController", "GetNotification"),
        ("InfraControl.Api.Controllers.Network.SwitchPortVlansController", "GetById"),
        ("InfraControl.Api.Controllers.Inventory.RemoteSessionsController", "GetSession"),
        ("InfraControl.Api.Controllers.Identity.SessionsController", "Get")
    ];

    [Fact]
    public void The_Six_Gap_Closing_Endpoints_Should_Exist_With_An_Id_Route()
    {
        foreach (var (controllerName, actionName) in RequiredGetById)
        {
            var controller = ApiAssembly
                .GetTypes()
                .SingleOrDefault(type =>
                    typeof(ControllerBase).IsAssignableFrom(type)
                    && type is { IsAbstract: false, IsClass: true }
                    && type.FullName == controllerName);
            controller.Should().NotBeNull($"controller {controllerName} não pode sumir");

            var action = controller!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(method => method.Name == actionName);
            action.Should().NotBeNull(
                $"{controllerName}.{actionName} (ADR-030 §2 — get by id) não pode sumir");

            var httpGet = action!
                .GetCustomAttributes(inherit: true)
                .OfType<HttpGetAttribute>()
                .FirstOrDefault();
            httpGet.Should().NotBeNull($"{controllerName}.{actionName} precisa ser GET");
            httpGet!.Template.Should().Contain("{",
                "a rota do get by id precisa receber o identificador");
        }
    }
}
