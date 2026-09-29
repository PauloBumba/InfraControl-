using InfraControl.Infrastructure.Services;
using InfraControl.Workers;
using InfraControl.Workers.Consumers.Notifications;
using MassTransit;
using MediatR;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(InfraControl.Application.Identity.AdminBootstrapper).Assembly));

// Consumers do broker — combinados com o UsingRabbitMq do AddInfrastructure
// (ADR-004/docs/06: OutboxWorker publica, aqui consome).
builder.Services.AddMassTransit(configuration =>
    configuration.AddConsumers(typeof(NotificationEventConsumer).Assembly));

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
