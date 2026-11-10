using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Projects.Ports;

/// <summary>
/// Porta do repositório das solicitações de conclusão de Projeto
/// (spec evolução 2026-11-07 — conclusão/aprovação formal).
/// </summary>
public interface IProjectConclusionRepository : IRepository<ProjectConclusion>
{
    Task<IReadOnlyCollection<ProjectConclusion>> ListByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectConclusion?> GetPendingByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
