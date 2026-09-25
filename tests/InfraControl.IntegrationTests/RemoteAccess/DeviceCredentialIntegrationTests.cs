using FluentAssertions;
using InfraControl.Application.RemoteAccess.Credentials;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.RemoteAccess;

/// <summary>
/// F11-T006: Credential Store no PostgreSQL real — upsert/rotação/delete
/// com autorização CredentialManage (Admin-only) e DTO sem senha.
/// Nenhum teste abre rede.
/// </summary>
public sealed class DeviceCredentialIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task UpsertAndGet_WithAdminRole_ShouldStoreAndReturnUsernameWithoutPassword()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.80");
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var saved = await mediator.Send(new UpsertDeviceCredentialCommand(
            device.Id, "svc-backup", "SenhaForte!1"));
        var dto = await mediator.Send(new GetDeviceCredentialQuery(device.Id));

        saved.HasCredential.Should().BeTrue();
        dto.Username.Should().Be("svc-backup");
        dto.HasCredential.Should().BeTrue();

        var stored = await dbContext.DeviceCredentials
            .SingleAsync(credential => credential.DeviceId == device.Id);
        stored.ProtectedPassword.Should().NotContain("SenhaForte!1");
    }

    [Fact]
    public async Task Upsert_WithUserRole_ShouldThrowUnauthorizedCredentialManage()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.81");
        UseRole(UserRole.User);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new UpsertDeviceCredentialCommand(
            device.Id, "svc", "Senha!1"));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*CredentialManage*");
        (await dbContext.DeviceCredentials.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Upsert_Twice_ShouldRotateWithRotatedAt()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.82");
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new UpsertDeviceCredentialCommand(
            device.Id, "svc-antigo", "SenhaAntiga!1"));
        var rotated = await mediator.Send(new UpsertDeviceCredentialCommand(
            device.Id, "svc-novo", "SenhaNova!2"));

        rotated.Username.Should().Be("svc-novo");
        rotated.RotatedAt.Should().NotBeNull();
        (await dbContext.DeviceCredentials.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delete_WithAdminRole_ShouldRemoveCredentialThenSecondDeleteIs404()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.83");
        UseRole(UserRole.Admin);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new UpsertDeviceCredentialCommand(
            device.Id, "svc", "Senha!1"));
        await mediator.Send(new DeleteDeviceCredentialCommand(device.Id));

        (await dbContext.DeviceCredentials.CountAsync()).Should().Be(0);
        var action = () => mediator.Send(new DeleteDeviceCredentialCommand(device.Id));
        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task Get_WithViewerRole_ShouldThrowUnauthorizedCredentialManage()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.84");
        UseRole(UserRole.Viewer);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var action = () => mediator.Send(new GetDeviceCredentialQuery(device.Id));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*CredentialManage*");
    }
}
