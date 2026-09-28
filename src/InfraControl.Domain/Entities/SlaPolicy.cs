using InfraControl.Domain.Common.Entities;

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

    public static SlaPolicy Create(
        string name,
        int responseMinutes,
        int resolutionMinutes,
        Guid? serviceId = null)
    {
        var policy = new SlaPolicy();
        policy.Update(name, responseMinutes, resolutionMinutes, serviceId);
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
    }
}
