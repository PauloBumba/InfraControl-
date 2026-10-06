using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Objetivo de SLO de um Service (docs/04: "Availability &gt;= 99.95%,
/// Window = 30 days"). `TargetPercent`/`WindowDays` são o compromisso
/// medido pelas `SloMeasurement` (F8-T006) orçado pela `SloBudget`
/// (F8-T007).
/// </summary>
public sealed class Slo : AggregateRoot
{
    public const int NameMaxLength = 120;

    public string Name { get; private set; } = string.Empty;
    public decimal TargetPercent { get; private set; }
    public int WindowDays { get; private set; }
    public Guid ServiceId { get; private set; }

    private Slo() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new SloDeletedDomainEvent(Id, Name));

    public static Slo Create(
        string name,
        decimal targetPercent,
        int windowDays,
        Guid serviceId)
    {
        var slo = new Slo();
        slo.Update(name, targetPercent, windowDays, serviceId);

        // ADR-030 §5 — descarta o Updated transitório do Update interno.
        slo.ClearDomainEvents();
        slo.RaiseDomainEvent(new SloCreatedDomainEvent(
            slo.Id, slo.Name, slo.TargetPercent, slo.ServiceId));
        return slo;
    }

    public void Update(
        string name,
        decimal targetPercent,
        int windowDays,
        Guid serviceId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do SLO é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome do SLO pode ter no máximo {NameMaxLength} caracteres.",
                nameof(name));

        if (targetPercent <= 0 || targetPercent > 100)
            throw new ArgumentException(
                "O alvo do SLO deve estar entre 0 (exclusivo) e 100.",
                nameof(targetPercent));

        if (windowDays <= 0)
            throw new ArgumentException(
                "A janela do SLO deve ser maior que zero.", nameof(windowDays));

        if (serviceId == Guid.Empty)
            throw new ArgumentException(
                "O serviço do SLO é obrigatório.", nameof(serviceId));

        Name = name;
        TargetPercent = targetPercent;
        WindowDays = windowDays;
        ServiceId = serviceId;

        RaiseDomainEvent(new SloUpdatedDomainEvent(Id, Name, TargetPercent));
    }
}
