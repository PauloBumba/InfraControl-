namespace InfraControl.Application.AI.Health;

public sealed class AiRuntimeHealthService(
    IAiProviderRouter router,
    IAiProviderRegistry registry) : IAiRuntimeHealth
{
    public async Task<AiRuntimeHealthReport> CheckAsync(CancellationToken cancellationToken = default)
    {
        var components = new List<AiComponentReport>();
        var testedProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var capability in Enum.GetValues<AiCapability>())
        {
            AiProviderSelection selection;
            try
            {
                selection = router.Resolve(capability);
            }
            catch (AiProviderNotConfiguredException)
            {
                components.Add(new AiComponentReport(
                    $"capability:{capability}",
                    AiComponentStatus.Degraded,
                    $"A capacidade '{capability}' não está configurada (§44)."));
                continue;
            }

            components.Add(new AiComponentReport(
                $"capability:{capability}",
                AiComponentStatus.Healthy,
                $"Mapeada para {selection.Provider}/{selection.Model}"));

            if (!string.IsNullOrWhiteSpace(selection.Provider))
            {
                testedProviders.Add(selection.Provider);
            }

            if (!string.IsNullOrWhiteSpace(selection.FallbackProvider))
            {
                testedProviders.Add(selection.FallbackProvider);
            }
        }

        foreach (var providerName in testedProviders)
        {
            IAiProvider provider;
            try
            {
                provider = registry.Get(providerName);
            }
            catch
            {
                components.Add(new AiComponentReport(
                    $"provider:{providerName}",
                    AiComponentStatus.Degraded,
                    $"provedor '{providerName}' não registrado"));
                continue;
            }

            try
            {
                var ok = await provider.TestConnectionAsync(cancellationToken);
                if (ok)
                {
                    components.Add(new AiComponentReport(
                        $"provider:{providerName}",
                        AiComponentStatus.Healthy,
                        "conexão OK (§13)"));
                }
                else
                {
                    components.Add(new AiComponentReport(
                        $"provider:{providerName}",
                        AiComponentStatus.Degraded,
                        "endpoint inalcançável (§13)"));
                }
            }
            catch (Exception ex)
            {
                components.Add(new AiComponentReport(
                    $"provider:{providerName}",
                    AiComponentStatus.Degraded,
                    $"falha ao testar conexão: {ex.Message}"));
            }
        }

        var isHealthy = components.All(c => c.Status == AiComponentStatus.Healthy);
        return new AiRuntimeHealthReport(isHealthy, components);
    }
}
