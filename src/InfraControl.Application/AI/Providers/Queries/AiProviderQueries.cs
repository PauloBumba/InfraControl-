using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.AI.Providers.Queries;

// FAI-T011b (ADR-057): listagem administrativa única (providers +
// rotas por capacidade + nomes de adaptadores registrados). Sem cache:
// IQuery direto (mesmo precedente do GetZabbixSettingsQuery) — a página
// precisa de frescor imediato após mutations e o CachingBehavior não
// expõe invalidação por prefixo (chave inclui usuário+parâmetros).

public sealed record SearchAiProvidersQuery
    : IQuery<AiProviderListDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class SearchAiProvidersQueryHandler(
    IAiProviderRepository repository,
    IEnumerable<IAiProvider> adapters,
    ICredentialProtector protector)
    : IRequestHandler<SearchAiProvidersQuery, AiProviderListDto>
{
    public async Task<AiProviderListDto> Handle(
        SearchAiProvidersQuery request, CancellationToken cancellationToken)
    {
        var providers = await repository.ListAsync(cancellationToken);
        var byId = providers.ToDictionary(provider => provider.Id);

        var capabilities = new List<AiProviderCapabilityRouteDto>();
        foreach (var capability in Enum.GetNames<AiCapability>())
        {
            var route = await repository.GetCapabilityRouteAsync(
                capability, cancellationToken);
            capabilities.Add(AiProviderAdminMapper.ToRouteDto(
                capability, route, byId));
        }

        return new AiProviderListDto(
            providers.Select(provider => provider.ToDto(protector)).ToArray(),
            capabilities,
            adapters
                .Select(adapter => adapter.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }
}
