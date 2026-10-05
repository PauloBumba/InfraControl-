using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Pdi.Ports;

/// <summary>
/// PDI — 1 plano vigente por usuário (docs/29 §8, decisão 2026-10-03).
/// A listagem traz o dono resolvido (join em DomainUsers) porque o front
/// não tem como resolver nomes sem a policy Administer de /users.
/// </summary>
public interface IPdiRepository : IRepository<IndividualDevelopmentPlan>
{
    Task<IndividualDevelopmentPlan?> GetByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Planos com dono (DisplayName + e-mail). <paramref name="onlyUserId"/>
    /// restringe ao próprio usuário (visão do Operador); null = todos (Admin).
    /// </summary>
    Task<IReadOnlyCollection<PdiWithOwner>> ListWithOwnersAsync(
        Guid? onlyUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Dados do dono para o retorno do upsert — null = usuário inexistente (404).</summary>
    Task<PlanOwner?> GetOwnerAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record PdiWithOwner(
    IndividualDevelopmentPlan Plan,
    string OwnerName,
    string OwnerEmail);

public sealed record PlanOwner(string Name, string Email);
