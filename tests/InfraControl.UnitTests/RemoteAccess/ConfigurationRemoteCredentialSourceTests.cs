using FluentAssertions;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.Infrastructure.RemoteAccess;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T002: fonte provisória de credencial SSH lê RemoteAccess:Ssh via
/// IOptions (F11-T006 substitui por Credential Store). Sem credencial
/// configurada, o acesso falha com mensagem pt-BR — nunca com credencial vazia.
/// </summary>
public sealed class ConfigurationRemoteCredentialSourceTests
{
    [Fact]
    public void GetCredential_WhenConfigured_ShouldReturnConfiguredValues()
    {
        var source = new ConfigurationRemoteCredentialSource(
            Options.Create(new RemoteSshOptions
            {
                Username = "it-ssh",
                Password = "S3nh@Configurada!7"
            }));

        var credential = source.GetCredentialAsync(Guid.NewGuid()).GetAwaiter().GetResult();

        credential.Username.Should().Be("it-ssh");
        credential.Password.Should().Be("S3nh@Configurada!7");
    }

    [Fact]
    public async Task GetCredential_WhenNotConfigured_ShouldThrowInvalidOperationExceptionPtBr()
    {
        var source = new ConfigurationRemoteCredentialSource(
            Options.Create(new RemoteSshOptions()));

        var action = () => source.GetCredentialAsync(Guid.NewGuid());

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*não configurada*");
    }

    [Fact]
    public async Task GetCredential_WhenOnlyUsernameSet_ShouldThrowInvalidOperationExceptionPtBr()
    {
        var source = new ConfigurationRemoteCredentialSource(
            Options.Create(new RemoteSshOptions { Username = "it-ssh" }));

        var action = () => source.GetCredentialAsync(Guid.NewGuid());

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*não configurada*");
    }
}
