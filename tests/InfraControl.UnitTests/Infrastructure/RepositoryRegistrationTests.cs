using System.Reflection;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.UnitTests.Infrastructure;

// F0-T024: especificação do registro automático de repositórios (estilo
// MediatR). O registrador deve varrer interfaces I*Repository (Application)
// e classes *Repository (Infrastructure), registrar Scoped com TryAdd e
// falhar no startup (fail-fast) quando o par estiver ausente.
public sealed class RepositoryRegistrationTests
{
    private static readonly Assembly InterfaceAssembly = typeof(IDeviceRepository).Assembly;
    private static readonly Assembly ImplementationAssembly = typeof(DeviceRepository).Assembly;

    [Fact]
    public void AddRepositoryRegistrations_RealAssemblies_ShouldRegisterEveryInterfaceAsScoped()
    {
        var services = new ServiceCollection();

        services.AddRepositoryRegistrations(InterfaceAssembly, ImplementationAssembly);

        var repositoryInterfaces = InterfaceAssembly.GetTypes()
            .Where(type => type.IsInterface
                && !type.IsGenericTypeDefinition
                && type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToArray();

        repositoryInterfaces.Should().NotBeEmpty();
        foreach (var repositoryInterface in repositoryInterfaces)
        {
            var descriptors = services
                .Where(descriptor => descriptor.ServiceType == repositoryInterface)
                .ToArray();

            descriptors.Should().HaveCount(1,
                $"o repositório {repositoryInterface.Name} deve ser registrado exatamente uma vez");

            var descriptor = descriptors[0];
            descriptor.ImplementationType.Should().NotBeNull();
            repositoryInterface.IsAssignableFrom(descriptor.ImplementationType)
                .Should().BeTrue(
                    $"{descriptor.ImplementationType!.Name} deve implementar {repositoryInterface.Name}");
            descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
        }
    }

    [Fact]
    public void AddRepositoryRegistrations_InterfaceWithoutImplementation_ShouldThrowWithName()
    {
        var services = new ServiceCollection();

        // Assembly de testes contém IOrphanRepository sem implementação;
        // Application não possui classes *Repository → par ausente.
        var act = () => services.AddRepositoryRegistrations(
            typeof(RepositoryRegistrationTests).Assembly,
            typeof(IDeviceRepository).Assembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IOrphanRepository*");
    }

    [Fact]
    public void AddRepositoryRegistrations_ManuallyRegistered_ShouldNotAddSecondDescriptor()
    {
        var services = new ServiceCollection();
        services.AddScoped<IInvitationRepository, InvitationRepository>();

        services.AddRepositoryRegistrations(InterfaceAssembly, ImplementationAssembly);

        services.Count(descriptor => descriptor.ServiceType == typeof(IInvitationRepository))
            .Should().Be(1, "o registro manual deve prevalecer (TryAdd) sem dobrar o descriptor");
    }

    [Fact]
    public async Task AddRepositoryRegistrations_ShouldResolveRepositoriesFromProvider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IDeviceRepository>()
            .Should().BeOfType<DeviceRepository>();
        scope.ServiceProvider.GetRequiredService<ISwitchPortRepository>()
            .Should().BeOfType<SwitchPortRepository>();
        scope.ServiceProvider.GetRequiredService<IInvitationRepository>()
            .Should().BeOfType<InvitationRepository>();
        scope.ServiceProvider.GetRequiredService<IOutboxRepository>()
            .Should().BeOfType<OutboxRepository>();
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddRepositoryRegistrations(
            typeof(IDeviceRepository).Assembly,
            typeof(DeviceRepository).Assembly);
        return services.BuildServiceProvider();
    }
}

/// <summary>
/// Espécime proposital SEM implementação: o registrador deve falhar
/// (fail-fast) apontando o nome da interface — nunca em runtime na
/// primeira requisição.
/// </summary>
public interface IOrphanRepository;
