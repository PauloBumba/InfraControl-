namespace InfraControl.Api.ViewModels.Admin;

/// <summary>FAI-T011b: payloads do CRUD Administer de providers de IA.</summary>
public sealed record CreateAiProviderViewModel(
    string Name,
    string BaseUrl,
    string? ApiKey,
    IReadOnlyList<string>? Models);

public sealed record UpdateAiProviderViewModel(
    string BaseUrl,
    string? ApiKey,
    IReadOnlyList<string>? Models);

public sealed record SetAiProviderActiveViewModel(bool Active);

public sealed record UpsertAiProviderCapabilityViewModel(
    Guid ProviderId,
    string DefaultModel,
    Guid? FallbackProviderId,
    string? FallbackModel);
