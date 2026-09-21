using InfraControl.Domain.Identity;

namespace InfraControl.Application.Identity;

public interface IAdminBootstrapper
{
    Task<AdminBootstrapResult> RunAsync(CancellationToken cancellationToken = default);
}

public interface IAdminIdentityProvisioner
{
    Task<bool> HasAdminAsync(CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task ProvisionAsync(User user, string password, CancellationToken cancellationToken = default);
}

public sealed record AdminBootstrapResult(bool Created, bool AlreadyConfigured, string? Reason = null);
