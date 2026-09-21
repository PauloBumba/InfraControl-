using InfraControl.Domain.Common.Enums;
using InfraControl.Domain.Identity;

namespace InfraControl.Application.Identity.Roles.Ports;

public interface IUserRoleRepository
{
    Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);
    Task SaveRoleChangeAsync(User user, UserRole role, bool assign, CancellationToken cancellationToken = default);
}
