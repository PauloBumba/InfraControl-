using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Common.Behaviors;
using InfraControl.Application.Identity;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Administration;
using InfraControl.Application.Identity.Roles.Ports;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Domain.Common;
using InfraControl.Infrastructure.Caching;
using InfraControl.Infrastructure.Services;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Auth;
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
        services.AddScoped<IUserRoleRepository, IdentityUserRoleRepository>();

        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IConnectionFactory>(_ =>
            new ConnectionFactory(connectionString));

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<RedisCacheService>();
        services.AddScoped<ICacheService>(provider =>
            provider.GetRequiredService<RedisCacheService>());
        services.AddScoped<IIdempotencyStore>(provider =>
            provider.GetRequiredService<RedisCacheService>());

        // Pipeline transversal: cada behavior é opt-in quando requer um contrato específico.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CorrelationIdBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehavior<,>));

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

        // Outbox
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IDomainEventService, DomainEventService>();

        // Retention / cleanup
        services.Configure<RetentionOptions>(configuration.GetSection("Retention"));
        services.AddScoped<ICleanupService, CleanupService>();

        // Invitations
        services.AddScoped<IInvitationRepository, InvitationRepository>();

        return services;
    }
}
