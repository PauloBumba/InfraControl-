using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Retenção genérica por tabela (metodologia do usuário, 2026-11-15): o app
/// descobre as entidades do EF model e o admin atribui dias por tabela —
/// sem novo <see cref="Enums.RetentionDataType"/> para cada uma. A poda
/// remove linhas com <c>CreateDateTime</c> anterior ao cutoff.
/// </summary>
public sealed class TableRetentionPolicy : Entity
{
    public const int EntityNameMaxLength = 128;
    public const int MinDays = 1;
    public const int MaxDays = 3650;

    /// <summary>Nome do CLR type da entidade (ex.: "ScheduledReport").</summary>
    public string EntityName { get; private set; } = string.Empty;
    public int Days { get; private set; }

    private TableRetentionPolicy() { }

    public static TableRetentionPolicy Create(string entityName, int days)
    {
        var name = entityName?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > EntityNameMaxLength)
            throw new ArgumentException(
                $"Nome de entidade inválido (1..{EntityNameMaxLength}).",
                nameof(entityName));

        return new TableRetentionPolicy
        {
            EntityName = name,
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
