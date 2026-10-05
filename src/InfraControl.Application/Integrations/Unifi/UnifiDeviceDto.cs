namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// Cadastro mínimo de um dispositivo UniFi (F20-T001, regra 8): só
/// identidade para mapear o <c>Device</c> — telemetria (PoE, tráfego,
/// clientes) não é persistida, é consultada sob demanda (F20-T003).
/// </summary>
public sealed record UnifiDeviceDto(
    string Mac,
    string Name,
    string Model,
    string Type,
    string Ip);
