namespace InfraControl.Api.HealthChecks;

/// <summary>
/// Leitura de <c>Health:Services</c> (F46-T006 + tela de status F50):
/// o compose pré-declara slots para serviços de profile opcional (docs/66
/// D7) com valor vazio — entradas sem Name/Url são descartadas para não
/// virarem probe morto Unhealthy.
/// </summary>
public static class HealthServiceEndpoints
{
    public static IReadOnlyList<HealthServiceEndpoint> FromConfiguration(
        IConfiguration configuration)
    {
        var endpoints = configuration
            .GetSection("Health:Services")
            .Get<List<HealthServiceEndpoint>>() ?? [];

        return endpoints
            .Select(endpoint => endpoint with
            {
                Name = endpoint.Name?.Trim() ?? string.Empty,
                Url = endpoint.Url?.Trim() ?? string.Empty,
                Link = string.IsNullOrWhiteSpace(endpoint.Link)
                    ? null
                    : endpoint.Link.Trim(),
            })
            .Where(endpoint =>
                endpoint.Name.Length > 0 && endpoint.Url.Length > 0)
            .ToArray();
    }
}
