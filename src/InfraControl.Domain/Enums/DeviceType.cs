namespace InfraControl.Domain.Enums;

/// <summary>
/// Tipos de equipamento do inventário (F2-T003 / docs/03-DOMINIO-DADOS.md).
/// </summary>
public enum DeviceType
{
    Other = 0,
    Server = 1,
    Workstation = 2,
    Switch = 3,
    Router = 4,
    Firewall = 5,
    AccessPoint = 6,
    Printer = 7,
    Camera = 8,
    Phone = 9,
    UPS = 10,

    /// <summary>
    /// F30-T005c (docs/46): gravador NVR — câmera e NVR são devices
    /// distintos no inventário e a associação é CameraNvrMapping. Aditivo
    /// no fim; persistido como string (HasConversion em DeviceConfiguration).
    /// </summary>
    Nvr = 11
}
