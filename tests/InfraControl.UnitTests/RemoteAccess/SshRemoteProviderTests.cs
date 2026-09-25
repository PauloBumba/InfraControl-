using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.Infrastructure.RemoteAccess;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T002: SshRemoteProvider (SSH.NET) abre conexão real sem terminal
/// interativo. Aqui só o caminho sem rede — porta TCP fechada em loopback
/// deve virar RemoteConnectResult falho com mensagem pt-BR, nunca exceção
/// vazando nem credencial exposta.
/// </summary>
public sealed class SshRemoteProviderTests
{
    private static SshRemoteProvider CreateProvider(int port, string password = "S3nh@Provisória!9")
        => new(Options.Create(new RemoteSshOptions
        {
            Username = "it-ssh",
            Password = password,
            Port = port,
            TimeoutSeconds = 3
        }));

    private static int GetClosedLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Theory]
    [InlineData("ssh")]
    [InlineData("Ssh")]
    [InlineData("SSH")]
    public void CanHandle_SshKeys_ShouldReturnTrue(string providerKey)
        => CreateProvider(22).CanHandle(providerKey).Should().BeTrue();

    [Theory]
    [InlineData("rdp")]
    [InlineData("ping")]
    [InlineData("")]
    public void CanHandle_OtherKeys_ShouldReturnFalse(string providerKey)
        => CreateProvider(22).CanHandle(providerKey).Should().BeFalse();

    [Fact]
    public void Name_ShouldBeSsh()
        => CreateProvider(22).Name.Should().Be("SSH");

    [Fact]
    public async Task ConnectAsync_ClosedPort_ShouldReturnFailureResultWithoutThrowing()
    {
        var port = GetClosedLoopbackPort();
        var provider = CreateProvider(port);

        var result = await provider.ConnectAsync(
            "127.0.0.1", new RemoteCredential("it-ssh", "S3nh@Provisória!9"));

        result.Succeeded.Should().BeFalse();
        result.Connection.Should().BeNull();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.Error.Should().Contain("SSH", "a mensagem deve contextualizar em pt-BR");
        result.Error.Should().NotContain("S3nh@Provisória!9",
            "a credencial nunca pode aparecer no erro");
    }
}
