using System.Net;
using System.Text.Json;

namespace InfraControl.Application.AI.Validation;

/// <summary>F49-T001: protocolos suportados pela validação de conexão.</summary>
public enum AiProtocol
{
    OpenAiCompatible,
    Gemini,
    Anthropic,
    Ollama,
}

/// <summary>F49-T001: estado de cada fase — nunca confundir 401 com "sem rede".</summary>
public enum AiPhaseStatus
{
    Success,
    Failed,
    Skipped,
}

public sealed record AiConnectionPhase(string Phase, AiPhaseStatus Status, string? Error)
{
    public static AiConnectionPhase Success(string phase) =>
        new(phase, AiPhaseStatus.Success, null);

    public static AiConnectionPhase Failed(string phase, string error) =>
        new(phase, AiPhaseStatus.Failed, error);

    public static AiConnectionPhase Skipped(string phase) =>
        new(phase, AiPhaseStatus.Skipped, null);
}

/// <summary>
/// F49-T001: pedido de validação dry-run — endpoint e credenciais
/// arbitrários, sem tocar no cadastro de providers.
/// </summary>
public sealed record AiConnectionValidationRequest(
    AiProtocol Protocol,
    string BaseUrl,
    string? ApiKey,
    string? Model = null);

/// <summary>
/// F49-T001: resultado em fases — conectividade, autenticação, modelos
/// descobertos e (opcional) disponibilidade do modelo pedido.
/// </summary>
public sealed record AiConnectionValidationResult(
    bool Ok,
    AiConnectionPhase Connectivity,
    AiConnectionPhase Authentication,
    AiConnectionPhase Models,
    IReadOnlyList<string> ModelsFound,
    AiConnectionPhase? ModelCheck);

