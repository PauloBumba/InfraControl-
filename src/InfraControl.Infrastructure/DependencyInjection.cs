using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Common.Caching;
using InfraControl.Infrastructure.Common.Messaging;
using InfraControl.Infrastructure.Common.Persistence;
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

        // PostgreSQL
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IConnectionFactory>(_ =>
            new ConnectionFactory(connectionString));

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<ICacheService, RedisCacheService>();

        // Outbox
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IDomainEventService, DomainEventService>();

        return services;
    }
}
