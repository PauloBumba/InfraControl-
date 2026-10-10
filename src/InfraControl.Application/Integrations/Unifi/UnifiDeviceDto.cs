namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// Cadastro mínimo de um dispositivo UniFi (F20-T001, regra 8): só
/// identidade para mapear o <c>Device</c> — telemetria (PoE, tráfego,
/// clientes) não é persistida, é consultada sob demanda (F20-T003).
/// F26 Fase 3: <c>SiteId</c>/<c>State</c>/<c>Firmware</c> são campos de
/// CADASTRO/estado informado pelo controller — o DTO os expõe; o que vai
/// ao banco permanece só o que a entidade <c>Device</c> suporta (REGRA 8).
/// </summary>
public sealed record UnifiDeviceDto(
    string Mac,
    string Name,
    string Model,
    string Type,
    string Ip,
    string? SiteId = null,
    string? State = null,
    string? Firmware = null);
