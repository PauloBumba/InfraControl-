using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Integrations.WhatsApp;

public sealed class WhatsAppIntegrationSettingTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInitializeCorrectly()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080",
            "encrypted-api-key",
            "Admin",
            "120363000@g.us",
            "5511999999999");

        setting.BaseUrl.Should().Be("http://evolution:8080");
        setting.ApiKeyEncrypted.Should().Be("encrypted-api-key");
        setting.InstanceId.Should().Be("Admin");
        setting.GroupId.Should().Be("120363000@g.us");
        setting.Phone.Should().Be("5511999999999");
    }

    [Fact]
    public void Create_WithInvalidUrl_ShouldThrowArgumentException()
    {
        var act = () => WhatsAppIntegrationSetting.Create(
            "invalid-url",
            "key",
            "Admin");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*absoluta*");
    }

    [Fact]
    public void Create_WithEmptyInstanceId_ShouldThrowArgumentException()
    {
        var act = () => WhatsAppIntegrationSetting.Create(
            "http://evolution:8080",
            "key",
            "   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*instância*");
    }

    [Fact]
    public void SetGroupId_WithoutGUsSuffix_ShouldThrowArgumentException()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080",
            "key",
            "Admin");

        var act = () => setting.SetGroupId("123456789");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*@g.us*");
    }

    [Fact]
    public void SetGroupId_WithNull_ShouldClearGroupId()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080",
            "key",
            "Admin",
            "123@g.us");

        setting.SetGroupId(null);

        setting.GroupId.Should().BeNull();
    }

    [Fact]
    public void RecordConnectionState_ShouldUpdateState()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080",
            "key",
            "Admin");

        setting.RecordConnectionState("open");
        setting.ConnectionState.Should().Be("open");

        setting.RecordConnectionState(null);
        setting.ConnectionState.Should().BeNull();
    }

    [Fact]
    public void RecordTestResult_WhenOk_ShouldClearError()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080",
            "key",
            "Admin");

        var now = DateTime.UtcNow;
        setting.RecordTestResult(false, now, "erro temporário");
        setting.LastTestOk.Should().BeFalse();
        setting.LastTestError.Should().Be("erro temporário");
        setting.LastTestedAtUtc.Should().Be(now);

        var next = now.AddMinutes(1);
        setting.RecordTestResult(true, next, null);
        setting.LastTestOk.Should().BeTrue();
        setting.LastTestError.Should().BeNull();
        setting.LastTestedAtUtc.Should().Be(next);
    }

    // ── ADR-069: modelo do cartão visual ─────────────────────────────

    [Fact]
    public void UpdateCardSettings_WithValidValues_ShouldPersist()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080", "key", "Admin");

        setting.UpdateCardSettings(
            incidentEnabled: false, warningEnabled: true,
            resolutionEnabled: true, windowHours: 6,
            incidentColor: "#ABC", warningColor: "#a1b2c3",
            resolutionColor: null);

        setting.CardIncidentEnabled.Should().BeFalse();
        setting.CardWarningEnabled.Should().BeTrue();
        setting.CardResolutionEnabled.Should().BeTrue();
        setting.CardWindowHours.Should().Be(6);
        setting.CardIncidentColor.Should().Be("#ABC");
        setting.CardWarningColor.Should().Be("#a1b2c3");
        setting.CardResolutionColor.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(-1)]
    public void UpdateCardSettings_WindowOutOfRange_ShouldThrow(int windowHours)
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080", "key", "Admin");

        var act = () => setting.UpdateCardSettings(
            true, true, true, windowHours, null, null, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("123456")]
    public void UpdateCardSettings_InvalidColor_ShouldThrow(string color)
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080", "key", "Admin");

        var act = () => setting.UpdateCardSettings(
            true, true, true, 2, color, null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateCardSettings_EmptyColor_ShouldClearToSemanticPalette()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080", "key", "Admin");
        setting.UpdateCardSettings(
            true, true, true, 2, "#ff0000", null, null);

        setting.UpdateCardSettings(
            true, true, true, 2, "", null, null);

        setting.CardIncidentColor.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldDefaultCardSettingsToF33Behavior()
    {
        var setting = WhatsAppIntegrationSetting.Create(
            "http://evolution:8080", "key", "Admin");

        setting.CardIncidentEnabled.Should().BeTrue();
        setting.CardWarningEnabled.Should().BeTrue();
        setting.CardResolutionEnabled.Should().BeTrue();
        setting.CardWindowHours.Should().Be(2);
        setting.CardIncidentColor.Should().BeNull();
    }
}
