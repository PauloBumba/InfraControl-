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
/// F11-T003 / ADR-013: ciclo de vida OpenRdp → CloseRemote no pipeline
/// MediatR real (autorização RemoteRdpConnect + auditoria + transação).
/// Provider RDP é falso — nenhum teste abre conexão de rede. O fluxo RDP
/// não consulta credencial (probe sem autenticação).
/// </summary>
public sealed class RdpSessionIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task OpenAndCloseRdp_WithAdminRole_ShouldCreateActiveThenClosedSession()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.73");
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var opened = await mediator.Send(new OpenRdpSessionCommand(device.Id));
        opened.Protocol.Should().Be("Rdp");
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
    public async Task OpenRdp_WithoutRole_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.74");
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new OpenRdpSessionCommand(device.Id));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
        (await dbContext.RemoteSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CloseRemote_WithoutRole_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.75");
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(
            new CloseRemoteSessionCommand(device.Id, Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

    [Fact]
    public async Task OpenRdp_DeviceWithoutIp_ShouldThrowArgumentExceptionPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: null);
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new OpenRdpSessionCommand(device.Id));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*não possui endereço IP*");
        (await dbContext.RemoteSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OpenRdp_UnknownDevice_ShouldThrowNotFoundPtBr()
    {
        using var scope = CreateScope();
        var unknownId = Guid.NewGuid();
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new OpenRdpSessionCommand(unknownId));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknownId}' não encontrado.");
    }
}
