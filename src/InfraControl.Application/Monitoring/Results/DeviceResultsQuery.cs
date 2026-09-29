using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Monitoring.Results;

/// <summary>Um poll registrado no histórico (F5-T002).</summary>
public sealed record MonitoringResultDto(
    Guid Id,
    string Check,
    bool Succeeded,
    long DurationMs,
    string? Error,
    IReadOnlyDictionary<string, string>? Values,
    DateTime CheckedAt);

/// <summary>Histórico de polls de um device.</summary>
public sealed record DeviceResultsDto(
    Guid DeviceId,
    string DeviceName,
    IReadOnlyList<MonitoringResultDto> Results);

/// <summary>Últimos polls do device, mais recente primeiro (F5-T002).</summary>
public sealed record GetDeviceResultsQuery(Guid Id, int Limit = 50)
    : IQuery<DeviceResultsDto>, IAuthorizedRequest
{
    public const int MinLimit = 1;
    public const int MaxLimit = 500;

    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetDeviceResultsQueryHandler(
    IDeviceRepository deviceRepository,
    IMonitoringResultRepository resultRepository)
    : IRequestHandler<GetDeviceResultsQuery, DeviceResultsDto>
{
    public async Task<DeviceResultsDto> Handle(
        GetDeviceResultsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Limit is < GetDeviceResultsQuery.MinLimit or > GetDeviceResultsQuery.MaxLimit)
            throw new ArgumentException(
                $"O limite deve estar entre {GetDeviceResultsQuery.MinLimit} e {GetDeviceResultsQuery.MaxLimit}.",
                nameof(request.Limit));

        var device = await deviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.Id}' não encontrado.");

        var results = await resultRepository.GetRecentByDeviceAsync(
            device.Id, request.Limit, cancellationToken);

        return new DeviceResultsDto(
            device.Id,
            device.Name,
            results
                .Select(result => new MonitoringResultDto(
                    result.Id,
                    result.Kind.ToString(),
                    result.Succeeded,
                    result.DurationMs,
                    result.Error,
                    result.Values,
                    result.CreateDateTime))
                .ToArray());
    }
}
