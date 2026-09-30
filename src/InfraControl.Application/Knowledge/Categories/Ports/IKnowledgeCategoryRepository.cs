using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Knowledge.Categories.Ports;

public interface IKnowledgeCategoryRepository : IRepository<KnowledgeCategory>
{
    /// <summary>
    /// Nome duplicado (case-insensitive) = 409 (F10-T001; precedente da
    /// Location). <paramref name="excludeId"/> zera o próprio id no update.
    /// </summary>
    Task<bool> HasNameAsync(
        string name, Guid excludeId = default, CancellationToken cancellationToken = default);

    Task<PagedResult<KnowledgeCategory>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
