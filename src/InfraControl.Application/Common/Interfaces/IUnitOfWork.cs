namespace InfraControl.Application.Common.Interfaces;

public interface IUnitOfWork : InfraControl.BuildingBlocks.Domain.IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
