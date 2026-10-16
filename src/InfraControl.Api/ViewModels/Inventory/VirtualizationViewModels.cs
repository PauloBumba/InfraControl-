namespace InfraControl.Api.ViewModels.Inventory;

// F31-T002: corpos de vínculo VM → hypervisor → vCenter.
public sealed record AssignHypervisorViewModel(Guid HypervisorId);

public sealed record AssignVCenterViewModel(Guid VCenterId);
