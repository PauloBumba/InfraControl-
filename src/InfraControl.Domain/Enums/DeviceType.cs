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
    // F31-T001 / docs/53 D2: VMs e vCenter passam a existir no inventário —
    // a mesma ideia do F3-T001 (Switch = Device com type próprio).
    VirtualMachine = 11,
    VCenter = 12
}
