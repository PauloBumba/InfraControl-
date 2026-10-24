using FluentAssertions;
using InfraControl.Application.Integrations.Email;
using InfraControl.Application.Integrations.Email.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Integrations.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F41-T001: resolver do canal Email — banco → env com fallback e
/// deciframento quieto da senha (falha = string vazia, nunca exception
/// opaca) — espelho do Zabbix/WhatsApp.
/// </summary>
public sealed class EmailOptionsResolverTests
{
    [Fact]
    public async Task Resolve_WithoutRow_ShouldFallBackToEnvironment()
    {
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailIntegrationSetting?)null);
        var env = new Mock<IEmailEnvOptionsAccessor>();
        env.SetupGet(a => a.Current).Returns(new EmailIntegrationOptions
        {
            Host = "smtp-env.example.test",
            Port = 465,
            From = "env@example.test"
        });
        var protector = new Mock<ICredentialProtector>();

        var resolver = new EmailOptionsResolver(
            repository.Object, env.Object, protector.Object,
            NullLogger<EmailOptionsResolver>.Instance);
        var options = await resolver.ResolveAsync(CancellationToken.None);

        options.Host.Should().Be("smtp-env.example.test");
        options.Port.Should().Be(465);
    }

    [Fact]
    public async Task Resolve_WithRow_ShouldUseDatabaseAndUnprotectPassword()
    {
        var setting = EmailIntegrationSetting.Create(
            "smtp-banco.example.test", 587, "user", "enc-pw",
            "noreply@example.test", "InfraControl", useSsl: false);
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        var env = new Mock<IEmailEnvOptionsAccessor>();
        var protector = new Mock<ICredentialProtector>();
        protector.Setup(p => p.Unprotect("enc-pw")).Returns("plain-pw");

        var resolver = new EmailOptionsResolver(
            repository.Object, env.Object, protector.Object,
            NullLogger<EmailOptionsResolver>.Instance);
        var options = await resolver.ResolveAsync(CancellationToken.None);

        options.Host.Should().Be("smtp-banco.example.test");
        options.Password.Should().Be("plain-pw");
        options.From.Should().Be("noreply@example.test");
        options.FromName.Should().Be("InfraControl");
        options.UseSsl.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_WhenUnprotectFails_ShouldReturnEmptyPassword()
    {
        var setting = EmailIntegrationSetting.Create(
            "smtp.example.test", 587, "user", "enc-quebrado",
            "noreply@example.test", null, useSsl: true);
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        var env = new Mock<IEmailEnvOptionsAccessor>();
        var protector = new Mock<ICredentialProtector>();
        protector.Setup(p => p.Unprotect("enc-quebrado"))
            .Throws(new InvalidOperationException("bad key"));

        var resolver = new EmailOptionsResolver(
            repository.Object, env.Object, protector.Object,
            NullLogger<EmailOptionsResolver>.Instance);
        var options = await resolver.ResolveAsync(CancellationToken.None);

        options.Password.Should().BeEmpty();
    }

    [Fact]
    public void EnvAccessor_ShouldMapEmailChannelOptions()
    {
        var options = Options.Create(new NotificationChannelsOptions
        {
            Email = new EmailChannelOptions
            {
                Host = "smtp-env.example.test",
                Port = 2525,
                Username = "u",
                Password = "p",
                From = "from@example.test",
                FromName = "Env",
                UseSsl = false
            }
        });

        var accessor = new EmailEnvOptionsAccessor(options);
        var current = accessor.Current;

        current.Host.Should().Be("smtp-env.example.test");
        current.Port.Should().Be(2525);
        current.Username.Should().Be("u");
        current.Password.Should().Be("p");
        current.From.Should().Be("from@example.test");
        current.FromName.Should().Be("Env");
        current.UseSsl.Should().BeFalse();
        current.IsSmtpConfigured.Should().BeTrue();
    }
}
