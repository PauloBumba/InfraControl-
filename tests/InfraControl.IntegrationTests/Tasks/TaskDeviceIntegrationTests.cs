using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.IntegrationTests.Common;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Tasks;

/// <summary>
/// F7-T003: vínculo Task ↔ Device ponta-a-ponta — link → get/search com
/// ids → exclusão de device bloqueada → unlink, no pipeline real.
/// </summary>
public sealed class TaskDeviceIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task LinkLifecycle_ShouldRoundTripViaQueries()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.72");
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(
            new CreateTaskCommand("Inventariar portas", "Discovery"));

        await mediator.Send(new LinkTaskDeviceCommand(task.Id, device.Id));

        var fetched = await mediator.Send(new GetTaskQuery(task.Id));
        fetched.DeviceCount.Should().Be(1);
        fetched.DeviceIds.Should().Equal(device.Id);

        var page = await mediator.Send(new SearchTasksQuery(Term: "Inventariar"));
        page.Items.Should().ContainSingle()
            .Which.DeviceIds.Should().Equal(device.Id);

        await mediator.Send(new UnlinkTaskDeviceCommand(task.Id, device.Id));

        var after = await mediator.Send(new GetTaskQuery(task.Id));
        after.DeviceCount.Should().Be(0);
        after.DeviceIds.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteDevice_WithLinkedTask_ShouldConflict409PtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.73");
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var task = await mediator.Send(new CreateTaskCommand("Trocar SFP", "Corrective"));
        await mediator.Send(new LinkTaskDeviceCommand(task.Id, device.Id));

        var action = () => mediator.Send(new DeleteDeviceCommand(device.Id));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o device: existem tasks vinculadas.");
    }

    [Fact]
    public async Task LinkDevice_WithoutTasksManage_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoPermissions();

        var action = () => mediator.Send(
            new LinkTaskDeviceCommand(Guid.NewGuid(), Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Permissões ausentes: TasksManage");
    }
}
