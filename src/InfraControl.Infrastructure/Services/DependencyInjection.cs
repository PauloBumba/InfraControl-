using InfraControl.Application.Notifications.Email;
using InfraControl.Application.Notifications.Webhook;
using InfraControl.Application.Notifications.Slack;
using InfraControl.Application.Notifications.Telegram;
using InfraControl.Application.Notifications.WhatsApp;
using Microsoft.Extensions.Http.Resilience;
using InfraControl.Application.Alerts;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Context;
using InfraControl.Application.AI.Evaluation;
using InfraControl.Application.AI.Health;
using InfraControl.Application.AI.Tools;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Common.Behaviors;
using InfraControl.Application.Discovery.Runs;
using InfraControl.Application.Identity;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Administration;
using InfraControl.Application.Identity.Sessions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.IpAddresses.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Inventory.MacAddresses.Ports;
using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.Application.Inventory.Racks.Ports;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.Application.Monitoring.Events;
using InfraControl.Application.Monitoring.Polling;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Application.Pdi.Ports;
using InfraControl.Application.Projects.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Application.Network.Services.Ports;
using InfraControl.Domain.Common;
using InfraControl.Infrastructure.AI;
using InfraControl.Infrastructure.Caching;
using InfraControl.Infrastructure.Services;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Discovery;
using InfraControl.Infrastructure.Integrations.Unifi;
using InfraControl.Infrastructure.Integrations.Zabbix;
using InfraControl.Infrastructure.Monitoring;
using InfraControl.Infrastructure.NetworkServices;
using InfraControl.Infrastructure.NetworkServices.Ports;
using InfraControl.Infrastructure.RemoteAccess;
using InfraControl.Infrastructure.Repositories;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace InfraControl.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        // MassTransit só aceita UMA chamada de AddMassTransit por container de DI:
        // consumers e observers do host (Workers) entram por aqui, na mesma
        // chamada que configura o bus — uma segunda chamada no Program do host
        // derruba o boot com MassTransit.ConfigurationException.
        Action<IBusRegistrationConfigurator>? configureBus = null)
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

        // ADR-018: dá autoridade ao cookie. Sem este registro o
        // ActiveSessionMiddleware não tem o que consultar.
        services.AddScoped<IActiveSessionReader, ActiveSessionReader>();

        // F1-T016b (ADR-020): auto-serviço de sessões — lista, revogação
        // individual e "sair de todos os outros", com a invalidação do cache
        // compartilhado que derruba o cookie na hora.
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IIdentityEmailSender, LoggingIdentityEmailSender>();
        services.AddScoped<IEmailNotificationDispatcher, LoggingEmailNotificationDispatcher>();

        // F19-T007/ADR-028 §6: POST real no n8n (substitui o adaptador de
        // log). Resiliência Polly no handler do IHttpClientFactory — retry
        // 3× backoff exponencial (1s, 2s, 4s) com jitter + circuit breaker
        // (5 falhas em 30s → circuito aberto por 60s). REGRA (regra 7): o
        // n8n só recebe a notificação; o estado do Incidente é 100% do
        // InfraControl — n8n nunca decide Incident = Resolved.
        services.AddScoped<IWebhookNotificationDispatcher, HttpWebhookNotificationDispatcher>();
        services.AddHttpClient(HttpWebhookNotificationDispatcher.HttpClientName)
            .AddResilienceHandler(
                "n8n-webhook",
                static (builder, _) => HttpWebhookNotificationDispatcher.ConfigureResilience(builder));

        // F20-T001/ADR-028 §2/§6 + Fase 1 (F26-T001): cliente REST de saída
        // do UniFi Controller — login/detecção de estilo (UniFi OS ×
        // standalone) + re-autenticação em 401 ficam no próprio client; aqui o
        // named client com a MESMA política Polly do dispatcher n8n (retry/
        // backoff + circuit breaker) e o handler primário que honra a opção
        // AllowInvalidTlsCertificate (self-signed de controlador local —
        // docs/35 §2; default = validação estrita).
        // F26 Fase 2.2: config administrativa persistida (linha única no
        // banco, senha AES-GCM) + fábrica de cliente com opções específicas;
        // env continua como fallback enquanto não houver linha no banco.
        services.AddScoped<IUniFiSettingsRepository, UniFiSettingsRepository>();
        services.AddScoped<IUniFiSitesRepository, UniFiSitesRepository>();
        services.AddScoped<IUniFiSyncRunsRepository, UniFiSyncRunsRepository>();
        services.AddScoped<IUnifiEnvOptionsAccessor, UnifiEnvOptionsAccessor>();
        services.AddScoped<IUnifiClientFactory, UnifiClientFactory>();
        services.AddScoped<IUniFiOptionsResolver, UniFiOptionsResolver>();

        // Cliente "padrão" resolve a config EFETIVA (banco → env) por
        // request — sync, health e demais consumidores falam com a mesma
        // configuração administrada na UI (F26 Fase 2.2). A criação é
        // síncrona sobre a resolução local (DbContext, sem SynchronizationContext).
        services.AddScoped<IUnifiApiClient>(serviceProvider =>
        {
            var resolver = serviceProvider.GetRequiredService<IUniFiOptionsResolver>();
            var factory = serviceProvider.GetRequiredService<IUnifiClientFactory>();
            var options = resolver.ResolveAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return factory.Create(options);
        });
        services.AddHttpClient(UnifiApiClient.HttpClientName)
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                // F26 Fase 2.2: flag TLS da config EFETIVA (banco → env).
                // O handler é cacheado por nome pelo IHttpClientFactory —
                // trocar a flag na UI pede restart (ou rotação de handler)
                // para valer; registrar como limitação na docs/29.
                var envAllowsInvalid = serviceProvider
                    .GetRequiredService<IOptions<UnifiIntegrationOptions>>().Value
                    .AllowInvalidTlsCertificate;
                var allowInvalid = envAllowsInvalid;
                try
                {
                    var scope = serviceProvider
                        .GetRequiredService<IServiceScopeFactory>()
                        .CreateScope();
                    using (scope)
                    {
                        var resolver = scope.ServiceProvider
                            .GetRequiredService<IUniFiOptionsResolver>();
                        allowInvalid = resolver
                            .ResolveAsync(CancellationToken.None)
                            .GetAwaiter()
                            .GetResult()
                            .AllowInvalidTlsCertificate;
                    }
                }
                catch
                {
                    // banco indisponível na criação do handler = flag de env
                }

                return UnifiHttpMessageHandler.CreatePrimaryHandler(allowInvalid);
            })
            .AddResilienceHandler(
                "unifi",
                static (builder, _) => HttpWebhookNotificationDispatcher.ConfigureResilience(builder));

        // F27 Fase 2.1/2.2: cliente de SAÍDA para a API JSON-RPC do Zabbix.
        // Config administrativa persistida (linha única, senha AES-GCM) com
        // fallback para env (Integrations:Zabbix:ApiUrl/Username/Password);
        // o IZabbixClient "padrão" resolve a config EFETIVA (banco → env) por
        // request — mesmo padrão do UniFi. Auth por user.login com Bearer
        // (Zabbix ≥ 8) ou campo auth (≤ 7) fica no próprio cliente; o named
        // client leva a MESMA política Polly do dispatcher n8n.
        services.AddScoped<IZabbixApiOptionsAccessor, ZabbixApiOptionsAccessor>();
        services.AddScoped<IZabbixOptionsResolver, ZabbixOptionsResolver>();
        services.AddScoped<IZabbixClient>(serviceProvider =>
        {
            var resolver = serviceProvider.GetRequiredService<IZabbixOptionsResolver>();
            var options = resolver.ResolveAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return new ZabbixApiClient(
                serviceProvider.GetRequiredService<IHttpClientFactory>(),
                Options.Create(options),
                serviceProvider.GetRequiredService<ILogger<ZabbixApiClient>>());
        });
        services.AddHttpClient(ZabbixApiClient.HttpClientName)
            // F27 Fase 11.1: timeout explícito (o default de 100s era demais
            // para a API do Zabbix) + resiliência retry/circuit breaker do
            // dispatcher (ADR-028 §6).
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15))
            .AddResilienceHandler(
                "zabbix-api",
                static (builder, _) => HttpWebhookNotificationDispatcher.ConfigureResilience(builder));

        services.AddScoped<ISlackNotificationDispatcher, LoggingSlackNotificationDispatcher>();
        services.AddScoped<ITelegramNotificationDispatcher, LoggingTelegramNotificationDispatcher>();
        services.AddScoped<IWhatsAppNotificationDispatcher, LoggingWhatsAppNotificationDispatcher>();
        services.AddScoped<IIdentityAdministrationService, IdentityAdministrationService>();
        services.AddScoped<IAdminIdentityProvisioner, IdentityAdminProvisioner>();

        // F1-T014: login social Google. A porta valida o id_token no servidor
        // (assinatura/JWKS + aud + iss + exp + email_verified); sem
        // ClientId a feature simplesmente fica desligada.
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.AddScoped<IGoogleIdentityTokenValidator, GoogleIdentityTokenValidator>();

        // F1-T016a (ADR-019): tolerância do TOTP é política de configuração,
        // não de código — padrão ±1 período de 30s.
        services.Configure<MfaOptions>(configuration.GetSection(MfaOptions.SectionName));

        // Fase 4 (ADR-034): upload de evidências do PDI em disco local —
        // caminho em PdiEvidence:StoragePath.
        services.Configure<PdiEvidenceOptions>(
            configuration.GetSection(PdiEvidenceOptions.SectionName));
        services.AddScoped<IPdiEvidenceFileStore, PhysicalPdiEvidenceFileStore>();

        // F28-T004: storage e repositórios do acompanhamento do projeto.
        services.Configure<ProjectEvidenceOptions>(
            configuration.GetSection(ProjectEvidenceOptions.SectionName));
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectPhaseRepository, ProjectPhaseRepository>();
        services.AddScoped<IProjectEvidenceFileStore, PhysicalProjectEvidenceFileStore>();
        services.AddScoped<IProjectProgressRepository, ProjectProgressRepository>();
        services.AddScoped<IProjectImpedimentRepository, ProjectImpedimentRepository>();
        services.AddScoped<IProjectEvidenceRepository, ProjectEvidenceRepository>();
        services.AddScoped<IProjectHistoryRepository, ProjectHistoryRepository>();

        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IConnectionFactory>(_ =>
            new ConnectionFactory(connectionString));

        // Redis standalone (dev) e Sentinel (HA) passam pela mesma factory,
        // para que cache, rate limit e locks acompanhem o primary promovido.
        services.AddSingleton<RedisConnectionProvider>(
            _ => RedisConnectionFactory.CreateProvider(redisConnectionString));
        services.AddSingleton<IConnectionMultiplexer>(provider =>
            provider.GetRequiredService<RedisConnectionProvider>().Connection);

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

        // ADR-044: Lock distribuído no Redis para coordenação de workers
        // em múltiplas instâncias (prevenção de duplicação).
        services.AddScoped<IDistributedLockService, DistributedLockService>();

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
            configureBus?.Invoke(x);

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

        // Monitoring (F5-T003/F5-T004/F5-T005/F5-T006): ping ICMP + SNMP
        // de impressoras + probe HTTP/HTTPS + probe TCP
        services.AddScoped<IIcmpClient, SystemNetIcmpClient>();
        services.AddScoped<IHttpProbeClient, SystemNetHttpProbeClient>();
        services.AddScoped<ITcpProbeClient, SystemNetTcpProbeClient>();
        // Discovery (F4-T003/T004): probes ICMP e SNMP do gatilho manual
        services.AddScoped<IDiscoveryProvider, IcmpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, SnmpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, LldpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, CdpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, ArpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, DhcpDiscoveryProvider>();
        services.AddScoped<IDiscoveryProvider, MacTableDiscoveryProvider>();
        // F3-T005b: ingestão de Connections a partir de vizinhos LLDP/CDP
        services.AddScoped<INeighborConnectionImporter, NeighborConnectionImporter>();
        services.AddScoped<IMonitoringProvider, PingMonitoringProvider>();
        services.AddScoped<IAlertEvaluator, AlertEvaluator>();
        // ADR-027: pipeline externo Zabbix → MonitoringEvent → Alert → Incident
        // (o par IMonitoringEventRepository ↔ MonitoringEventRepository nasce
        // no AddRepositoryRegistrations, lá embaixo).
        services.AddScoped<IMonitoringEventIngestor, MonitoringEventIngestor>();
        services.AddScoped<IRootCauseResolver, RootCauseResolver>();
        services.Configure<SnmpOptions>(configuration.GetSection(SnmpOptions.SectionName));
        services.AddSingleton<ISnmpClient, SharpSnmpSnmpClient>();
        services.AddScoped<IMonitoringProvider, SnmpTonerProvider>();
        services.AddScoped<IMonitoringProvider, SnmpDrumProvider>();
        services.AddScoped<IMonitoringProvider, SnmpSystemMonitoringProvider>();
        services.AddScoped<IMonitoringProvider, SnmpInterfacesMonitoringProvider>();
        services.AddScoped<IMonitoringProvider, HttpWanMonitoringProvider>();
        services.AddScoped<IMonitoringProvider, TcpMonitoringProvider>();

        // Network services read (F18-T005, ADR-042): leases DHCP/ARP +
        // rotas via SNMP (reusa ISnmpClient) e ACLs via SSH de leitura
        // por vendor (reusa SSH.NET do ADR-013). Somente leitura.
        services.Configure<NetworkServicesOptions>(
            configuration.GetSection(NetworkServicesOptions.SectionName));
        services.AddScoped<INetworkServicesReader, NetworkServicesReader>();
        services.AddScoped<ISshCommandRunner, SshNetCommandRunner>();
        services.AddScoped<IFirewallVendorReader, CiscoFirewallVendorReader>();
        services.AddScoped<IFirewallVendorReaderSelector, FirewallVendorReaderSelector>();

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
        // F7-T006: histórico de tasks (interface não termina em "Repository"
        // → sem auto-registro; linha manual aqui).
        services.AddScoped<InfraControl.Application.Tasks.History.Ports.ITaskHistoryWriter,
            InfraControl.Application.Tasks.History.TaskHistoryWriter>();
        services.AddScoped<IAuditReader, AuditReader>();
        services.AddScoped<IDomainEventService, DomainEventService>();

        // ADR-021: implementação "sem requisição": fora do host HTTP (os
        // workers) não há correlation id em andamento, e DomainEventService
        // precisa de algo para resolver. A API sobrescreve com a do HttpContext
        // (registrada mais tarde no Program.cs, última-registração ganha).
        services.AddScoped<InfraControl.Application.Common.Interfaces.ICurrentCorrelation,
            InfraControl.Application.Common.Interfaces.NullCurrentCorrelation>();

        // Retention / cleanup
        services.Configure<RetentionOptions>(configuration.GetSection("Retention"));

        // Performance (Dia 5 — docs/20): alerta de request lento
        services.Configure<PerformanceOptions>(configuration.GetSection("Performance"));
        services.AddScoped<ICleanupService, CleanupService>();

        // F23-T004/P4c — evidências de task em disco local (ADR-033):
        // storage é interface sem par I*Repository → registro manual.
        services.Configure<StorageOptions>(
            configuration.GetSection(StorageOptions.SectionName));
        services.AddScoped<InfraControl.Application.Tasks.Ports.IEvidenceFileStore,
            LocalEvidenceFileStore>();

        // F5-T007: polling periódico de monitoring (worker fino agenda)
        services.AddScoped<IMonitoringPollingService, MonitoringPollingService>();

        // Invitations
        services.AddScoped<IInvitationAccountPort, InvitationAccountPort>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IRackRepository, RackRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<INetworkInterfaceRepository, NetworkInterfaceRepository>();
        services.AddScoped<IMonitoringCheckRepository, MonitoringCheckRepository>();
        services.AddScoped<IMonitoringResultRepository, MonitoringResultRepository>();
        services.AddScoped<IRemoteSessionRepository, RemoteSessionRepository>();

        // Fase AI — FAI-T005 (S4): Provider & Model Manager v1 (ADR-037)
        // Configuração de IA: Ollama + roteamento por capacidade
        services.Configure<AiOllamaOptions>(configuration.GetSection("Ai:Ollama"));
        services.Configure<AiRoutingOptionsConfiguration>(configuration.GetSection("Ai:Routing"));
        // AiProviderRouter injeta o objeto concreto (não IOptions) — o
        // binding acima já cobre a seção; seção ausente vira defaults
        // vazios e o router lança AiProviderNotConfiguredException no
        // Resolve (estado degradado desejado, ADR-037 §44).
        var aiRouting = configuration.GetSection("Ai:Routing").Get<AiRoutingOptionsConfiguration>()
            ?? new AiRoutingOptionsConfiguration
            {
                Copilot = new() { DefaultModel = string.Empty },
                IncidentAnalysis = new() { DefaultModel = string.Empty },
                KnowledgeGeneration = new() { DefaultModel = string.Empty },
                Reporting = new() { DefaultModel = string.Empty },
                Automation = new() { DefaultModel = string.Empty }
            };
        services.AddSingleton(aiRouting);

        // HttpClient do Ollama (named client)
        services.AddHttpClient("ollama");

        // Registry de providers v1 (configuração)
        services.AddScoped<IAiProviderRegistry, ConfigurationProviderRegistry>();

        // Provider Ollama
        services.AddScoped<IAiProvider, OllamaProvider>();

        // Router de capacidade
        services.AddScoped<IAiProviderRouter, AiProviderRouter>();

        // FAI-T005 (S4): liga o gateway (S3) ao registro/router/providers
        // acima + hook de auditoria de log (ADR-036/§41 — a persistência
        // da ação AI.* na auditoria de domínio é do S6/ADR-040). Limites
        // por chamada (§55) configuráveis em Ai:Gateway (default do
        // record vale quando a seção não existe).
        var aiGatewayOptions = configuration.GetSection("Ai:Gateway").Get<AiGatewayOptions>()
            ?? new AiGatewayOptions();
        services.AddSingleton(aiGatewayOptions);
        // FAI-T007 (S6): hook de auditoria persiste a ação AI.* na
        // auditoria de domínio existente (F0-T014, sem migração) +
        // log estruturado (ADR-040 · §41/§54).
        services.AddScoped<IAiAuditSink, AuditAiAuditSink>();
        services.AddScoped<IAiGateway, AiGateway>();

        // FAI-T006 (S5): runtime health da camada de IA (§44) —
        // capacidades sem mapeamento/providers inalcançáveis = degradado,
        // nunca unhealthy (o host sobe sem Ollama). Consumido pelo
        // IHealthCheck do host (Infrastructure/AI/AiRuntimeHealthCheck).
        services.AddScoped<IAiRuntimeHealth, AiRuntimeHealthService>();

        // FAI-T008 (S7): MCP foundation — tools de leitura in-process
        // (§20/§21/§52 · ADR-039): registry + executor (autorização §41
        // pela MESMA hierarquia RoleAccess do pipeline MediatR, timeout
        // por tool, auditoria AI.Tool.*). O servidor MCP externo vai
        // servir este registry quando o ADR-039 determinar.
        services.AddScoped<GetActiveIncidentsTool>();
        services.AddScoped<SearchKnowledgeTool>();
        services.AddScoped<IAiToolRegistry>(provider => new InMemoryToolRegistry(
        [
            provider.GetRequiredService<GetActiveIncidentsTool>(),
            provider.GetRequiredService<SearchKnowledgeTool>(),
        ]));
        services.AddScoped<IAiToolExecutor, AiToolExecutor>();

        // FAI-T009 (S8): Context Engine + RAG foundation (ADR-038) —
        // contratos + v1 heurística (rerank por score/recência, chunker
        // por artigo, ingesta de Knowledge publicado). IVectorStore NÃO
        // é registrado até o pgvector existir no ambiente (avaliação de
        // imagem pendente de Docker, §46): a híbrida degrada para
        // keyword (§44) e a ingesta fica inacessível — sem invenção.
        services.AddScoped<IEmbeddingProvider, OllamaEmbeddingProvider>();
        services.AddScoped<IKeywordSearch, KnowledgeKeywordSearch>();
        services.AddScoped<IReranker, HeuristicReranker>();
        services.AddScoped<IContextBuilder, EvidenceSetContextBuilder>();
        services.AddScoped<IAiKnowledgeIngestion, KnowledgeIngestionService>();
        services.AddScoped<IRetrievalService>(provider => new KnowledgeHybridRetrievalService(
            provider.GetRequiredService<IEmbeddingProvider>(),
            provider.GetService<IVectorStore>(),
            provider.GetRequiredService<IKeywordSearch>(),
            provider.GetRequiredService<IReranker>(),
            provider.GetRequiredService<ILogger<KnowledgeHybridRetrievalService>>()));

        // FAI-T010 (S9): harness de avaliação (§45) — checks que não
        // dependem de LLM (tool selection, authorization, grounding);
        // o IEvaluationScenario do fluxo real entra com o Copilot/Fase 1.
        services.AddScoped<IEvaluationCheck, ToolSelectionCheck>();
        services.AddScoped<IEvaluationCheck, AuthorizationCheck>();
        services.AddScoped<IEvaluationCheck, GroundingCheck>();
        services.AddScoped<EvaluationHarness>();

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
