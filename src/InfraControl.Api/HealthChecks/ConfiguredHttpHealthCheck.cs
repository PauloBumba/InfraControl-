using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InfraControl.Api.HealthChecks;

/// <summary>Endpoint HTTP de um serviço irmão da stack (F46-T006).</summary>
public sealed record HealthServiceEndpoint(string Name, string Url, string? Link);

/// <summary>
/// F46-T006 — checagem HTTP configurável por ambiente
/// (<c>Health:Services</c> no compose/env): um check por serviço irmão
/// (frontend, zabbix-web, guacamole, evolution, adminer, rabbitmq-management…).
/// O <c>Link</c> (quando houver) vai no <c>Data</c> do resultado e chega ao
/// front via <c>/health</c>, que renderiza a âncora para o console.
/// </summary>
public sealed class ConfiguredHttpHealthCheck(
    IHttpClientFactory httpClientFactory,
    string serviceName,
    string url,
    string? link = null) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object> { ["url"] = url };
        if (!string.IsNullOrWhiteSpace(link))
            data["link"] = link;

        try
        {
            using var client = httpClientFactory.CreateClient("health-probe");
            using var response = await client.GetAsync(url, cancellationToken);
            var healthy = response.IsSuccessStatusCode;
            return healthy
                ? HealthCheckResult.Healthy(
                    $"{serviceName} respondeu {(int)response.StatusCode}.", data: data)
                : HealthCheckResult.Unhealthy(
                    $"{serviceName} respondeu {(int)response.StatusCode}.", data: data);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                $"{serviceName} inacessível: {exception.Message}", exception, data);
        }
    }
}
