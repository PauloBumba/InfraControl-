using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Notifications.Feed.Ports;

/// <summary>
/// F18-T001 — feed de notificações do usuário: destinatário direto OU
/// destinatário de role do usuário, uma única linha por notificação
/// (dedup por construção — docs/07), mais recentes primeiro.
/// </summary>
public interface INotificationFeedRepository : IRepository<Notification>
{
    /// <summary>Visibilidade pontual (ADR-030 §2) — mesmo predicado do feed.</summary>
    Task<Notification?> GetForUserAsync(
        Guid id,
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Notification>> SearchForUserAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
