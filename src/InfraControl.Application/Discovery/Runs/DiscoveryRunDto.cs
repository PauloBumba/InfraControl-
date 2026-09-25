namespace InfraControl.Application.Discovery.Runs;

/// <summary>
/// Payload de DiscoveryRun devolvido pela API (sucesso → data do envelope).
/// Status como string (mesmo padrão do DeviceDto.Type).
/// </summary>
public sealed record DiscoveryRunDto(
    Guid Id,
    Guid DeviceId,
    string Status,
    DateTime StartedAt,
    DateTime? FinishedAt,
    string? ErrorMessage);
