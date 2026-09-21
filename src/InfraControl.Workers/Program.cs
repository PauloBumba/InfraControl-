using InfraControl.Infrastructure;
using InfraControl.Workers;
using MediatR;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(InfraControl.Application.Identity.AdminBootstrapper).Assembly));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
