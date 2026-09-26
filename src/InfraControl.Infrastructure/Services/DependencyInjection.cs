using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Common.Behaviors;
using InfraControl.Application.Identity;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Administration;
using InfraControl.Application.Identity.Roles.Ports;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.IpAddresses.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Inventory.MacAddresses.Ports;
using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.Application.Inventory.Racks.Ports;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Domain.Common;
using InfraControl.Infrastructure.Caching;
using InfraControl.Infrastructure.Services;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;
using InfraControl.Infrastructure.RemoteAccess;
using InfraControl.Infrastructure.Repositories;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace InfraControl.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";

        var rabbitMqHost = configuration.GetConnectionString("RabbitMQ") ?? "localhost";
        var rabbitMqUser = configuration["RabbitMQ:User"] ?? "infracontrol";
        var rabbitMqPassword = configuration["RabbitMQ:Password"] ?? "infracontrol_dev_2026";

        // TimeProvider — São Paulo timezone
        var saoPauloProvider = new SaoPauloTimeProvider();
        AppTimeProvider.Current = saoPauloProvider;
        services.AddSingleton<TimeProvider>(saoPauloProvider);

        // PostgreSQL
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Identity foundation. Authentication schemes and endpoints are separate Tasks.
        services.AddIdentityCore<InfraControlUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IIdentityAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IIdentityEmailSender, LoggingIdentityEmailSender>();
        services.AddScoped<IIdentityAdministrationService, IdentityAdministrationService>();
        services.AddScoped<IAdminIdentityProvisioner, IdentityAdminProvisioner>();

        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IConnectionFactory>(_ =>
            new ConnectionFactory(connectionString));

        // Redis — AbortOnConnectFail=false: a aplicação sobe mesmo com o Redis
        // fora e degrada via HybridCacheService (ADR-010).
        var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
        redisOptions.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));

        // Cache híbrido (ADR-010): Redis primário + fallback em memória.
        // IIdempotencyStore permanece só no Redis — operação crítica,
        // fail-closed (docs/08: "política mais restritiva").
        services.AddSingleton<MemoryFallbackStore>();
        services.AddScoped<RedisCacheService>();
        services.AddScoped<IRedisCacheStore>(provider =>
            provider.GetRequiredService<RedisCacheService>());
        services.AddScoped<ICacheService, HybridCacheService>();
        services.AddScoped<IIdempotencyStore>(provider =>
            provider.GetRequiredService<RedisCacheService>());
        services.AddScoped<IRateLimiter, RedisRateLimiter>();

        // Pipeline transversal: cada behavior é opt-in quando requer um contrato específico.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CorrelationIdBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ObservabilityBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RateLimitingBehavior<,>));

        // RabbitMQ via MassTransit
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitMqHost, "/", h =>
                {
                    h.Username(rabbitMqUser);
                    h.Password(rabbitMqPassword);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IEventBus, MassTransitEventBus>();
        services.AddScoped<IResilienceExecutor, ResilienceExecutor>();
        services.AddScoped<IFeatureFlagProvider, FeatureFlagProvider>();
        services.AddScoped(typeof(IProviderRegistry<>), typeof(ProviderRegistry<>));

        // Monitoring (F5-T003/F5-T004): ping ICMP + SNMP de impressoras
        services.AddScoped<IIcmpClient, SystemNetIcmpClient>();
        // Discovery (F4-T003/T004): probes ICMP e SNMP do gatilho manual
        services.AddScoped<IDiscoveryProvider, IcmpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, SnmpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, LldpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, CdpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, ArpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, DhcpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, MacTableDiscoveryProvider>();
        services.AddScoped<IMonitoringProvider, PingMonitoringProvider>();
        services.Configure<SnmpOptions>(configuration.GetSection(SnmpOptions.SectionName));
        services.AddSingleton<ISnmpClient, SharpSnmpSnmpClient>();
        services.AddScoped<IMonitoringProvider, SnmpTonerProvider>();
        services.AddScoped<IMonitoringProvider, SnmpDrumProvider>();

        // Remote access (F11-T002/T003, ADR-013): SSH sem terminal interativo
        // + probe RDP TCP/X.224. Credencial: store por device cifrado com
        // chave em env (F11-T006) e fallback global RemoteAccess:Ssh; RDP
        // não usa credencial.
        services.Configure<RemoteSshOptions>(
            configuration.GetSection(RemoteSshOptions.SectionName));
        services.Configure<RemoteRdpOptions>(
            configuration.GetSection(RemoteRdpOptions.SectionName));
        services.Configure<CredentialEncryptionOptions>(
            configuration.GetSection(CredentialEncryptionOptions.SectionName));
        services.AddScoped<IRemoteProvider, SshRemoteProvider>();
        services.AddScoped<IRemoteProvider, RdpRemoteProvider>();
        services.AddScoped<ICredentialProtector, AesGcmCredentialProtector>();
        services.AddScoped<ConfigurationRemoteCredentialSource>();
        services.AddScoped<IRemoteCredentialSource, StoreRemoteCredentialSource>();
        services.AddSingleton<IRemoteConnectionRegistry, RemoteConnectionRegistry>();

        // Outbox
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IAuditReader, AuditReader>();
        services.AddScoped<IDomainEventService, DomainEventService>();

        // Retention / cleanup
        services.Configure<RetentionOptions>(configuration.GetSection("Retention"));

        // Performance (Dia 5 — docs/20): alerta de request lento
        services.Configure<PerformanceOptions>(configuration.GetSection("Performance"));
        services.AddScoped<ICleanupService, CleanupService>();

        // Invitations
        services.AddScoped<IInvitationAccountPort, InvitationAccountPort>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IRackRepository, RackRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<INetworkInterfaceRepository, NetworkInterfaceRepository>();
        services.AddScoped<IMonitoringCheckRepository, MonitoringCheckRepository>();
        services.AddScoped<IMonitoringResultRepository, MonitoringResultRepository>();
        services.AddScoped<IRemoteSessionRepository, RemoteSessionRepository>();

        // F0-T024: registro automático de todos os pares I*Repository (Application)
        // ↔ *Repository (Infrastructure) por varredura de assembly — fail-fast no
        // startup se o par faltar; TryAdd faz qualquer registro manual prevalecer
        // (escape hatch para lifetime/fábrica especiais).
        services.AddRepositoryRegistrations(
            typeof(IDeviceRepository).Assembly,
            typeof(DeviceRepository).Assembly);

        return services;
    }
}
