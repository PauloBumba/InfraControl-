namespace InfraControl.Api.ViewModels.Network;

public sealed record CreateConnectionViewModel(Guid SourceInterfaceId, Guid TargetInterfaceId);

public sealed record UpdateConnectionViewModel(Guid SourceInterfaceId, Guid TargetInterfaceId);
