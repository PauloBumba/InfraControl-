using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfraControl.Application.Operations.Cluster.Ports;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Cliente HTTP do compose-agent (docs/65 Fase 3): container dedicado na
/// rede interna do compose que executa `docker compose up --scale`.
/// BaseUrl/token vêm de ComposeAgent__* (appsettings/compose env).
/// Timeout longo no client nomeado — scale pode levar dezenas de segundos.
/// </summary>
public sealed class ComposeAgentClient(IHttpClientFactory httpClientFactory)
    : IComposeAgent
{
    public const string HttpClientName = "compose-agent";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<ComposeServiceDto>?> ListServicesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var json = await client.GetStringAsync("/services", cancellationToken);
            var doc = JsonSerializer.Deserialize<ServicesResponse>(json, JsonOptions);
            return doc?.Services?.Select(s => new ComposeServiceDto(
                s.Service ?? string.Empty,
                s.Total,
                s.Running,
                s.Containers?.Select(c => new ComposeServiceContainerDto(
                    c.Name ?? string.Empty, c.State ?? string.Empty, c.Status ?? string.Empty))
                    .ToArray() ?? [])).ToArray() ?? [];
        }
        catch
        {
            // Agente fora do ar / token divergente / timeout = indisponível.
            return null;
        }
    }

    public async Task<ComposeScaleResult?> ScaleAsync(
        string service,
        int replicas,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsJsonAsync(
                $"/services/{Uri.EscapeDataString(service)}/scale",
                new { replicas },
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonSerializer.Deserialize<ScaleResponse>(json, JsonOptions);
            return new ComposeScaleResult(
                service,
                replicas,
                doc?.Success ?? false,
                doc?.Output ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    private sealed class ServicesResponse
    {
        [JsonPropertyName("services")]
        public List<ServiceEntry>? Services { get; set; }
    }

    private sealed class ServiceEntry
    {
        [JsonPropertyName("service")]
        public string? Service { get; set; }
        [JsonPropertyName("total")]
        public int Total { get; set; }
        [JsonPropertyName("running")]
        public int Running { get; set; }
        [JsonPropertyName("containers")]
        public List<ContainerEntry>? Containers { get; set; }
    }

    private sealed class ContainerEntry
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("state")]
        public string? State { get; set; }
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private sealed class ScaleResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("output")]
        public string? Output { get; set; }
    }
}
