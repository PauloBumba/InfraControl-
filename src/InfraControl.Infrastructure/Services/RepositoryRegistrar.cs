using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Registro automático de repositórios por convenção (F0-T024), no espírito do
/// RegisterServicesFromAssembly do MediatR: toda interface "I*Repository" do
/// assembly de contratos (Application) que tenha exatamente uma classe
/// "*Repository" implementadora no assembly de implementações (Infrastructure)
/// é registrada como Scoped.
/// Regras:
/// - Registro manual existente PREVALECE (TryAdd) — escape hatch para lifetime
///   ou fábrica especiais; basta manter a linha manual antes desta chamada.
/// - Par ausente ou ambíguo → exceção NO STARTUP (fail-fast), nunca em runtime
///   na primeira requisição.
/// Novo repositório seguindo o padrão nome (IXxxRepository / XxxRepository)
/// passa a ser registrado sozinho, sem linha nova no DependencyInjection.
/// </summary>
public static class RepositoryRegistrar
{
    public static IServiceCollection AddRepositoryRegistrations(
        this IServiceCollection services,
        Assembly interfaceAssembly,
        Assembly implementationAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(interfaceAssembly);
        ArgumentNullException.ThrowIfNull(implementationAssembly);

        var repositoryInterfaces = interfaceAssembly.GetTypes()
            .Where(type => type.IsInterface
                && !type.IsGenericTypeDefinition
                && type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToArray();

        foreach (var repositoryInterface in repositoryInterfaces)
        {
            if (services.Any(descriptor => descriptor.ServiceType == repositoryInterface))
                continue;

            var implementations = implementationAssembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false }
                    && !type.IsGenericTypeDefinition
                    && type.Name.EndsWith("Repository", StringComparison.Ordinal)
                    && repositoryInterface.IsAssignableFrom(type))
                .ToArray();

            switch (implementations.Length)
            {
                case 0:
                    throw new InvalidOperationException(
                        $"Nenhuma implementação encontrada para o repositório '{repositoryInterface.Name}' " +
                        $"no assembly '{implementationAssembly.GetName().Name}'.");
                case > 1:
                    throw new InvalidOperationException(
                        $"Múltiplas implementações encontradas para o repositório '{repositoryInterface.Name}': " +
                        $"{string.Join(", ", implementations.Select(type => type.Name))}. " +
                        "Registre manualmente no DependencyInjection para prevalecer.");
                default:
                    services.TryAdd(ServiceDescriptor.Scoped(repositoryInterface, implementations[0]));
                    break;
            }
        }

        return services;
    }
}
