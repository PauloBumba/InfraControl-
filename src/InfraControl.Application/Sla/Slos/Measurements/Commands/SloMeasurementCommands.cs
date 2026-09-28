using InfraControl.Application.Sla.Slos.Measurements.Ports;
using InfraControl.Application.Sla.Slos.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Sla.Slos.Measurements.Commands;

// F8-T006: medições do SLO — 400 (valor fora de 0–100), 404 (medição/SLO),
// 409 (exclusão de SLO com medições).

public sealed record RecordSloMeasurementCommand(
    Guid SloId,
    DateTime MeasuredAt,
    decimal ValuePercent) : BaseCommand<SloMeasurementDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaManage.ToString()];
}

public sealed class RecordSloMeasurementCommandHandler(
    ISloMeasurementRepository repository,
    ISloRepository sloRepository,
    ILogger<RecordSloMeasurementCommandHandler> logger)
    : IRequestHandler<RecordSloMeasurementCommand, SloMeasurementDto>
{
    public async Task<SloMeasurementDto> Handle(
        RecordSloMeasurementCommand request, CancellationToken cancellationToken)
    {
        _ = await sloRepository.GetByIdAsync(request.SloId, cancellationToken)
            ?? throw new KeyNotFoundException($"SLO '{request.SloId}' não encontrado.");

        var measurement = SloMeasurement.Create(
            request.SloId, request.MeasuredAt, request.ValuePercent);

        await repository.AddAsync(measurement, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SloMeasurement {SloMeasurementId} recorded for SLO {SloId} ({Value}%).",
            measurement.Id, measurement.SloId, measurement.ValuePercent);

        return new SloMeasurementDto(
            measurement.Id, measurement.SloId,
            measurement.MeasuredAt, measurement.ValuePercent);
    }
}

public sealed record DeleteSloMeasurementCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaManage.ToString()];
}

public sealed class DeleteSloMeasurementCommandHandler(
    ISloMeasurementRepository repository,
    ILogger<DeleteSloMeasurementCommandHandler> logger)
    : IRequestHandler<DeleteSloMeasurementCommand>
{
    public async Task Handle(
        DeleteSloMeasurementCommand request, CancellationToken cancellationToken)
    {
        var measurement = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Medição de SLO '{request.Id}' não encontrada.");

        repository.Remove(measurement);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SloMeasurement {SloMeasurementId} deleted (SLO {SloId}).",
            measurement.Id, measurement.SloId);
    }
}
