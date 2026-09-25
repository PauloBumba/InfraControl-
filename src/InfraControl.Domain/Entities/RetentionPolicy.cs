using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Política de retenção por tipo de dado (F12-T002, docs/03/docs/11):
/// sobrescreve o default de <c>Retention:*</c> em runtime; ausente, o
/// cleanup usa a configuração.
/// </summary>
public sealed class RetentionPolicy : Entity
{
    public const int MinDays = 1;
    public const int MaxDays = 3650;

    public RetentionDataType DataType { get; private set; }
    public int Days { get; private set; }

    public static RetentionPolicy Create(RetentionDataType dataType, int days)
    {
        if (!Enum.IsDefined(dataType))
            throw new ArgumentOutOfRangeException(
                nameof(dataType), dataType, "Tipo de dado de retenção inválido.");

        return new RetentionPolicy
        {
            DataType = dataType,
            Days = ValidateDays(days)
        };
    }

    public void Update(int days) => Days = ValidateDays(days);

    private static int ValidateDays(int days)
    {
        if (days is < MinDays or > MaxDays)
            throw new ArgumentException(
                $"Os dias de retenção devem estar entre {MinDays} e {MaxDays}.",
                nameof(days));

        return days;
    }
}
