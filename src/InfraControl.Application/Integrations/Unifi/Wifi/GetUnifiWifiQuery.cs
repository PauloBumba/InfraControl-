using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Integrations.Unifi.Wifi;

/// <summary>
/// F18-T006 (decisão 2026-10-10, docs/29 §WiFi): redes SSID e telemetria
/// de AP lidas AO VIVO no UniFi Controller — nenhuma métrica é persistida
/// no InfraControl (regra 8 da integração, ADR-028); a janela de sondagem
/// encurta com cache Redis de 60s.
/// </summary>
public sealed record GetUnifiWifiQuery
    : IQuery<UnifiWifiDto>, IAuthorizedRequest
{
    /// <summary>Qualquer papel autenticado lê (leitura de dashboard).</summary>
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetUnifiWifiQueryHandler(IUnifiApiClient client, ICacheService cache)
    : IRequestHandler<GetUnifiWifiQuery, UnifiWifiDto>
{
    public const string CacheKey = "integration:unifi:wifi";

    /// <summary>TTL curto: telemetria fresca sem refazer a sonda a cada
    /// navegação (decisão da task: sob demanda + cache).</summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<UnifiWifiDto> Handle(
        GetUnifiWifiQuery request, CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync<UnifiWifiDto>(CacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // Sequencial de propósito: as duas chamadas reutilizam a MESMA
        // sessão do gate de login do UnifiApiClient — paralelizar duplicaria
        // o login no Controller (mesma razão do Insights, §Insights).
        var ssids = await client.ListSsidsAsync(cancellationToken);
        var accessPoints = await client.ListAccessPointsAsync(cancellationToken);

        var dto = new UnifiWifiDto(ssids, accessPoints, DateTimeOffset.UtcNow);
        await cache.SetAsync(CacheKey, dto, CacheTtl, cancellationToken);
        return dto;
    }
}
