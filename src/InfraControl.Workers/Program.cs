using InfraControl.Infrastructure.Services;
using InfraControl.Workers;
using MediatR;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(InfraControl.Application.Identity.AdminBootstrapper).Assembly));
builder.Services.AddHostedService<OutboxDispatcherWorker>();
builder.Services.AddHostedService<CleanupWorker>();

var host = builder.Build();
host.Run();
