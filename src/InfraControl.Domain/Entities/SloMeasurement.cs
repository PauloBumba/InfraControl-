using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Medição do SLO em um momento (F8-T006) — valor observado dentro da
/// janela (docs/04: `Window = 30 days`); comparação com o
/// `TargetPercent` do `Slo` dá o status do objetivo.
/// </summary>
public sealed class SloMeasurement : AggregateRoot
{
    public Guid SloId { get; private set; }
    public DateTime MeasuredAt { get; private set; }
    public decimal ValuePercent { get; private set; }

    private SloMeasurement() { }

    /// <summary>ADR-030 §5 — correção de registro (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new SloMeasurementDeletedDomainEvent(Id, SloId));

    public static SloMeasurement Create(
        Guid sloId, DateTime measuredAt, decimal valuePercent)
    {
        if (sloId == Guid.Empty)
            throw new ArgumentException("O id do SLO é obrigatório.", nameof(sloId));

        if (valuePercent < 0 || valuePercent > 100)
            throw new ArgumentException(
                "O valor medido deve estar entre 0 e 100.",
                nameof(valuePercent));

        var measurement = new SloMeasurement
        {
            SloId = sloId,
            MeasuredAt = measuredAt,
            ValuePercent = valuePercent
        };
        measurement.RaiseDomainEvent(new SloMeasurementCreatedDomainEvent(
            measurement.Id, measurement.SloId, measurement.ValuePercent));
        return measurement;
    }
}
