using InfraControl.Api.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.UnitTests.Api;

public sealed class DataProtectionSetupTests
{
    [Fact]
    public void AddInfraControlDataProtection_TwoProviders_ShouldSharePersistedKeys()
    {
        var keyRingPath = Path.Combine(
            Path.GetTempPath(), "infracontrol-keyring-tests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DataProtectionSetup.KeyRingPathConfigurationKey] = keyRingPath
            })
            .Build();

        try
        {
            var firstServices = new ServiceCollection();
            firstServices.AddInfraControlDataProtection(configuration);
            using var firstProvider = firstServices.BuildServiceProvider();
            var protectedValue = firstProvider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("InfraControl.UnitTests")
                .Protect("shared-authentication-cookie");

            var secondServices = new ServiceCollection();
            secondServices.AddInfraControlDataProtection(configuration);
            using var secondProvider = secondServices.BuildServiceProvider();
            var unprotectedValue = secondProvider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("InfraControl.UnitTests")
                .Unprotect(protectedValue);

            unprotectedValue.Should().Be("shared-authentication-cookie");
        }
        finally
        {
            if (Directory.Exists(keyRingPath))
            {
                Directory.Delete(keyRingPath, recursive: true);
            }
        }
    }
}
