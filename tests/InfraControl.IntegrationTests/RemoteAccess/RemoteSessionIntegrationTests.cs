using FluentAssertions;
using InfraControl.Application.RemoteAccess.Sessions;
using InfraControl.Domain.Enums;
using InfraControl.Domain.RemoteAccess;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.RemoteAccess;

/// <summary>
/// F11-T001 / ADR-013: histórico de RemoteSession no PostgreSQL real —
/// autorização por role no pipeline MediatR (RemoteSessionsRead) e leitura
/// das sessões com ordenação. Nenhum teste usa rede.
/// </summary>
public sealed class RemoteSessionIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task GetDeviceSessions_WithAdminRole_ShouldListSessionsNewestFirst()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.30");
        await SeedSessionsAsync(dbContext, device.Id, count: 2);
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dto = await mediator.Send(new GetDeviceSessionsQuery(device.Id));

        dto.DeviceId.Should().Be(device.Id);
        dto.Sessions.Should().HaveCount(2);
        dto.Sessions.Select(session => session.StartedAt)
            .Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetDeviceSessions_WithoutRemoteSessionsRead_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.31");
        await SeedSessionsAsync(dbContext, device.Id, count: 1);
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new GetDeviceSessionsQuery(device.Id));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*RemoteSessionsRead*");
    }

    private static async Task SeedSessionsAsync(
        AppDbContext dbContext, Guid deviceId, int count)
    {
        var start = new DateTime(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < count; i++)
        {
            var session = RemoteSession.Open(deviceId, Guid.NewGuid(), RemoteProtocol.Ssh);
            dbContext.RemoteSessions.Add(session);
            await dbContext.SaveChangesAsync();

            var entry = dbContext.Entry(session);
            entry.Property(nameof(RemoteSession.StartedAt)).CurrentValue =
                start.AddMinutes(i);
            entry.State = EntityState.Modified;
            await dbContext.SaveChangesAsync();
        }
    }
}
