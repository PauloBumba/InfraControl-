using InfraControl.Domain.Entities;

namespace InfraControl.Application.Notifications.Routing.Ports;

/// <summary>Estado completo da política de roteamento (F39-T003).</summary>
public sealed record NotificationRoutingState(
    NotificationRoutingPolicy? Policy,
    IReadOnlyList<NotificationRoutingRule> Rules);

/// <summary>
/// F39-T003: persistência da política de roteamento (ADR-063). A linha de
/// política é singleton; regras são sincronizadas por Id (upsert/remove).
/// </summary>
public interface INotificationRoutingRepository
{
    Task<NotificationRoutingState> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persiste a política (cria ou atualiza com bump de versão) e sincroniza
    /// as regras: as enviadas são criadas/atualizadas, as ausentes removidas.
    /// </summary>
    Task SaveStateAsync(
        NotificationRoutingPolicy policy,
        IReadOnlyList<NotificationRoutingRule> rules,
        CancellationToken cancellationToken);
}
