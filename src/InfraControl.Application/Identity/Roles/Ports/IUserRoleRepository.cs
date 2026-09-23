using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Identity.Roles.Ports;

public interface IUserRoleRepository : IRepository<User>
{
    Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);
    Task SaveRoleChangeAsync(User user, UserRole role, bool assign, CancellationToken cancellationToken = default);
}
