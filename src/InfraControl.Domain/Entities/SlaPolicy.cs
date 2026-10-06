using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Política de SLA — compromisso de resposta e resolução (docs/04).
/// `ServiceId` opcional: política global (null) ou vinculada a um Service
/// (F8-T001). O prazo aplicado é snapshotado no TaskSla (F8-T003) para
/// preservar os prazos históricos.
/// </summary>
public sealed class SlaPolicy : AggregateRoot
{
    public const int NameMaxLength = 120;

    public string Name { get; private set; } = string.Empty;
    public int ResponseMinutes { get; private set; }
    public int ResolutionMinutes { get; private set; }
    public Guid? ServiceId { get; private set; }

    private SlaPolicy() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new SlaPolicyDeletedDomainEvent(Id, Name));

    public static SlaPolicy Create(
        string name,
        int responseMinutes,
        int resolutionMinutes,
        Guid? serviceId = null)
    {
        var policy = new SlaPolicy();
        policy.Update(name, responseMinutes, resolutionMinutes, serviceId);

        // ADR-030 §5 — descarta o Updated transitório do Update interno.
        policy.ClearDomainEvents();
        policy.RaiseDomainEvent(new SlaPolicyCreatedDomainEvent(
            policy.Id, policy.Name, policy.ResponseMinutes, policy.ResolutionMinutes));
        return policy;
    }

    public void Update(
        string name,
        int responseMinutes,
        int resolutionMinutes,
        Guid? serviceId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da política de SLA é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome da política de SLA pode ter no máximo {NameMaxLength} caracteres.",
                nameof(name));

        if (responseMinutes <= 0)
            throw new ArgumentException(
                "O prazo de resposta deve ser maior que zero.", nameof(responseMinutes));

        if (resolutionMinutes <= 0)
            throw new ArgumentException(
                "O prazo de resolução deve ser maior que zero.", nameof(resolutionMinutes));

        Name = name;
        ResponseMinutes = responseMinutes;
        ResolutionMinutes = resolutionMinutes;
        ServiceId = serviceId;

        RaiseDomainEvent(new SlaPolicyUpdatedDomainEvent(Id, Name));
    }
}
