namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// F26 Fase 3.1: site do UniFi Controller (cadastro mínimo —
/// `GET /api/self/sites`; id estável = `short_id`).
/// </summary>
public sealed record UnifiSiteDto(
    string ShortId,
    string Name,
    string Description,
    bool Hidden);
