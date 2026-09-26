using InfraControl.Application.Discovery.Results.Queries;

namespace InfraControl.Application.Discovery.Results;

/// <summary>
/// Payload de DiscoveryResult devolvido pela API (sucesso → data do
/// envelope). Kind como string (mesmo padrão de DiscoveryRunDto.Status).
/// </summary>
public sealed record DiscoveryResultDto(
    Guid Id,
    Guid DiscoveryRunId,
    string Kind,
    string Summary,
    IReadOnlyDictionary<string, string>? Values,
    Guid? DeviceId,
    DateTime CreatedAt);
