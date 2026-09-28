namespace InfraControl.Api.ViewModels.Sla;

/// <summary>Criação de serviço (F8-T001).</summary>
public sealed record CreateServiceViewModel(string Name, string? Description);

/// <summary>Atualização de serviço — nome/descrição (F8-T001).</summary>
public sealed record UpdateServiceViewModel(string Name, string? Description);
