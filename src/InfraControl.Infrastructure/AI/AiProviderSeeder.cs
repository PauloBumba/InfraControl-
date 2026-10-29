using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// ADR-057 (FAI-T011a): seed idempotente do cadastro de providers de IA
/// (§2 — Ollama é o único default embarcado; providers externos
/// permanecem via config até o operador cadastrá-los). Rode uma vez no
/// boot; nunca sobrescreve linha existente (cadastro é autoridade do
/// operador). Provider externo já cadastrado (ex.: openai-compatible
/// com chave) é reaproveitado; ausente é criado sem chave (D3).
/// </summary>
public sealed class AiProviderSeeder(
    IAiProviderRepository repository,
    IUnitOfWork unitOfWork,
    IOptions<AiOllamaOptions> ollamaOptions,
    AiRoutingOptionsConfiguration routing)
{
    private const string OllamaName = "ollama";
    private const string DefaultOllamaBaseUrl = "http://localhost:11434";

    /// <summary>
    /// Popula o cadastro mínimo (Ollama + capacidades mapeadas na
    /// configuração) quando as tabelas estão vazias/novas.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var ollamaBaseUrl = string.IsNullOrWhiteSpace(ollamaOptions.Value.BaseUrl)
            ? DefaultOllamaBaseUrl
            : ollamaOptions.Value.BaseUrl.Trim();

        var ollama = await repository.GetByNameAsync(OllamaName, cancellationToken);
        if (ollama is null)
        {
            ollama = AiProvider.Create(OllamaName, ollamaBaseUrl);
            await repository.AddAsync(ollama, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // ADR-065: uma fonte — todas as capacidades saem do modelo único
        // (`Routing:Default`) com override opcional por capacidade.
        foreach (var capability in Enum.GetValues<AiCapability>())
        {
            var options = routing.EffectiveFor(capability);
            if (options is null)
            {
                continue;
            }

            await SeedCapabilityRouteAsync(
                capability, ollama, options.Provider, options.DefaultModel,
                cancellationToken);
        }
    }

    private async Task SeedCapabilityRouteAsync(
        AiCapability capability,
        AiProvider ollama,
        string? configuredProvider,
        string? configuredModel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuredModel))
        {
            // Capacidade sem configuração: estado degradado desejado (§44).
            return;
        }

        var capabilityName = capability.ToString();
        if (await repository.CapabilityRouteExistsAsync(capabilityName, cancellationToken))
        {
            // Cadastro é autoridade — linha existente do operador não é
            // sobrescrita.
            return;
        }

        var providerName = string.IsNullOrWhiteSpace(configuredProvider)
            ? OllamaName
            : configuredProvider.Trim();
        var provider = string.Equals(providerName, OllamaName, StringComparison.OrdinalIgnoreCase)
            ? ollama
            : await repository.GetByNameAsync(providerName, cancellationToken);

        // Provider externo ainda não cadastrado: deixa a capacidade sem
        // rota no cadastro (D3-A — resolve pela config) até o operador
        // cadastrá-lo.
        if (provider is null)
        {
            return;
        }

        var route = AiProviderCapability.Create(
            capabilityName, provider.Id, configuredModel.Trim());
        await repository.UpsertCapabilityRouteAsync(route, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
