using FluentAssertions;
using InfraControl.Application.Identity;
using InfraControl.IntegrationTests.Common;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Seed;

/// <summary>
/// F46-T001 — seed de dados mock para diagnóstico ponta-a-ponta.
/// Regras: tudo marcado (nomes com [MOCK], e-mails @mock.test) para o
/// operador identificar/limpar; idempotente (rodar N× não duplica); o purge
/// remove SÓ o marcado e preserva dados reais do banco.
/// </summary>
public sealed class MockDataSeederTests : BaseIntegrationTest
{
    private static MockDataSeeder CreateSeeder(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<InfraControlUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        return new MockDataSeeder(db, users, roles);
    }

    [Fact]
    public async Task Seed_ShouldPopulateEveryMarkedModule()
    {
        using var scope = CreateScope();
        var seeder = CreateSeeder(scope);

        var result = await seeder.SeedAsync();

        result.UsersCreated.Should().BeGreaterThanOrEqualTo(3,
            "admin/operator/viewer mock cobrem a matriz de papéis do E2E");
        result.LocationsCreated.Should().BeGreaterThanOrEqualTo(2);
        result.DevicesCreated.Should().BeGreaterThanOrEqualTo(6);
        result.IncidentsCreated.Should().BeGreaterThanOrEqualTo(4);
        result.AlertsCreated.Should().BeGreaterThanOrEqualTo(3);
        result.TasksCreated.Should().BeGreaterThanOrEqualTo(4);
        result.AuditEntriesCreated.Should().BeGreaterThanOrEqualTo(1);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Devices.CountAsync(d => d.Name.StartsWith(MockDataOptions.Marker)))
            .Should().Be(result.DevicesCreated);
        (await db.Incidents.CountAsync(i => i.Title.StartsWith(MockDataOptions.Marker)))
            .Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task Seed_RunTwice_ShouldNotDuplicateAnything()
    {
        using var scope = CreateScope();
        var seeder = CreateSeeder(scope);

        var first = await seeder.SeedAsync();
        var second = await seeder.SeedAsync();

        second.UsersCreated.Should().Be(0, "idempotência: usuários já existem");
        second.LocationsCreated.Should().Be(0);
        second.DevicesCreated.Should().Be(0);
        second.IncidentsCreated.Should().Be(0);
        second.AlertsCreated.Should().Be(0);
        second.TasksCreated.Should().Be(0);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var devices = await db.Devices.CountAsync(d => d.Name.StartsWith(MockDataOptions.Marker));
        devices.Should().Be(first.DevicesCreated);
    }

    [Fact]
    public async Task Seed_ShouldCreateLoggableIdentityUsers()
    {
        using var scope = CreateScope();
        var seeder = CreateSeeder(scope);
        await seeder.SeedAsync();

        var users = scope.ServiceProvider.GetRequiredService<UserManager<InfraControlUser>>();
        var admin = await users.FindByEmailAsync($"admin@{MockDataOptions.EmailDomain}");

        admin.Should().NotBeNull("o login pelo front precisa funcionar com o seed");
        (await users.CheckPasswordAsync(admin!, MockDataOptions.Password)).Should().BeTrue();
        (await users.IsInRoleAsync(admin!, "Admin")).Should().BeTrue();
    }

    [Fact]
    public async Task Purge_ShouldRemoveOnlyMarkedData_AndBeIdempotent()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Dado "real" que NÃO pode morrer no purge.
        var realLocation = Domain.Entities.Location.Create("Sala Producao Real");
        db.Locations.Add(realLocation);
        db.Devices.Add(Domain.Entities.Device.Create(
            realLocation.Id, "switch-real", Domain.Enums.DeviceType.Switch, ip: "10.0.0.1"));
        await db.SaveChangesAsync();

        var seeder = CreateSeeder(scope);
        await seeder.SeedAsync();

        var purged = await seeder.PurgeAsync();
        purged.Should().BeGreaterThan(0, "há dados marcados para remover");

        var db2 = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db2.Devices.CountAsync(d => d.Name.StartsWith(MockDataOptions.Marker)))
            .Should().Be(0, "devices mock somem");
        (await db2.Incidents.CountAsync(i => i.Title.StartsWith(MockDataOptions.Marker)))
            .Should().Be(0, "incidents mock somem");
        (await db2.Locations.AnyAsync(l => l.Id == realLocation.Id))
            .Should().BeTrue("dados reais são preservados");
        (await db2.Devices.AnyAsync(d => d.Name == "switch-real"))
            .Should().BeTrue("device real sobrevive ao purge");

        var secondPurge = await seeder.PurgeAsync();
        secondPurge.Should().Be(0, "purge é idempotente");
    }
}
