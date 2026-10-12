using Microsoft.AspNetCore.DataProtection;

namespace InfraControl.Api.Security;

public static class DataProtectionSetup
{
    public const string ApplicationName = "InfraControl";
    public const string KeyRingPathConfigurationKey = "DataProtection:KeyRingPath";

    public static void AddInfraControlDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName(ApplicationName);

        var keyRingPath = configuration[KeyRingPathConfigurationKey];
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            dataProtection.PersistKeysToFileSystem(
                new DirectoryInfo(Path.GetFullPath(keyRingPath)));
        }
    }
}
