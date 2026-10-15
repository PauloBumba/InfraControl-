using System.Reflection;
using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using InfraControl.Application.AI.Chat;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// Fase B (ADR-052): exposição HTTP do chat do Copilot —
/// <c>POST /api/v1/ai/chat</c>. Sumir/renomear a action, trocar o verbo
/// ou a autorização quebra aqui (e o front em runtime). Read = Authorize
/// SEM policy (ADR-016/ADR-052 §4) e tem de bater com o RequiredRoles
/// do handler (ADR-030 §3).
/// </summary>
public sealed class AiChatEndpointExposureTests : BaseArchitectureTest
{
    [Fact]
    public void The_Copilot_Chat_Route_Should_Exist_With_Read()
    {
        var controller = ApiAssembly
            .GetTypes()
            .SingleOrDefault(type =>
                typeof(ControllerBase).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsClass: true }
                && type.FullName == "InfraControl.Api.Controllers.AI.AiChatController");
        controller.Should().NotBeNull("o controller do Copilot não pode sumir");

        var action = controller!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name == "Chat");
        action.Should().NotBeNull("a action Chat (rota que o front Copilot consome) não pode sumir");

        var post = action!.GetCustomAttributes(inherit: true)
            .OfType<HttpPostAttribute>().FirstOrDefault();
        post.Should().NotBeNull("a action Chat precisa continuar sendo POST");
        post!.Template.Should().Be("chat", "a rota é api/v1/ai/chat (controller + template)");

        var authorize = action.GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>().FirstOrDefault();
        authorize.Should().NotBeNull("a action Chat exige autenticação (Read)");
        authorize!.Policy.Should().BeNullOrEmpty(
            "Read = Authorize SEM policy (ADR-016 — qualquer role autenticada)");
    }

    [Fact]
    public void The_Copilot_Handler_Should_Require_Read_Roles()
    {
        var command = new SendAiChatCommand([new ChatTurn("user", "pergunta")]);

        command.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Read);
    }
}
