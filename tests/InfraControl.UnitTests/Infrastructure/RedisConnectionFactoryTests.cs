using InfraControl.Infrastructure.Caching;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class RedisConnectionFactoryTests
{
    [Fact]
    public void CreateOptions_StandaloneConnection_ShouldKeepAbortOnConnectFailDisabled()
    {
        var options = RedisConnectionFactory.CreateOptions("localhost:6379");

        options.AbortOnConnectFail.Should().BeFalse();
        options.ServiceName.Should().BeNullOrEmpty();
        options.EndPoints.Should().ContainSingle();
    }

    [Fact]
    public void CreateOptions_SentinelConnection_ShouldParseServiceAndCredentials()
    {
        var options = RedisConnectionFactory.CreateOptions(
            "sentinel-1:26379,sentinel-2:26379,sentinel-3:26379," +
            "serviceName=mymaster,password=primary-password,sentinelPassword=sentinel-password");

        options.AbortOnConnectFail.Should().BeFalse();
        options.ServiceName.Should().Be("mymaster");
        options.Password.Should().Be("primary-password");
        options.SentinelPassword.Should().Be("sentinel-password");
        options.EndPoints.Should().HaveCount(3);
    }
}
