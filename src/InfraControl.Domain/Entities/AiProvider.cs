using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// ADR-057 (FAI-T011a): cadastro de provider de IA na plataforma —
/// substitui o registry "configuration" do ADR-037 §2 como fonte de
/// <c>BaseUrl/ApiKey/Models/Ativo</c>. O adaptador concreto continua em
/// código (ADR-050 §1); isto é ENDEREÇO e CREDENCIAL, nunca SDK.
/// <c>ApiKeyEncrypted</c> é cifrada (ICredentialProtector/AES-GCM,
/// F11-T006 — mesmo padrão do RemoteAccess/UniFi) e nunca aparece em
/// DTO, log ou resposta (só sufixo mascarado na superfície).
/// Ollama é o único default embarcado (seed idempotente, ADR-057 §2).
/// </summary>
public sealed class AiProvider : Entity
{
    public const int MaxNameLength = 64;
    public const int MaxBaseUrlLength = 512;
    public const int MaxModelLength = 128;
    public const int MaxApiKeyEncryptedLength = 2048;

    private List<string>? _models;

    public string Name { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public string? ApiKeyEncrypted { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyList<string> Models => _models ?? [];

    private AiProvider() { }

    public static AiProvider Create(
        string name,
        string baseUrl,
        string? apiKeyEncrypted = null,
        IEnumerable<string>? models = null,
        bool isActive = true)
    {
        var provider = new AiProvider();
        provider.Update(name, baseUrl, apiKeyEncrypted, models, isActive);
        return provider;
    }

    public void Update(
        string name,
        string baseUrl,
        string? apiKeyEncrypted,
        IEnumerable<string>? models,
        bool isActive)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new ArgumentException("Nome do provider é obrigatório.", nameof(name));
        if (trimmedName.Length > MaxNameLength)
            throw new ArgumentException(
                $"Nome do provider pode ter no máximo {MaxNameLength} caracteres.", nameof(name));

        var trimmedBaseUrl = baseUrl?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(trimmedBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException(
                "BaseUrl do provider deve ser uma URL http(s) absoluta.", nameof(baseUrl));
        if (trimmedBaseUrl.Length > MaxBaseUrlLength)
            throw new ArgumentException(
                $"BaseUrl do provider pode ter no máximo {MaxBaseUrlLength} caracteres.",
                nameof(baseUrl));

        var trimmedKey = string.IsNullOrWhiteSpace(apiKeyEncrypted) ? null : apiKeyEncrypted.Trim();
        if (trimmedKey is { Length: > MaxApiKeyEncryptedLength })
            throw new ArgumentException(
                $"ApiKey cifrada pode ter no máximo {MaxApiKeyEncryptedLength} caracteres.",
                nameof(apiKeyEncrypted));

        Name = trimmedName;
        BaseUrl = trimmedBaseUrl;
        ApiKeyEncrypted = trimmedKey;
        _models = NormalizeModels(models) ?? [];
        IsActive = isActive;
        Touch();
    }

    /// <summary>Chave nula ou vazia = mantém a já cifrada (rotação opcional).</summary>
    public void UpdateApiKey(string? apiKeyEncrypted)
    {
        if (!string.IsNullOrWhiteSpace(apiKeyEncrypted))
        {
            if (apiKeyEncrypted.Trim().Length > MaxApiKeyEncryptedLength)
                throw new ArgumentException(
                    $"ApiKey cifrada pode ter no máximo {MaxApiKeyEncryptedLength} caracteres.",
                    nameof(apiKeyEncrypted));
            ApiKeyEncrypted = apiKeyEncrypted.Trim();
        }

        Touch();
    }

    public void Activate() => Update(Name, BaseUrl, ApiKeyEncrypted, _models, isActive: true);

    public void Deactivate() => Update(Name, BaseUrl, ApiKeyEncrypted, _models, isActive: false);

    private static List<string>? NormalizeModels(IEnumerable<string>? models)
    {
        if (models is null)
            return null;

        var normalized = models
            .Select(model => model?.Trim() ?? string.Empty)
            .Where(model => model.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var model in normalized)
        {
            if (model.Length > MaxModelLength)
                throw new ArgumentException(
                    $"Modelo pode ter no máximo {MaxModelLength} caracteres: '{model}'.");
        }

        return normalized;
    }
}
