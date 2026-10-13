using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// Fase 06d: UserSettings — defaults, validação de timezone e update.
/// </summary>
public sealed class UserSettingsTests
{
    [Fact]
    public void CreateDefault_ShouldPersistUserIdAndDefaults()
    {
        var userId = Guid.NewGuid();

        var settings = UserSettings.CreateDefault(userId);

        settings.UserId.Should().Be(userId);
        settings.Timezone.Should().Be("America/Sao_Paulo");
        settings.DarkMode.Should().BeTrue();
        settings.AutoRefresh.Should().BeTrue();
        settings.Telemetry.Should().BeFalse();
    }

    [Fact]
    public void CreateDefault_EmptyUserId_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => UserSettings.CreateDefault(Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do usuário é obrigatório.*");
    }

    [Fact]
    public void Update_ShouldPersistAllFieldsAndTouch()
    {
        var settings = UserSettings.CreateDefault(Guid.NewGuid());
        var before = settings.UpdateDateTime;

        settings.Update("America/New_York", darkMode: false, autoRefresh: false, telemetry: true);

        settings.Timezone.Should().Be("America/New_York");
        settings.DarkMode.Should().BeFalse();
        settings.AutoRefresh.Should().BeFalse();
        settings.Telemetry.Should().BeTrue();
        settings.UpdateDateTime.Should().BeAfter(before);
    }

    [Fact]
    public void Update_ShouldTrimTimezone()
    {
        var settings = UserSettings.CreateDefault(Guid.NewGuid());

        settings.Update("  America/Sao_Paulo  ", true, true, false);

        settings.Timezone.Should().Be("America/Sao_Paulo");
    }

    [Fact]
    public void Update_EmptyTimezone_ShouldThrowArgumentExceptionPtBr()
    {
        var settings = UserSettings.CreateDefault(Guid.NewGuid());

        var act = () => settings.Update("   ", true, true, false);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O fuso horário é obrigatório.*");
    }

    [Fact]
    public void Update_TimezoneTooLong_ShouldThrowArgumentExceptionPtBr()
    {
        var settings = UserSettings.CreateDefault(Guid.NewGuid());
        var longTimezone = new string('x', UserSettings.TimezoneMaxLength + 1);

        var act = () => settings.Update(longTimezone, true, true, false);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"O fuso horário deve ter no máximo {UserSettings.TimezoneMaxLength} caracteres.*");
    }
}
