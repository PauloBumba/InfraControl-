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

builder.Services.AddHostedService<OutboxDispatcherWorker>();
builder.Services.AddHostedService<CleanupWorker>();

var host = builder.Build();
host.Run();