/// <summary>
/// F49-T001: porta da validação em fases (T004 expõe via endpoint admin).
/// </summary>
public interface IAiConnectionValidator
{
    Task<AiConnectionValidationResult> ValidateAsync(
        AiConnectionValidationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// F49-T001: valida um endpoint+key arbitrários em 3 fases independentes —
/// (1) conectividade: o endereço responde HTTP?; (2) autenticação: a key
/// é aceita no probe de modelos do PROTOCOLO (Bearer, ?key=, x-api-key);
/// (3) modelos: descoberta pela lista oficial do fornecedor. HTTP 200 no
/// caminhão não prova key válida; 401 não é "sem rede". Sem SDK, HTTP
/// puro (molde dos adaptadores ADR-050); timeout curto e key nunca
/// aparece em erro/log.
/// </summary>
public sealed class AiConnectionValidator : IAiConnectionValidator, IDisposable
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public AiConnectionValidator(HttpMessageHandler? handler = null)
    {
        _ownsClient = handler is null;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = ProbeTimeout;
    }

    public async Task<AiConnectionValidationResult> ValidateAsync(
        AiConnectionValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = request.BaseUrl.Trim().TrimEnd('/');

        // Fase 1 — conectividade: qualquer resposta HTTP prova que o
        // endereço existe; exceção de rede/timeout = inalcançável.
        var connectivity = await ProbeConnectivityAsync(baseUrl, cancellationToken);
        if (connectivity.Status == AiPhaseStatus.Failed)
        {
            return new AiConnectionValidationResult(
                Ok: false,
                Connectivity: connectivity,
                Authentication: AiConnectionPhase.Skipped("autenticação"),
                Models: AiConnectionPhase.Skipped("modelos"),
                ModelsFound: [],
                ModelCheck: null);
        }

        // Fase 2+3 — probe de modelos do protocolo distingue key recusada
        // (401/403) de endpoint errado (404) e de falha do fornecedor (5xx).
        var (authentication, models, modelsFound) =
            await ProbeModelsAsync(request, baseUrl, cancellationToken);

        AiConnectionPhase? modelCheck = null;
        if (modelsFound.Count > 0 && !string.IsNullOrWhiteSpace(request.Model))
        {
            modelCheck = modelsFound.Contains(request.Model, StringComparer.OrdinalIgnoreCase)
                ? AiConnectionPhase.Success("modelo")
                : AiConnectionPhase.Failed(
                    "modelo",
                    $"O fornecedor não listou o modelo '{request.Model}' — confira o nome exato (não invente modelos).");
        }

        var ok = connectivity.Status == AiPhaseStatus.Success
            && authentication.Status == AiPhaseStatus.Success
            && models.Status == AiPhaseStatus.Success
            && modelCheck?.Status != AiPhaseStatus.Failed;

        return new AiConnectionValidationResult(
            Ok: ok,
            Connectivity: connectivity,
            Authentication: authentication,
            Models: models,
            ModelsFound: modelsFound,
            ModelCheck: modelCheck);
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }

    private async Task<AiConnectionPhase> ProbeConnectivityAsync(
        string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(
                baseUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return AiConnectionPhase.Success("conectividade");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AiConnectionPhase.Failed(
                "conectividade",
                $"Esgotou o tempo ({ProbeTimeout.TotalSeconds:0}s) ao alcançar o endpoint — verifique a rede e o endereço.");
        }
        catch (HttpRequestException ex)
        {
            return AiConnectionPhase.Failed(
                "conectividade",
                $"Não foi possível alcançar o endereço: {DescribeNetworkError(ex)}.");
        }
    }

    private async Task<(
        AiConnectionPhase Authentication,
        AiConnectionPhase Models,
        IReadOnlyList<string> ModelsFound)> ProbeModelsAsync(
            AiConnectionValidationRequest request,
            string baseUrl,
            CancellationToken cancellationToken)
    {
        var requiresKey = request.Protocol is AiProtocol.Gemini or AiProtocol.Anthropic;
        if (requiresKey && string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return (
                AiConnectionPhase.Failed(
                    "autenticação",
                    $"A API key é obrigatória para o protocolo {request.Protocol}."),
                AiConnectionPhase.Skipped("modelos"),
                Array.Empty<string>());
        }

        using var probe = BuildModelsProbe(request, baseUrl);
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(probe, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (
                AiConnectionPhase.Skipped("autenticação"),
                AiConnectionPhase.Failed(
                    "modelos",
                    $"Esgotou o tempo ({ProbeTimeout.TotalSeconds:0}s) ao consultar a lista de modelos."),
                Array.Empty<string>());
        }
        catch (HttpRequestException ex)
        {
            return (
                AiConnectionPhase.Skipped("autenticação"),
                AiConnectionPhase.Failed(
                    "modelos",
                    $"Falha de rede ao consultar a lista de modelos: {DescribeNetworkError(ex)}."),
                Array.Empty<string>());
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                return (
                    AiConnectionPhase.Failed(
                        "autenticação",
                        $"API key recusada pelo fornecedor (401){ExtractProviderMessage(body)}."),
                    AiConnectionPhase.Skipped("modelos"),
                    Array.Empty<string>());
            }

            if (response.StatusCode is HttpStatusCode.Forbidden)
            {
                return (
                    AiConnectionPhase.Failed(
                        "autenticação",
                        $"A key foi aceita, mas não tem permissão para listar modelos (403){ExtractProviderMessage(body)}."),
                    AiConnectionPhase.Skipped("modelos"),
                    Array.Empty<string>());
            }

            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return (
                    AiConnectionPhase.Success("autenticação"),
                    AiConnectionPhase.Failed(
                        "modelos",
                        "Endpoint de modelos não encontrado (404) — o base URL ou o protocolo está errado."),
                    Array.Empty<string>());
            }

            if ((int)response.StatusCode == 429)
            {
                return (
                    AiConnectionPhase.Success("autenticação"),
                    AiConnectionPhase.Failed(
                        "modelos",
                        "O fornecedor limitou a requisição (429 — rate limit); tente novamente em instantes."),
                    Array.Empty<string>());
            }

            if (!response.IsSuccessStatusCode)
            {
                return (
                    AiConnectionPhase.Success("autenticação"),
                    AiConnectionPhase.Failed(
                        "modelos",
                        $"O fornecedor devolveu {(int)response.StatusCode} ao listar modelos{ExtractProviderMessage(body)}."),
                    Array.Empty<string>());
            }

            try
            {
                var models = ParseModels(request.Protocol, body);
                return (
                    AiConnectionPhase.Success("autenticação"),
                    models.Count > 0
                        ? AiConnectionPhase.Success("modelos")
                        : AiConnectionPhase.Failed(
                            "modelos",
                            "A autenticação funcionou, mas a lista de modelos veio vazia — confira as permissões da key."),
                    models);
            }
            catch (JsonException)
            {
                return (
                    AiConnectionPhase.Success("autenticação"),
                    AiConnectionPhase.Failed(
                        "modelos",
                        "Resposta inválida ao listar modelos — o endpoint não parece ser a API esperada para este protocolo."),
                    Array.Empty<string>());
            }
        }
    }

    private static HttpRequestMessage BuildModelsProbe(
        AiConnectionValidationRequest request, string baseUrl)
    {
        var probe = request.Protocol switch
        {
            AiProtocol.Gemini => new HttpRequestMessage(
                HttpMethod.Get, $"{baseUrl}/models?key={request.ApiKey}"),
            AiProtocol.Anthropic => AnthropicProbe($"{baseUrl}/v1/models", request.ApiKey!),
            AiProtocol.Ollama => new HttpRequestMessage(
                HttpMethod.Get, $"{baseUrl}/api/tags"),
            _ => OpenAiProbe($"{baseUrl}/models", request.ApiKey),
        };
        probe.Headers.Accept.ParseAdd("application/json");
        return probe;
    }

    private static HttpRequestMessage OpenAiProbe(string url, string? apiKey)
    {
        var probe = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(apiKey))
            probe.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return probe;
    }

    private static HttpRequestMessage AnthropicProbe(string url, string apiKey)
    {
        var probe = new HttpRequestMessage(HttpMethod.Get, url);
        probe.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        probe.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        return probe;
    }

    private static IReadOnlyList<string> ParseModels(AiProtocol protocol, string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return protocol switch
        {
            // OpenAI-compatible: {"data":[{"id":"..."}]} — também Anthropic.
            AiProtocol.OpenAiCompatible or AiProtocol.Anthropic
                => ReadIds(root, "data", "id"),
            // Gemini: {"models":[{"name":"models/..."}]} — prefixo removido.
            AiProtocol.Gemini
                => ReadIds(root, "models", "name")
                    .Select(name => name.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                        ? name["models/".Length..]
                        : name)
                    .ToArray(),
            // Ollama: {"models":[{"name":"..."}]}.
            AiProtocol.Ollama => ReadIds(root, "models", "name"),
            _ => Array.Empty<string>(),
        };
    }

    private static IReadOnlyList<string> ReadIds(
        JsonElement root, string arrayName, string idProperty)
    {
        if (!root.TryGetProperty(arrayName, out var array)
            || array.ValueKind != JsonValueKind.Array)
            throw new JsonException($"'{arrayName}' ausente.");

        var ids = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty(idProperty, out var value)
                && value.GetString() is { Length: > 0 } id)
                ids.Add(id);
        }

        return ids;
    }

    private static string ExtractProviderMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var message = root.TryGetProperty("error", out var error)
                ? error.ValueKind switch
                {
                    JsonValueKind.Object when error.TryGetProperty("message", out var m)
                        => m.GetString(),
                    JsonValueKind.String => error.GetString(),
                    _ => null,
                }
                : null;

            return string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : $" ({Truncate(message, 160)})";
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string DescribeNetworkError(HttpRequestException ex)
    {
        var message = ex.Message;
        return message.Contains("No such host", StringComparison.OrdinalIgnoreCase)
            ? "o endereço não existe (DNS)"
            : message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
                ? "a conexão foi recusada (porta fechada ou serviço parado)"
                : Truncate(message, 200);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";
}
