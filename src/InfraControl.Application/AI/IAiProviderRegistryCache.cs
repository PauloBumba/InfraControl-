namespace InfraControl.Application.AI;

/// <summary>
/// F49-T002: invalidação do cache de adaptadores bound por protocolo —
/// o admin pode mudar o protocolo de um provider cadastrado e o registry
/// precisa reconstruir a instância do adaptador.
/// </summary>
public interface IAiProviderRegistryCache
{
    void Invalidate(string? providerName = null);
}
