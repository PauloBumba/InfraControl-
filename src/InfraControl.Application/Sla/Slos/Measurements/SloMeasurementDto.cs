namespace InfraControl.Application.Sla.Slos.Measurements;

/// <summary>
/// Payload de SloMeasurement devolvido pela API.
/// </summary>
public sealed record SloMeasurementDto(
    Guid Id,
    Guid SloId,
    DateTime MeasuredAt,
    decimal ValuePercent);
