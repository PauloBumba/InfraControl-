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

    Task<PagedResult<Project>> SearchAsync(
        string? term,
        ProjectStatus? status,
        ProjectPriority? priority,
        Guid? leaderId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Project>> ListAllAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
