using FluentAssertions;
using InfraControl.Application.RemoteAccess.Sessions;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.RemoteAccess;

/// <summary>
/// F11-T002/T003 / ADR-013: ciclo de vida OpenSsh → CloseRemote no pipeline
/// MediatR real (autorização RemoteSshConnect + auditoria + transação). Provider SSH
/// é falso — nenhum teste abre conexão de rede.
/// </summary>
public sealed class SshSessionIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task OpenAndClose_WithAdminRole_ShouldCreateActiveThenClosedSession()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.70");
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var opened = await mediator.Send(new OpenSshSessionCommand(device.Id));
        opened.Protocol.Should().Be("Ssh");
        opened.Status.Should().Be("Active");
        opened.UserId.Should().Be(CurrentUser.UserId!.Value);

        var closed = await mediator.Send(new CloseRemoteSessionCommand(device.Id, opened.Id));
        closed.Status.Should().Be("Closed");
        closed.EndedAt.Should().NotBeNull();

        var history = await mediator.Send(new GetDeviceSessionsQuery(device.Id));
        history.Sessions.Should().ContainSingle()
            .Which.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task OpenAndClose_WithUserRole_ShouldCreateActiveThenClosedSession()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.73");
        UseRole(UserRole.User);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var opened = await mediator.Send(new OpenSshSessionCommand(device.Id));
        opened.Status.Should().Be("Active");

        var closed = await mediator.Send(new CloseRemoteSessionCommand(device.Id, opened.Id));
        closed.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task OpenSsh_WithoutRemoteSshConnect_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.71");
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new OpenSshSessionCommand(device.Id));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*RemoteSshConnect*");
        (await dbContext.RemoteSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CloseRemote_WithoutRemoteSshConnect_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.72");
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(
            new CloseRemoteSessionCommand(device.Id, Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*RemoteSshConnect*");
    }

    [Fact]
    public async Task OpenSsh_DeviceWithoutIp_ShouldThrowArgumentExceptionPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: null);
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new OpenSshSessionCommand(device.Id));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*não possui endereço IP*");
        (await dbContext.RemoteSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OpenSsh_UnknownDevice_ShouldThrowNotFoundPtBr()
    {
        using var scope = CreateScope();
        var unknownId = Guid.NewGuid();
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new OpenSshSessionCommand(unknownId));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }
}
