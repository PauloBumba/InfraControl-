using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Pdi.Ports;

/// <summary>
/// PDI — 1 plano vigente por usuário (docs/29 §8, decisão 2026-10-03);
/// timeline de eventos em <see cref="PdiHistoryEntry"/> (docs/30 §17,
/// ADR-029). A listagem traz dono e gestor resolvidos (join em
/// DomainUsers) porque o front não tem como resolver nomes sem a policy
/// Administer de /users.
/// </summary>
public interface IPdiRepository : IRepository<IndividualDevelopmentPlan>
{
    Task<IndividualDevelopmentPlan?> GetByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Planos com dono (DisplayName + e-mail) e nome do gestor (quando
    /// definido). <paramref name="onlyUserId"/> restringe ao próprio
    /// usuário (visão do Operador); null = todos (Admin).
    /// </summary>
    Task<IReadOnlyCollection<PdiWithOwner>> ListWithOwnersAsync(
        Guid? onlyUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Planos em que o usuário é o gestor (dashboard do §19) — mesmos
    /// joins de <see cref="ListWithOwnersAsync"/>, ordenados por dono.
    /// </summary>
    Task<IReadOnlyCollection<PdiWithOwner>> ListByManagerAsync(
        Guid managerId, CancellationToken cancellationToken = default);

    /// <summary>Dados do dono para o retorno do upsert — null = usuário inexistente (404).</summary>
    Task<PlanOwner?> GetOwnerAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Nome exibido de qualquer usuário (gestor etc.) — null = inexistente.</summary>
    Task<string?> GetUserNameAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Timeline do plano do usuário, mais recente primeiro, com o nome
    /// do ator resolvido (null = ator removido).
    /// </summary>
    Task<IReadOnlyCollection<PdiHistoryWithActor>> ListHistoryAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Enfileira entradas de histórico (persistidas no mesmo save — ADR-029 §4).</summary>
    void AddHistoryRange(IReadOnlyCollection<PdiHistoryEntry> entries);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record PdiWithOwner(
    IndividualDevelopmentPlan Plan,
    string OwnerName,
    string OwnerEmail,
    string? ManagerName);

public sealed record PlanOwner(string Name, string Email);

public sealed record PdiHistoryWithActor(PdiHistoryEntry Entry, string? ActorName);
