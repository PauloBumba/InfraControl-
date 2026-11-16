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
        // F53-T003: falha de UMA fonte não derruba a outra — a seção
        // afetada volta vazia com *Error preenchido. Só
        // HttpRequestException é capturada (erro HTTP do Controller);
        // config ausente e payload inesperado continuam propagando.
        var ssids = await ProbeAsync(() => client.ListSsidsAsync(cancellationToken));
        var accessPoints = await ProbeAsync(() => client.ListAccessPointsAsync(cancellationToken));

        // As duas fontes falharam → propaga o erro original (502 via
        // GlobalExceptionHandler) em vez de um DTO vazio fingindo sucesso.
        if (ssids.Error is not null && accessPoints.Error is not null)
            throw ssids.Error!;

        var dto = new UnifiWifiDto(
            ssids.Value ?? [],
            accessPoints.Value ?? [],
            DateTimeOffset.UtcNow,
            SsidsError: ssids.Error is null ? null : SectionUnavailableMessage,
            AccessPointsError: accessPoints.Error is null ? null : SectionUnavailableMessage);
        await cache.SetAsync(CacheKey, dto, CacheTtl, cancellationToken);
        return dto;
    }

    private const string SectionUnavailableMessage =
        "Seção indisponível nesta versão do UniFi Controller.";

    private static async Task<ProbeResult<T>> ProbeAsync<T>(
        Func<Task<IReadOnlyList<T>>> probe)
    {
        try
        {
            return new ProbeResult<T>(await probe(), null);
        }
        catch (HttpRequestException ex)
        {
            return new ProbeResult<T>(null, ex);
        }
    }

    private sealed record ProbeResult<T>(IReadOnlyList<T>? Value, HttpRequestException? Error);
}
