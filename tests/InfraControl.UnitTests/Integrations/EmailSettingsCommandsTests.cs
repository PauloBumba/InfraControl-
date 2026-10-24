using FluentAssertions;
using InfraControl.Application.Integrations.Email;
using InfraControl.Application.Integrations.Email.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F41-T001: configuração administrativa do SMTP — senha write-only e
/// cifrada (omitação mantém a gravada), fallback banco → env e resultado
/// do teste de envio persistido sem quebrar por falha de rede.
/// </summary>
public sealed class EmailSettingsCommandsTests
{
    [Fact]
    public void Create_WithValidFields_ShouldConfigureSetting()
    {
        var setting = EmailIntegrationSetting.Create(
            "smtp.example.test", 587, "user", "enc-pw",
            "noreply@example.test", "InfraControl", useSsl: true);

        setting.Host.Should().Be("smtp.example.test");
        setting.Port.Should().Be(587);
        setting.Username.Should().Be("user");
        setting.PasswordEncrypted.Should().Be("enc-pw");
        setting.From.Should().Be("noreply@example.test");
        setting.FromName.Should().Be("InfraControl");
        setting.UseSsl.Should().BeTrue();
        setting.NotificationsEnabled.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyHost_ShouldThrow()
    {
        var act = () => EmailIntegrationSetting.Create(
            "  ", 587, "user", "enc-pw", "noreply@example.test", null, true);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*host SMTP*");
    }

    [Fact]
    public void Create_WithInvalidPort_ShouldThrow()
    {
        var act = () => EmailIntegrationSetting.Create(
            "smtp.example.test", 0, "user", "enc-pw", "noreply@example.test", null, true);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithoutFrom_ShouldThrow()
    {
        var act = () => EmailIntegrationSetting.Create(
            "smtp.example.test", 587, "user", "enc-pw", " ", null, true);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*remetente*");
    }

    [Fact]
    public void Update_WithEmptyPassword_ShouldKeepEncryptedPassword()
    {
        var setting = EmailIntegrationSetting.Create(
            "smtp.example.test", 587, "user", "enc-original",
            "noreply@example.test", null, useSsl: true);

        setting.Update(
            "smtp2.example.test", 465, "user2", passwordEncrypted: null,
            "from2@example.test", "New Name", useSsl: false,
            notificationsEnabled: true);

        setting.Host.Should().Be("smtp2.example.test");
        setting.Port.Should().Be(465);
        setting.PasswordEncrypted.Should().Be("enc-original");
        setting.NotificationsEnabled.Should().BeTrue();
    }

    [Fact]
    public void RecordTestResult_OnFailure_ShouldTruncateError()
    {
        var setting = EmailIntegrationSetting.Create(
            "smtp.example.test", 587, "user", "enc-pw",
            "noreply@example.test", null, useSsl: true);

        setting.RecordTestResult(
            ok: false, DateTime.UtcNow, new string('x', 2000));

        setting.LastTestOk.Should().BeFalse();
        setting.LastTestError.Should().HaveLength(EmailIntegrationSetting.MaxErrorLength);
    }

    [Fact]
    public void RecordTestResult_OnSuccess_ShouldClearError()
    {
        var setting = EmailIntegrationSetting.Create(
            "smtp.example.test", 587, "user", "enc-pw",
            "noreply@example.test", null, useSsl: true);
        setting.RecordTestResult(ok: false, DateTime.UtcNow, "falha anterior");

        setting.RecordTestResult(ok: true, DateTime.UtcNow, error: null);

        setting.LastTestOk.Should().BeTrue();
        setting.LastTestError.Should().BeNull();
    }

    [Fact]
    public async Task GetSettings_WithoutRow_ShouldFallBackToEnvironment()
    {
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailIntegrationSetting?)null);
        var accessor = new Mock<IEmailEnvOptionsAccessor>();
        accessor.SetupGet(a => a.Current).Returns(new EmailIntegrationOptions
        {
            Host = "smtp-env.example.test",
            Port = 465,
            Username = "env-user",
            Password = "env-senha",
            From = "env@example.test",
            UseSsl = true
        });

        var handler = new GetEmailSettingsQueryHandler(repository.Object, accessor.Object);
        var dto = await handler.Handle(new GetEmailSettingsQuery(), CancellationToken.None);

        dto.Source.Should().Be("environment");
        dto.Host.Should().Be("smtp-env.example.test");
        dto.Port.Should().Be(465);
        dto.PasswordSet.Should().BeTrue();
        dto.From.Should().Be("env@example.test");
        dto.LastTestedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task GetSettings_WithRow_ShouldReturnDatabaseSourceWithoutPassword()
    {
        var setting = EmailIntegrationSetting.Create(
            "smtp-banco.example.test", 587, "admin", "enc-abc",
            "noreply@example.test", "InfraControl", useSsl: true);
        setting.RecordTestResult(ok: true, DateTime.UtcNow, error: null);
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        var accessor = new Mock<IEmailEnvOptionsAccessor>();

        var handler = new GetEmailSettingsQueryHandler(repository.Object, accessor.Object);
        var dto = await handler.Handle(new GetEmailSettingsQuery(), CancellationToken.None);

        dto.Source.Should().Be("database");
        dto.PasswordSet.Should().BeTrue();
        dto.LastTestOk.Should().BeTrue();
        dto.LastTestError.Should().BeNull();
        dto.FromName.Should().Be("InfraControl");
    }

    [Fact]
    public async Task SaveSettings_WithPassword_ShouldEncryptAndPersist()
    {
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailIntegrationSetting?)null);
        repository.Setup(r => r.SaveAsync(It.IsAny<EmailIntegrationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var protector = new Mock<ICredentialProtector>();
        protector.Setup(p => p.Protect("plain-senha")).Returns("enc-senha");

        var handler = new SaveEmailSettingsCommandHandler(repository.Object, protector.Object);
        var dto = await handler.Handle(
            new SaveEmailSettingsCommand(new SaveEmailSettingsRequest(
                "smtp.exemplo.test", 587, "Admin", "plain-senha",
                "noreply@example.test", "InfraControl", true, null)),
            CancellationToken.None);

        dto.Source.Should().Be("database");
        dto.PasswordSet.Should().BeTrue();
        protector.Verify(p => p.Protect("plain-senha"), Times.Once);
        repository.Verify(
            r => r.SaveAsync(
                It.Is<EmailIntegrationSetting>(s =>
                    s.PasswordEncrypted == "enc-senha" &&
                    s.Host == "smtp.exemplo.test"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveSettings_WithoutPassword_OnFirstConfig_ShouldThrow()
    {
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailIntegrationSetting?)null);
        var protector = new Mock<ICredentialProtector>();

        var handler = new SaveEmailSettingsCommandHandler(repository.Object, protector.Object);
        var act = async () => await handler.Handle(
            new SaveEmailSettingsCommand(new SaveEmailSettingsRequest(
                "smtp.exemplo.test", 587, "Admin", null,
                "noreply@example.test", null, true, null)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*senha SMTP*");
    }

    [Fact]
    public async Task SaveSettings_OmittingPassword_ShouldKeepExisting()
    {
        var existing = EmailIntegrationSetting.Create(
            "smtp.old.test", 587, "old", "enc-keep",
            "old@example.test", null, useSsl: true);
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository.Setup(r => r.SaveAsync(It.IsAny<EmailIntegrationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var protector = new Mock<ICredentialProtector>();

        var handler = new SaveEmailSettingsCommandHandler(repository.Object, protector.Object);
        var dto = await handler.Handle(
            new SaveEmailSettingsCommand(new SaveEmailSettingsRequest(
                "smtp.new.test", 465, "new", Password: null,
                "new@example.test", null, false, true)),
            CancellationToken.None);

        dto.Host.Should().Be("smtp.new.test");
        protector.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
        existing.PasswordEncrypted.Should().Be("enc-keep");
        existing.NotificationsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Test_WithoutConfiguration_ShouldReturnIncompleteError()
    {
        var resolver = new Mock<IEmailOptionsResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailIntegrationOptions());
        var repository = new Mock<IEmailSettingsRepository>();
        var client = new Mock<IEmailTestClient>();

        var handler = new SendEmailTestCommandHandler(
            resolver.Object, repository.Object, client.Object,
            NullLogger<SendEmailTestCommandHandler>.Instance);
        var result = await handler.Handle(
            new SendEmailTestCommand(), CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("Configuração incompleta");
        client.Verify(
            c => c.SendTestAsync(It.IsAny<EmailIntegrationOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Test_WithConfiguration_ShouldSendAndRecordSuccess()
    {
        var setting = EmailIntegrationSetting.Create(
            "smtp.example.test", 587, "user", "enc-pw",
            "noreply@example.test", null, useSsl: true);
        var resolver = new Mock<IEmailOptionsResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailIntegrationOptions
            {
                Host = "smtp.example.test",
                Port = 587,
                From = "noreply@example.test"
            });
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        repository.Setup(r => r.SaveAsync(It.IsAny<EmailIntegrationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var client = new Mock<IEmailTestClient>();
        client.Setup(c => c.SendTestAsync(It.IsAny<EmailIntegrationOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SendEmailTestCommandHandler(
            resolver.Object, repository.Object, client.Object,
            NullLogger<SendEmailTestCommandHandler>.Instance);
        var result = await handler.Handle(
            new SendEmailTestCommand(), CancellationToken.None);

        result.Ok.Should().BeTrue();
        setting.LastTestOk.Should().BeTrue();
        setting.LastTestedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_WhenClientThrows_ShouldReturnErrorAndRecordFailure()
    {
        var setting = EmailIntegrationSetting.Create(
            "smtp.example.test", 587, "user", "enc-pw",
            "noreply@example.test", null, useSsl: true);
        var resolver = new Mock<IEmailOptionsResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailIntegrationOptions
            {
                Host = "smtp.example.test",
                Port = 587,
                From = "noreply@example.test"
            });
        var repository = new Mock<IEmailSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
        repository.Setup(r => r.SaveAsync(It.IsAny<EmailIntegrationSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var client = new Mock<IEmailTestClient>();
        client.Setup(c => c.SendTestAsync(It.IsAny<EmailIntegrationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        var handler = new SendEmailTestCommandHandler(
            resolver.Object, repository.Object, client.Object,
            NullLogger<SendEmailTestCommandHandler>.Instance);
        var result = await handler.Handle(
            new SendEmailTestCommand(), CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().Be("Connection refused");
        setting.LastTestOk.Should().BeFalse();
        setting.LastTestError.Should().Be("Connection refused");
    }
}
