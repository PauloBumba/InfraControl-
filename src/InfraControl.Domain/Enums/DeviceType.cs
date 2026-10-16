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
<<<<<<< HEAD

    /// <summary>
    /// F30-T005c (docs/46): gravador NVR — câmera e NVR são devices
    /// distintos no inventário e a associação é CameraNvrMapping. Aditivo
    /// no fim; persistido como string (HasConversion em DeviceConfiguration).
    /// </summary>
    Nvr = 11
=======
    // F31-T001 / docs/53 D2: VMs e vCenter passam a existir no inventário —
    // a mesma ideia do F3-T001 (Switch = Device com type próprio).
    VirtualMachine = 11,
    VCenter = 12
>>>>>>> 6d1c9f3a66083b23e4de4c0529690bfcb44755e5
}
