using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Integrations.Unifi.Insights;

/// <summary>
/// F18-T007 (decisão 2026-10-09, docs/29 §Insights): painéis reais da
/// InsightsPage — Tráfego por Cliente e DPI por Aplicação, sondados ao
/// vivo no UniFi Controller (nenhuma métrica é persistida — regra 8 da
/// integração; ver docs/29). Speed test fica fora: não existe no
/// Controller e exige coletor próprio (escopo adiado, decidido na task).
/// </summary>
public sealed record GetUnifiInsightsQuery
    : IQuery<UnifiInsightsDto>, IAuthorizedRequest
{
    /// <summary>Qualquer papel autenticado lê (leitura de dashboard).</summary>
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetUnifiInsightsQueryHandler(IUnifiApiClient client)
    : IRequestHandler<GetUnifiInsightsQuery, UnifiInsightsDto>
{
    /// <summary>
    /// Teto de linhas por painel (UI não precisa de mais que o top 50 —
    /// o Controller pode devolver centenas de clientes/apps).
    /// </summary>
    public const int MaxEntries = 50;

    public async Task<UnifiInsightsDto> Handle(
        GetUnifiInsightsQuery request, CancellationToken cancellationToken)
    {
        // Sequencial de propósito: as duas chamadas reutilizam a MESMA
        // sessão (gate de login do UnifiApiClient) — paralelizar duplicaria
        // o login em sessão expirada sem ganho (duas chamadas leves).
        var traffic = await client.ListClientTrafficAsync(cancellationToken);
        var dpi = await client.ListDpiStatsAsync(cancellationToken);

        var trafficTop = traffic
            .OrderByDescending(client => client.TotalBytes)
            .Take(MaxEntries)
            .ToArray();

        var dpiTop = dpi
            .Where(app => app.TotalBytes > 0)
            .OrderByDescending(app => app.TotalBytes)
            .Take(MaxEntries)
            .ToArray();

        return new UnifiInsightsDto(trafficTop, dpiTop, DateTimeOffset.UtcNow);
    }
}
