using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Estado atual persistido de uma ocorrência (spec: "comparar sempre o
/// estado recebido com o último estado persistido"). Uma linha por
/// <see cref="OccurrenceKey"/> (ex.: "alert:{id}" unificando alerta e o
/// incidente vinculado — IncidentAlert). <see cref="Version"/> é token
/// de concorrência: duas decisões simultâneas na mesma ocorrência não
/// podem avançar o estado em paralelo.
/// </summary>
public sealed class Occurrence : Entity
{
    public const int KeyMaxLength = 200;

    public string OccurrenceKey { get; private set; } = string.Empty;
    public OccurrenceState State { get; private set; }
    public int Version { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Occurrence() { }

    public static Occurrence Create(string occurrenceKey, OccurrenceState state)
    {
        if (string.IsNullOrWhiteSpace(occurrenceKey))
            throw new ArgumentException(
                "A chave da ocorrência é obrigatória.", nameof(occurrenceKey));

        return new Occurrence
        {
            OccurrenceKey = occurrenceKey.Trim(),
            State = state,
            Version = 1,
            UpdatedAtUtc = AppTimeProvider.Current.GetUtcNow().UtcDateTime
        };
    }

    /// <summary>Avança o estado; Version++ ativa o check de concorrência no UPDATE.</summary>
    public void TransitionTo(OccurrenceState newState)
    {
        State = newState;
        Version++;
        UpdatedAtUtc = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    }
}
