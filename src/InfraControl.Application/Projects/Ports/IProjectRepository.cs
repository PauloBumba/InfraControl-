using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Projects.Ports;

/// <summary>
/// Porta do repositório do agregado Project (ADR-043 · docs/38).
/// </summary>
public interface IProjectRepository : IRepository<Project>
{
    Task<Project?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<string> GetNextCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca paginada. Quando <paramref name="visibleToUserId"/> é informado
    /// (usuário não-Admin), restringe a projetos onde o usuário é líder,
    /// criador ou membro — spec evolução 2026-11-07 §5.
    /// </summary>
    Task<PagedResult<Project>> SearchAsync(
        string? term,
        ProjectStatus? status,
        ProjectPriority? priority,
        Guid? leaderId,
        int page,
        int pageSize,
        Guid? visibleToUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Project>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve DisplayName (fallback e-mail) dos usuários referenciados pelo
    /// projeto — spec §4: Username para identificação visual, sem trocar as
    /// chaves estrangeiras.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
