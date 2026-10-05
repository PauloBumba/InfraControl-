using InfraControl.Application;
using InfraControl.Application.Alerts;
using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Common.Behaviors;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Runs;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.Application.Monitoring.Checks;
using InfraControl.Application.Monitoring.Events;
using InfraControl.Application.Monitoring.Events.Ports;
using InfraControl.Application.Network.Connections.Ports;
using InfraControl.Application.Operations.Retention.Ports;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.Application.Identity.Administration;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Monitoring;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.RemoteAccess;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// MON-009 (docs/21): base dos integration tests — PostgreSQL real via
/// Testcontainers + pipeline MediatR do aplicativo (Authorization/Audit/
/// Transaction behaviors) + repositórios Infrastructure reais. Sem rede:
/// ICMP é falso; sem Redis/RabbitMQ (behaviors opt-in não registrados).
/// </summary>
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    // Imagem já presente localmente — evita pull do Docker Hub (exige auth/rate limit).
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    protected ServiceProvider Services { get; private set; } = null!;

    /// <summary>Connection do container — usada no round-trip de backup/restore (F12-T004).</summary>
    protected string PostgresConnectionString => _postgres.GetConnectionString();
    protected TestCurrentUserContext CurrentUser { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        CurrentUser = new TestCurrentUserContext();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        // Banco real (PostgreSQL do container)
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString()));

        // Identity foundation (mínimo para DomainUsers funcionar)
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

        // Pipeline MediatR do aplicativo — mesmo escopo do Program.cs
        services.AddApplicationMediatR();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CorrelationIdBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));

        // Infraestrutura real (repositórios + auditoria)
        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IAuditWriter, AuditWriter>();
        // F7-T002: seed de DomainUsers dispara UserCreatedDomainEvent →
        // OutboxDispatchHandler precisa do serviço real (grava OutboxMessages,
        // sem broker).
        services.AddScoped<IDomainEventService, DomainEventService>();
        // F1-T018: IdentityAdministrationService para CRUD de usuários
        services.AddScoped<IIdentityAdministrationService, IdentityAdministrationService>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        // F19-T006: identidade externa estável do device (MAC/Device ID)
        services.AddScoped<IExternalDeviceReferenceRepository, ExternalDeviceReferenceRepository>();
        services.AddScoped<IMonitoringCheckRepository, MonitoringCheckRepository>();
        services.AddScoped<IMonitoringResultRepository, MonitoringResultRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        // ADR-027: pipeline Zabbix → MonitoringEvent → Alert → Incident
        services.AddScoped<IMonitoringEventRepository, MonitoringEventRepository>();
        services.AddScoped<IMonitoringEventIngestor, MonitoringEventIngestor>();
        services.AddScoped<IUpstreamCorrelationRepository, UpstreamCorrelationRepository>();
        services.AddScoped<IRootCauseResolver, RootCauseResolver>();
        services.AddScoped<IAlertEvaluator, AlertEvaluator>();
        services.AddScoped<IRemoteSessionRepository, RemoteSessionRepository>();

        // Monitoring: registry real + PingMonitoringProvider com ICMP falso
        services.AddScoped(typeof(IProviderRegistry<>), typeof(ProviderRegistry<>));
        services.AddScoped<IIcmpClient, FakeIcmpClient>();
        services.AddScoped<IMonitoringProvider, PingMonitoringProvider>();
        services.AddScoped<IHttpProbeClient, FakeHttpProbeClient>();
        services.AddScoped<IMonitoringProvider, HttpWanMonitoringProvider>();
        services.AddScoped<ITcpProbeClient, FakeTcpProbeClient>();
        services.AddScoped<IMonitoringProvider, TcpMonitoringProvider>();
        services.AddScoped<InfraControl.Application.Common.Interfaces.IDiscoveryProvider, IcmpDiscoveryProvider>();
        services.AddScoped<ISnmpClient, FakeSnmpClient>();
        services.AddScoped<InfraControl.Application.Common.Interfaces.IDiscoveryProvider, SnmpDiscoveryProvider>();
        services.AddScoped<InfraControl.Application.Common.Interfaces.IDiscoveryProvider, LldpDiscoveryProvider>();
        services.AddScoped<InfraControl.Application.Common.Interfaces.IDiscoveryProvider, CdpDiscoveryProvider>();
        services.AddScoped<InfraControl.Application.Common.Interfaces.IDiscoveryProvider, ArpDiscoveryProvider>();
        services.AddScoped<InfraControl.Application.Common.Interfaces.IDiscoveryProvider, DhcpDiscoveryProvider>();
        services.AddScoped<InfraControl.Application.Common.Interfaces.IDiscoveryProvider, MacTableDiscoveryProvider>();

        // Remote access (F11-T002/T003): providers SSH/RDP falsos (sem rede)
        // + credencial provisória de teste + registry real em memória
        services.AddScoped<IRemoteProvider, FakeSshRemoteProvider>();
        services.AddScoped<IRemoteProvider, FakeRdpRemoteProvider>();
        services.Configure<RemoteSshOptions>(options =>
        {
            options.Username = "it-ssh";
            options.Password = "S3nh@Teste!123";
        });
        services.Configure<RemoteRdpOptions>(options =>
        {
            options.Port = 3389;
            options.TimeoutSeconds = 5;
        });
        // F11-T006: cadeia real — store cifrado (chave de teste em config)
        // + fallback global de teste
        services.AddScoped<ICredentialProtector, AesGcmCredentialProtector>();
        services.AddScoped<IDeviceCredentialRepository, DeviceCredentialRepository>();
        services.AddScoped<IRetentionPolicyRepository, RetentionPolicyRepository>();
        // F7-T001: CRUD de Tasks (repositório segue convenção I*Repository)
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        services.AddScoped<ITaskAssigneeRepository, TaskAssigneeRepository>();
        services.AddScoped<ITaskDeviceRepository, TaskDeviceRepository>();
        services.AddScoped<ITaskIncidentRepository, TaskIncidentRepository>();
        services.AddScoped<ITaskCommentRepository, TaskCommentRepository>();
        services.AddScoped<InfraControl.Application.Tasks.History.Ports.ITaskHistoryRepository,
            TaskHistoryRepository>();
        services.AddScoped<InfraControl.Application.Tasks.History.Ports.ITaskHistoryWriter,
            InfraControl.Application.Tasks.History.TaskHistoryWriter>();
        // F8-T001: CRUD de Services (SLA/SLO — a base não faz varredura automática)
        services.AddScoped<InfraControl.Application.Sla.Services.Ports.IServiceRepository,
            ServiceRepository>();
        services.AddScoped<InfraControl.Application.Sla.Policies.Ports.ISlaPolicyRepository,
            SlaPolicyRepository>();
        services.AddScoped<InfraControl.Application.Sla.TaskSlas.Ports.ITaskSlaRepository,
            TaskSlaRepository>();
        services.AddScoped<InfraControl.Application.Sla.TaskSlas.Ports.ITaskSlaPauseRepository,
            TaskSlaPauseRepository>();
        services.AddScoped<InfraControl.Application.Sla.Slos.Ports.ISloRepository,
            SloRepository>();
        services.AddScoped<InfraControl.Application.Sla.Slos.Measurements.Ports.ISloMeasurementRepository,
            SloMeasurementRepository>();
        services.AddScoped<InfraControl.Application.Sla.Slos.Budgets.Ports.ISloBudgetRepository,
            SloBudgetRepository>();
        services.AddScoped<InfraControl.Application.Notifications.Recipients.Ports.INotificationRecipientRepository,
            NotificationRecipientRepository>();
        // F18-T001: feed do usuário (a base não faz varredura automática)
        services.AddScoped<InfraControl.Application.Notifications.Feed.Ports.INotificationFeedRepository,
            NotificationFeedRepository>();
        services.AddScoped<InfraControl.Application.Notifications.Preferences.Ports.INotificationPreferenceRepository,
            NotificationPreferenceRepository>();
        services.AddScoped<InfraControl.Application.Notifications.Deliveries.Ports.INotificationDeliveryRepository,
            NotificationDeliveryRepository>();
        services.AddScoped<InfraControl.Application.Knowledge.Categories.Ports.IKnowledgeCategoryRepository,
            KnowledgeCategoryRepository>();
        services.AddScoped<InfraControl.Application.Knowledge.Articles.Ports.IKnowledgeArticleRepository,
            KnowledgeArticleRepository>();
        services.AddScoped<InfraControl.Application.Knowledge.Articles.Ports.IKnowledgeArticleDeviceRepository,
            KnowledgeArticleDeviceRepository>();
        services.AddScoped<InfraControl.Application.Knowledge.Articles.Ports.IKnowledgeArticleIncidentRepository,
            KnowledgeArticleIncidentRepository>();
        services.AddScoped<InfraControl.Application.Knowledge.Articles.Ports.IKnowledgeArticleServiceRepository,
            KnowledgeArticleServiceRepository>();
        // F4-T001/T002: leitura de discovery (a base não faz varredura automática)
        services.AddScoped<InfraControl.Application.Discovery.Runs.Ports.IDiscoveryRunRepository, DiscoveryRunRepository>();
        services.AddScoped<InfraControl.Application.Discovery.Results.Ports.IDiscoveryResultRepository, DiscoveryResultRepository>();
        // F3-T005b: ingestão de Connections ao fim do run (repos reais)
        services.AddScoped<INeighborConnectionImporter, NeighborConnectionImporter>();
        services.AddScoped<INetworkInterfaceRepository, NetworkInterfaceRepository>();
        services.AddScoped<IConnectionRepository, ConnectionRepository>();
        services.AddScoped<ConfigurationRemoteCredentialSource>();
        services.AddScoped<IRemoteCredentialSource, StoreRemoteCredentialSource>();
        services.Configure<CredentialEncryptionOptions>(options =>
        {
            options.Key = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
        });
        // F12-T002: defaults de retenção (sem seção — mesmas values do appsettings)
        services.Configure<RetentionOptions>(_ => { });
        // F12-T005: alerta de request lento (Dia 5 — docs/20)
        services.Configure<PerformanceOptions>(options => options.SlowRequestMs = 1000);
        services.AddSingleton<IRemoteConnectionRegistry, RemoteConnectionRegistry>();

        // Usuário de teste (roles são a fonte da verdade, ADR-016)
        services.AddSingleton<ICurrentUserContext>(CurrentUser);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (Services is not null)
            await Services.DisposeAsync();

        await _postgres.DisposeAsync();
    }

    protected IServiceScope CreateScope() => Services.CreateScope();

    /// <summary>Aplica a role do usuário como no login real (claim <c>ClaimTypes.Role</c>).</summary>
    protected void UseRole(UserRole role)
    {
        CurrentUser.Roles = [role.ToString()];
    }

    protected void UseNoRoles()
    {
        CurrentUser.Roles = [];
    }

    protected static async Task<Domain.Entities.Device> SeedDeviceAsync(
        AppDbContext dbContext, string? ip, string name = "sw-it")
    {
        var location = Domain.Entities.Location.Create("Sala Integracao");
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync();

        var device = Domain.Entities.Device.Create(
            location.Id, name, DeviceType.Switch, ip: ip);
        dbContext.Devices.Add(device);
        await dbContext.SaveChangesAsync();
        return device;
    }
}
