using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfraControl.Application;

/// <summary>
/// Registro do MediatR da aplicação, compartilhado pela API, pelos Workers e
/// pelos testes de integração — os três hosts precisam levantar o MESMO
/// pipeline de handlers (behaviors de Authorization/Audit/Transaction junto).
/// </summary>
public static class MediatRServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationMediatR(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(
            typeof(Identity.AdminBootstrapper).Assembly));

        // F11/F12: handlers da rede delegam a handlers irmãos por TIPO CONCRETO
        // (ex.: SearchSwitchesQueryHandler → SearchDevicesQueryHandler). O
        // MediatR só registra as interfaces IRequestHandler<,> — sem este
        // registro dos concretos, o host com validação de DI (Development) não
        // sobe (ValidateOnBuild) e esses endpoints quebram em runtime com
        // "Unable to resolve service for type ...". Descoberto pelo e2e de
        // pipeline completo (WebApplicationFactory).
        foreach (var handler in typeof(Identity.AdminBootstrapper).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.Name.EndsWith("Handler", StringComparison.Ordinal)))
        {
            services.TryAddTransient(handler);
        }

        return services;
    }
}
