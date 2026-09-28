namespace InfraControl.Application.Sla.Services;

/// <summary>
/// Payload de Service devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record ServiceDto(Guid Id, string Name, string? Description);
