using FluentAssertions;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.IntegrationTests.Common;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Tasks;

/// <summary>
/// F7-T001: fluxo ponta-a-ponta das Tasks — create → search → get →
/// update → delete no pipeline real (AuthorizationBehavior + PostgreSQL
/// do Testcontainers).
/// </summary>
public sealed class TaskIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task CrudLifecycle_ShouldRoundTripViaQueries()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var created = await mediator.Send(new CreateTaskCommand(
            "Trocar módulo óptico do switch core", "Maintenance",
            "janela de manutenção noturna"));
        created.State.Should().Be(nameof(TaskState.Open));
        created.Kind.Should().Be(nameof(TaskKind.Maintenance));

        var page = await mediator.Send(new SearchTasksQuery(
            Term: "óptico", Kind: "maintenance", State: "open"));
        page.Items.Should().ContainSingle().Which.Id.Should().Be(created.Id);

        var fetched = await mediator.Send(new GetTaskQuery(created.Id));
        fetched.Title.Should().Be("Trocar módulo óptico do switch core");

        await mediator.Send(new UpdateTaskCommand(
            created.Id, "Trocar módulo óptico (revisado)", "escopo ampliado"));

        var updated = await mediator.Send(new GetTaskQuery(created.Id));
        updated.Title.Should().Be("Trocar módulo óptico (revisado)");
        updated.Description.Should().Be("escopo ampliado");

        await mediator.Send(new DeleteTaskCommand(created.Id));

        var afterDelete = () => mediator.Send(new GetTaskQuery(created.Id));
        (await afterDelete.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Task '{created.Id}' não encontrada.");
    }

    [Fact]
    public async Task Search_WithoutTasksRead_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(new SearchTasksQuery());

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator, Viewer");
    }

    [Fact]
    public async Task Create_WithoutTasksManage_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(
            new CreateTaskCommand("t", "Manual"));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

    [Fact]
    public async Task Create_UnknownKind_ShouldFailWith400PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var action = () => mediator.Send(
            new CreateTaskCommand("t", "besouro"));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Tipo inválido: 'besouro'.*");
    }
}
