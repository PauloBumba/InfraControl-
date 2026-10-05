namespace InfraControl.Api.ViewModels.Monitoring;

public sealed record CreateMonitoringCheckViewModel(
    Guid DeviceId,
    string Check,
    bool Enabled = true);

public sealed record UpdateMonitoringCheckViewModel(bool Enabled);
