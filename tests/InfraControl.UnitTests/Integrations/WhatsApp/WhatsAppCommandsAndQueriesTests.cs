using FluentAssertions;
using InfraControl.Application.Integrations.WhatsApp;
using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Integrations.WhatsApp;

public sealed class WhatsAppCommandsAndQueriesTests
{
    private readonly Mock<IWhatsAppSettingsRepository> _repository = new();
    private readonly Mock<IWhatsAppEnvOptionsAccessor> _envAccessor = new();
    private readonly Mock<IWhatsAppOptionsResolver> _optionsResolver = new();
    private readonly Mock<IEvolutionInstanceClient> _client = new();
    private readonly Mock<ICredentialProtector> _protector = new();

    [Fact]
    public async Task GetWhatsAppSettings_WhenDbHasRecord_ShouldReturnDatabaseSource()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://db-url:8080",
            "encrypted-key",
            "Admin",
            "123@g.us",
            "5511999999999");
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);

        var handler = new GetWhatsAppSettingsQueryHandler(_repository.Object, _envAccessor.Object);
        var result = await handler.Handle(new GetWhatsAppSettingsQuery(), CancellationToken.None);

        result.Source.Should().Be("database");
        result.BaseUrl.Should().Be("http://db-url:8080");
        result.ApiKeySet.Should().BeTrue();
        result.GroupId.Should().Be("123@g.us");
    }

    [Fact]
    public async Task GetWhatsAppSettings_WhenDbEmpty_ShouldReturnEnvironmentSource()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppIntegrationSetting?)null);
        _envAccessor.Setup(e => e.Current).Returns(new WhatsAppChannelOptions
        {
            BaseUrl = "http://env-url:8080",
            ApiKey = "env-key",
            InstanceId = "EnvAdmin",
            GroupId = "env@g.us"
        });

        var handler = new GetWhatsAppSettingsQueryHandler(_repository.Object, _envAccessor.Object);
        var result = await handler.Handle(new GetWhatsAppSettingsQuery(), CancellationToken.None);

        result.Source.Should().Be("environment");
        result.BaseUrl.Should().Be("http://env-url:8080");
        result.ApiKeySet.Should().BeTrue();
    }

    [Fact]
    public async Task SaveWhatsAppSettings_FirstTimeWithoutApiKey_ShouldThrowArgumentException()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppIntegrationSetting?)null);

        var handler = new SaveWhatsAppSettingsCommandHandler(_repository.Object, _protector.Object);
        var cmd = new SaveWhatsAppSettingsCommand(new SaveWhatsAppSettingsRequest(
            "http://url:8080", "Admin", ApiKey: null, GroupId: null, Phone: null));

        var act = () => handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*obrigatória*");
    }

    [Fact]
    public async Task SaveWhatsAppSettings_WithApiKey_ShouldProtectAndSave()
    {
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppIntegrationSetting?)null);
        _protector.Setup(p => p.Protect("plain-key")).Returns("encrypted-key");

        var handler = new SaveWhatsAppSettingsCommandHandler(_repository.Object, _protector.Object);
        var cmd = new SaveWhatsAppSettingsCommand(new SaveWhatsAppSettingsRequest(
            "http://url:8080", "Admin", ApiKey: "plain-key", GroupId: "group@g.us", Phone: null));

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.BaseUrl.Should().Be("http://url:8080");
        result.ApiKeySet.Should().BeTrue();
        _repository.Verify(r => r.SaveAsync(
            It.Is<WhatsAppIntegrationSetting>(s => s.ApiKeyEncrypted == "encrypted-key"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendWhatsAppTest_WhenNoDestinationConfigured_ShouldReturnFailureWithoutCallingClient()
    {
        _optionsResolver.Setup(o => o.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppChannelOptions { BaseUrl = "http://url", ApiKey = "k", InstanceId = "i" });

        var handler = new SendWhatsAppTestCommandHandler(
            _optionsResolver.Object, _repository.Object, _client.Object,
            NullLogger<SendWhatsAppTestCommandHandler>.Instance);

        var result = await handler.Handle(new SendWhatsAppTestCommand(), CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("Sem destino");
        _client.Verify(c => c.SendTextAsync(
            It.IsAny<WhatsAppChannelOptions>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendWhatsAppTest_WhenSendSucceeds_ShouldReturnOkAndRecordSuccess()
    {
        var setting = WhatsAppIntegrationSetting.Create("http://url:8080", "key", "Admin", "grp@g.us");
        _optionsResolver.Setup(o => o.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppChannelOptions { BaseUrl = "http://url", ApiKey = "k", InstanceId = "Admin", GroupId = "grp@g.us" });
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);

        var handler = new SendWhatsAppTestCommandHandler(
            _optionsResolver.Object, _repository.Object, _client.Object,
            NullLogger<SendWhatsAppTestCommandHandler>.Instance);

        var result = await handler.Handle(new SendWhatsAppTestCommand(), CancellationToken.None);

        result.Ok.Should().BeTrue();
        setting.LastTestOk.Should().BeTrue();
        setting.LastTestError.Should().BeNull();
        _repository.Verify(r => r.SaveAsync(setting, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateWhatsAppQr_ShouldDecodePngAndReturnDto()
    {
        _optionsResolver.Setup(o => o.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppChannelOptions { BaseUrl = "http://url", ApiKey = "k", InstanceId = "Admin" });
        _client.Setup(c => c.ConnectAsync(It.IsAny<WhatsAppChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvolutionQrResult("data:image/png;base64,QUJD", "1234"));
        _client.Setup(c => c.GetConnectionStateAsync(It.IsAny<WhatsAppChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("connecting");

        var handler = new GenerateWhatsAppQrCommandHandler(
            _optionsResolver.Object, _repository.Object, _client.Object,
            NullLogger<GenerateWhatsAppQrCommandHandler>.Instance);

        var result = await handler.Handle(new GenerateWhatsAppQrCommand(), CancellationToken.None);

        result.PairingCode.Should().Be("1234");
        result.State.Should().Be("connecting");
        result.Png.Should().NotBeNull();
        result.Png.Should().Equal(Convert.FromBase64String("QUJD"));
    }

    [Fact]
    public async Task ListWhatsAppGroups_ShouldOrderGroupsAlphabetically()
    {
        _optionsResolver.Setup(o => o.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppChannelOptions { BaseUrl = "http://url", ApiKey = "k", InstanceId = "Admin" });
        _client.Setup(c => c.ListGroupsAsync(It.IsAny<WhatsAppChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EvolutionGroupDto>
            {
                new("2@g.us", "Zebra", 5, null, null, false, false),
                new("1@g.us", "Alpha", 10, null, null, false, false)
            });

        var handler = new ListWhatsAppGroupsQueryHandler(_optionsResolver.Object, _client.Object);
        var result = await handler.Handle(new ListWhatsAppGroupsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Subject.Should().Be("Alpha");
        result[1].Subject.Should().Be("Zebra");
    }
}
