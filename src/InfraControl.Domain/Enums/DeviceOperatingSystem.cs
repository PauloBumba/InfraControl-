namespace InfraControl.Domain.Enums;

/// <summary>
/// Sistema operacional informado do equipamento (F34-T006, ADR-060).
/// Null = não informado (ficha não oferece "Acesso remoto"). Só faz
/// sentido para tipos com acesso remoto (Server/Workstation/VM/Router/
/// Firewall — guard em <see cref="Entities.Device.Update"/>). Persistido
/// como string (precedente <see cref="DeviceType"/>).
/// </summary>
public enum DeviceOperatingSystem
{
    Windows = 0,
    Linux = 1,
    Other = 2
}
