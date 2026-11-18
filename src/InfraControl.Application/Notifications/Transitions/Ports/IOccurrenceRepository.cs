using InfraControl.Domain.Entities;

namespace InfraControl.Application.Notifications.Transitions.Ports;

/// <summary>
/// Porta de persistência do estado atual das ocorrências e do histórico
/// de transições (gate central de notificação por mudança de estado).
/// </summary>
public interface IOccurrenceRepository
{
    /// <summary>Occurrence por chave (entidade rastreada para update).</summary>
    Task<Occurrence?> GetByKeyAsync(
        string occurrenceKey, CancellationToken cancellationToken = default);

    /// <summary>Recarrega a entidade do banco (após corrida de concorrência).</summary>
    Task ReloadAsync(Occurrence occurrence, CancellationToken cancellationToken = default);

    /// <summary>Desfaz um Add pendente (corrida de INSERT na chave única).</summary>
    void Remove(Occurrence occurrence);

    /// <summary>Desfaz um Add de transição pendente.</summary>
    void RemoveTransition(OccurrenceTransition transition);

    Task AddAsync(Occurrence occurrence, CancellationToken cancellationToken = default);

    Task AddTransitionAsync(
        OccurrenceTransition transition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste as alterações pendentes da ocorrência/transição.
    /// false = conflito de concorrência (outra decisão avançou o estado
    /// primeiro) — o chamador deve recarregar e reavaliar; demais erros
    /// propagam para o retry do broker.
    /// </summary>
    Task<bool> TrySaveAsync(CancellationToken cancellationToken = default);
}
