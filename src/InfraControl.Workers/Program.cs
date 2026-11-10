using InfraControl.Application;
using InfraControl.Contracts.AI;
using InfraControl.Infrastructure.Services;
using InfraControl.Workers;
using InfraControl.Workers.AI;
using InfraControl.Workers.Consumers.Notifications;
using InfraControl.Workers.Tracing;
using MassTransit;
using MediatR;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ADR-021: o worker entra na MESMA plataforma de log da API. Sem isto, tudo o
// que roda fora da API (outbox, cleanup, backup, polling, notificações) não
// chega a lugar nenhum e o admin enxerga metade da aplicação.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "InfraControl.Workers")
    .CreateLogger();

// HostApplicationBuilder nao tem .Host: o Serilog entra pelo ILoggerFactory.
// ClearProviders e obrigatorio, senao o console padrao continua junto e cada
// linha sai duas vezes.
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);
// Consumers do broker — combinados com o UsingRabbitMq do AddInfrastructure
// (ADR-004/docs/06: OutboxWorker publica, aqui consome). Entram pela MESMA
// chamada de AddMassTransit: uma segunda chamada aqui derrubava o boot com
// MassTransit.ConfigurationException ("already called ... may only be called
// once per container").
builder.Services.AddInfrastructure(
    builder.Configuration,
    configuration =>
    {
        configuration.AddConsumers(typeof(NotificationEventConsumer).Assembly);

        // ADR-044: Worker de inferência de IA para escalar Ollama com fila RabbitMQ
        configuration.AddConsumer<AiInferenceWorker>();

        // ADR-021: observador global — levanta o CorrelationId do header da
        // mensagem para o LogContext de qualquer consumer, sem alterá-los.
        configuration.AddConsumeObserver<CorrelationIdConsumeObserver>();
    });

builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(InfraControl.Application.Identity.AdminBootstrapper).Assembly));
builder.Services.AddApplicationMediatR();

// F9-T006: canais do fanout + destinos por canal (Notifications:Channels) —
// a MESMA seção que a API lê para os workers de canal.
// D5 (docs/66): PostConfigure normaliza (binder append → duplicatas).
builder.Services.Configure<InfraControl.Application.Notifications.Channels.NotificationChannelsOptions>(
    options =>
    {
        builder.Configuration
            .GetSection(InfraControl.Application.Notifications.Channels.NotificationChannelsOptions.SectionName)
            .Bind(options);
        options.NormalizeDefaultChannels();
    });

// F12-T003: backup periódico do PostgreSQL — senha só via ConnectionString
// (env ConnectionStrings__DefaultConnection), nunca em config nova.
builder.Services.Configure<InfraControl.Workers.Backup.BackupOptions>(options =>
{
    builder.Configuration.GetSection("Backup").Bind(options);
    if (string.IsNullOrWhiteSpace(options.ConnectionString))
        options.ConnectionString =
            builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
});
builder.Services.AddSingleton<
    InfraControl.Workers.Backup.IProcessRunner, InfraControl.Workers.Backup.SystemProcessRunner>();
builder.Services.AddSingleton<InfraControl.Workers.Backup.PostgresBackupService>();

builder.Services.AddHostedService<OutboxDispatcherWorker>();
builder.Services.AddHostedService<CleanupWorker>();
builder.Services.AddHostedService<InfraControl.Workers.Backup.BackupWorker>();
builder.Services.AddHostedService<MonitoringPollingWorker>();
builder.Services.AddHostedService<InfraControl.Workers.UnifiSyncWorker>();

// Observabilidade (profile obs): MESMO pipeline OTLP da API — traces dos
// commands MediatR (ObservabilityBehavior), spans de consumers MassTransit
// e métricas InfraControl.* (db commands, request.duration dos handlers
// executados aqui). Sem OpenTelemetry:Endpoint configurado o host sobe
// normal sem telemetria (mesmo comportamento da API).
var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"];
if (!string.IsNullOrWhiteSpace(otelEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService("InfraControl.Workers")
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName
            }))
        .WithTracing(tracing => tracing
            .AddHttpClientInstrumentation()
            .AddSource("InfraControl.*")
            .AddSource("MassTransit")
            .AddOtlpExporter(options => options.Endpoint = new Uri(otelEndpoint)))
        .WithMetrics(metrics => metrics
            .AddHttpClientInstrumentation()
            .AddMeter("InfraControl.*")
            .AddOtlpExporter(options => options.Endpoint = new Uri(otelEndpoint)));
}

var host = builder.Build();
host.Run();
