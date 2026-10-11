using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Identity.Invitations.Ports;

public interface IInvitationRepository : IRepository<Invitation>
{
    Task<Invitation?> GetAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Invitation?> GetPendingByEmailAndProjectAsync(
        string email,
        Guid projectId,
        CancellationToken cancellationToken = default);
    Task<List<Invitation>> ListByEmailAsync(
        string email,
        int take = 100,
        CancellationToken cancellationToken = default);
    Task<List<Invitation>> SearchAsync(
        InvitationStatus? status = null,
        string? email = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);
    Task<int> CountAsync(
        InvitationStatus? status = null,
        string? email = null,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// ADR-030 §1 — troca física das roles de um convite (DELETE das linhas
    /// atuais + INSERT das novas). O replace é feito aqui de propósito: a
    /// coleção do domínio é exposta como view read-only e o fixup do EF
    /// corrompe o ChangeTracker (detecta as novas filhas como Modified).
    /// </summary>
    Task ReplaceRolesAsync(
        Guid invitationId,
        IReadOnlyCollection<UserRole> roles,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
