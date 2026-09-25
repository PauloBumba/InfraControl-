using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Operations.Retention.Ports;

public interface IRetentionPolicyRepository
{
    Task<IReadOnlyList<RetentionPolicy>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<RetentionPolicy?> GetByTypeAsync(
        RetentionDataType dataType, CancellationToken cancellationToken = default);

    Task AddAsync(RetentionPolicy policy, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        RetentionDataType dataType, CancellationToken cancellationToken = default);
}
