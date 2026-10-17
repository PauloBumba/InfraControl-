using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.AI.Providers;

/// <summary>
/// FAI-T011b (ADR-057): visão administrativa do cadastro de providers.
/// A chave nunca sai plaintext — só <see cref="AiProviderDto.ApiKeySet"/>
/// e o sufixo mascarado <c>••••XXXX</c> (decifra só em memória; falha de
/// unprotect degrada para máscara sem sufixo).
/// </summary>
public sealed record AiProviderDto(
    Guid Id,
    string Name,
    string BaseUrl,
    bool Active,
    bool ApiKeySet,
    string? ApiKeyMasked,
    IReadOnlyList<string> Models);

/// <summary>Rota capacidade → provider (sem linha = capacidade cai no config, D3-A).</summary>
public sealed record AiProviderCapabilityRouteDto(
    string Capability,
    Guid? ProviderId,
    string? ProviderName,
    bool ProviderActive,
    Guid? FallbackProviderId,
    string? FallbackProviderName,
    bool FallbackProviderActive,
    string DefaultModel,
    string? FallbackModel);

public sealed record AiProviderListDto(
    IReadOnlyList<AiProviderDto> Providers,
    IReadOnlyList<AiProviderCapabilityRouteDto> Capabilities,
    IReadOnlyList<string> AvailableAdapterNames);

public sealed record AiProviderConnectionTestDto(bool Ok, string? Error);

public sealed record AiProviderModelsDto(IReadOnlyList<string> Models, string? Error);

internal static class AiProviderAdminMapper
{
    internal static AiProviderDto ToDto(this AiProvider provider, ICredentialProtector protector)
    {
        var (apiKeySet, mask) = MaskApiKey(provider.ApiKeyEncrypted, protector);
        return new AiProviderDto(
            provider.Id,
            provider.Name,
            provider.BaseUrl,
            provider.IsActive,
            apiKeySet,
            mask,
            provider.Models);
    }

    internal static AiProviderCapabilityRouteDto ToRouteDto(
        string capability,
        AiProviderCapability? route,
        IReadOnlyDictionary<Guid, AiProvider> providersById)
    {
        var provider = route is null
            ? null
            : providersById.GetValueOrDefault(route.ProviderId);
        var fallback = route?.FallbackProviderId is { } fallbackId
            ? providersById.GetValueOrDefault(fallbackId)
            : null;

        return new AiProviderCapabilityRouteDto(
            capability,
            route?.ProviderId,
            provider?.Name,
            provider?.IsActive == true,
            route?.FallbackProviderId,
            fallback?.Name,
            fallback?.IsActive == true,
            route?.DefaultModel ?? string.Empty,
            route?.FallbackModel);
    }

    private static (bool Set, string? Mask) MaskApiKey(
        string? apiKeyEncrypted, ICredentialProtector protector)
    {
        if (string.IsNullOrEmpty(apiKeyEncrypted))
            return (false, null);

        try
        {
            var plain = protector.Unprotect(apiKeyEncrypted);
            var suffix = plain.Length > 4 ? plain[^4..] : plain;
            return (true, $"••••{suffix}");
        }
        catch (Exception)
        {
            return (true, "••••••");
        }
    }
}
