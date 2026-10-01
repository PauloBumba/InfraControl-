using InfraControl.Application;
using InfraControl.Infrastructure.Services;
using InfraControl.Workers;
using InfraControl.Workers.Consumers.Notifications;
using InfraControl.Workers.Tracing;
using MassTransit;
using MediatR;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// ADR-021: o worker entra na MESMA plataforma de log da API. Sem isto, tudo o
// que roda fora da API (outbox, cleanup, backup, polling, notificações) não
// chega a lugar nenhum e o admin enxerga metade da aplicação.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "InfraControl.Workers")
    .WriteTo.Console()
    .CreateLogger();

// HostApplicationBuilder nao tem .Host: o Serilog entra pelo ILoggerFactory.
// ClearProviders e obrigatorio, senao o console padrao continua junto e cada
// linha sai duas vezes.
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationMediatR();

// Consumers do broker — combinados com o UsingRabbitMq do AddInfrastructure
// (ADR-004/docs/06: OutboxWorker publica, aqui consome).
builder.Services.AddMassTransit(configuration =>
{
    configuration.AddConsumers(typeof(NotificationEventConsumer).Assembly);

    // ADR-021: observador global — levanta o CorrelationId do header da
    // mensagem para o LogContext de qualquer consumer, sem alterá-los.
    configuration.AddConsumeObserver<CorrelationIdConsumeObserver>();
});

// F9-T006: canais do fanout + destinos por canal (Notifications:Channels) —
// a MESMA seção que o host da Api lê para os workers de canal.
builder.Services.Configure<InfraControl.Application.Notifications.Channels.NotificationChannelsOptions>(
    options => builder.Configuration
        .GetSection(InfraControl.Application.Notifications.Channels.NotificationChannelsOptions.SectionName)
        .Bind(options));

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

var host = builder.Build();
host.Run();
