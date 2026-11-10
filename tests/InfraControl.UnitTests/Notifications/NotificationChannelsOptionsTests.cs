using FluentAssertions;
using InfraControl.Application.Notifications.Channels;
using Microsoft.Extensions.Configuration;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// D5 (docs/66): ConfigurationBinder ACRESCENTA em List já populada —
/// o inicializador ["Email","SignalR"] + env Default__0..2 duplicavam canais
/// ([Email,SignalR,Email,SignalR,WhatsApp]). NormalizeDefaultChannels deduplica
/// preservando a ordem e descarta vazios (NOTIFY_DEFAULT_2= em branco).
/// </summary>
public sealed class NotificationChannelsOptionsTests
{
    [Fact]
    public void Default_WithoutConfig_ShouldBeEmailAndSignalR()
    {
        new NotificationChannelsOptions().Default
            .Should().Equal("Email", "SignalR");
    }

    [Fact]
    public void Bind_WithFullEnvStyleConfig_ThenNormalize_ShouldNotDuplicate()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Channels:Default:0"] = "Email",
                ["Notifications:Channels:Default:1"] = "SignalR",
                ["Notifications:Channels:Default:2"] = "WhatsApp"
            })
            .Build();

        var options = new NotificationChannelsOptions();
        config.GetSection(NotificationChannelsOptions.SectionName).Bind(options);
        // sem normalize, o binder já duplicou (comportamento do bug):
        options.Default.Should().HaveCountGreaterThan(3,
            "pré-condição: binder append no inicializador");

        options.NormalizeDefaultChannels();

        options.Default.Should().Equal("Email", "SignalR", "WhatsApp");
    }

    [Fact]
    public void Bind_WithEmptyTrailingEnv_ShouldDropEmptyAndKeepBase()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Channels:Default:0"] = "Email",
                ["Notifications:Channels:Default:1"] = "SignalR",
                ["Notifications:Channels:Default:2"] = ""
            })
            .Build();

        var options = new NotificationChannelsOptions();
        config.GetSection(NotificationChannelsOptions.SectionName).Bind(options);
        options.NormalizeDefaultChannels();

        options.Default.Should().Equal("Email", "SignalR");
    }

    [Fact]
    public void Normalize_ShouldBeIdempotent()
    {
        var options = new NotificationChannelsOptions
        {
            Default = ["Email", "email", " SignalR ", "WhatsApp", ""]
        };

        options.NormalizeDefaultChannels();
        options.NormalizeDefaultChannels();

        options.Default.Should().Equal("Email", "SignalR", "WhatsApp");
    }
}
