using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity;
using InfraControl.Application.Identity.Roles.Ports;
using InfraControl.Domain.Common;
using InfraControl.Infrastructure.Common.Caching;
using InfraControl.Infrastructure.Common.Messaging;
using InfraControl.Infrastructure.Common.Persistence;
using InfraControl.Infrastructure.Common.Time;
using InfraControl.Infrastructure.Identity;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace InfraControl.Infrastructure;

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

        services.AddScoped<IAdminIdentityProvisioner, IdentityAdminProvisioner>();
        services.AddScoped<IUserRoleRepository, IdentityUserRoleRepository>();

        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IConnectionFactory>(_ =>
            new ConnectionFactory(connectionString));

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<ICacheService, RedisCacheService>();

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

        // Outbox
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IDomainEventService, DomainEventService>();

        return services;
    }
}
