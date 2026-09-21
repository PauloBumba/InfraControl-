using FluentAssertions;
using InfraControl.Application.Identity;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfraControl.UnitTests.Identity;

public sealed class AdminBootstrapperTests
{
    [Fact]
    public async Task RunAsync_ShouldCreateOnlyOneAdmin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAdminBootstrapper>();

        var firstRun = await bootstrapper.RunAsync();
        var secondRun = await bootstrapper.RunAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<InfraControlUser>>();

        firstRun.Created.Should().BeTrue();
        secondRun.AlreadyConfigured.Should().BeTrue();
        (await userManager.GetUsersInRoleAsync("Admin")).Should().ContainSingle();
        (await userManager.FindByEmailAsync("admin@example.com")).Should().NotBeNull();
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<InfraControlUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddSingleton(new BootstrapAdminOptions
        {
            Enabled = true,
            Email = "admin@example.com",
            DisplayName = "Initial Admin",
            Password = "ChangeMe!123456"
        });
        services.AddScoped<IAdminIdentityProvisioner, IdentityAdminProvisioner>();
        services.AddScoped<IAdminBootstrapper, AdminBootstrapper>();
        services.AddScoped<IAdminBootstrapper, AdminBootstrapper>();
        return services.BuildServiceProvider();
    }
}
