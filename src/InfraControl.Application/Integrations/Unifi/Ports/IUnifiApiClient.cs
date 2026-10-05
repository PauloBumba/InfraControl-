namespace InfraControl.Application.Integrations.Unifi.Ports;

/// <summary>
/// Porta de saída para o UniFi Controller (F20-T001/ADR-028 §2/§5) —
/// autenticação por sessão <c>unifises</c> e re-auth automática em 401
/// ficam na implementação; aqui só o contrato de cadastro/health.
/// </summary>
public interface IUnifiApiClient
{
    /// <summary>Lista os dispositivos (Gateway/Switch/AP) do Controller.</summary>
    Task<IReadOnlyList<UnifiDeviceDto>> ListDevicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Sondagem leve (GET /api/self) para o health da integração.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
