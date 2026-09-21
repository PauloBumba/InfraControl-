using InfraControl.Domain.Enums;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Identity;

public sealed class AdminBootstrapper(
    IAdminIdentityProvisioner provisioner,
    BootstrapAdminOptions options,
    ILogger<AdminBootstrapper> logger) : IAdminBootstrapper
{
    public async Task<AdminBootstrapResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
            return new AdminBootstrapResult(false, false, "Bootstrap is disabled.");

        Validate(options);

        if (await provisioner.HasAdminAsync(cancellationToken))
        {
            logger.LogInformation("Bootstrap Admin skipped because an administrator already exists.");
            return new AdminBootstrapResult(false, true, "An administrator already exists.");
        }

        var email = options.Email.Trim().ToLowerInvariant();
        if (await provisioner.EmailExistsAsync(email, cancellationToken))
        {
            logger.LogInformation("Bootstrap Admin skipped because the configured email already exists.");
            return new AdminBootstrapResult(false, true, "The configured email already exists.");
        }

        var user = User.Create(Guid.NewGuid(), email, options.DisplayName, UserRole.Admin);
        await provisioner.ProvisionAsync(user, options.Password, cancellationToken);

        logger.LogInformation("Bootstrap Admin created the first administrator with id {UserId}.", user.Id);
        return new AdminBootstrapResult(true, false);
    }

    private static void Validate(BootstrapAdminOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Email) || !settings.Email.Contains('@'))
            throw new InvalidOperationException("BootstrapAdmin:Email must be a valid email.");
        if (string.IsNullOrWhiteSpace(settings.DisplayName))
            throw new InvalidOperationException("BootstrapAdmin:DisplayName is required.");
        if (string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException("BootstrapAdmin:Password must be provided through a secret environment variable.");
    }
}
