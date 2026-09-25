using FluentAssertions;
using InfraControl.Infrastructure.RemoteAccess;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T006: cifra AES-256-GCM da credencial — chave-símétrica vem só de
/// env/config (RemoteAccess:CredentialEncryption:Key); nunca em texto puro
/// no banco.
/// </summary>
public sealed class CredentialProtectorTests
{
    private const string ValidKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY="; // 32 bytes em base64

    private static AesGcmCredentialProtector CreateProtector(string key = ValidKey)
        => new(Options.Create(new CredentialEncryptionOptions { Key = key }));

    [Fact]
    public void ProtectUnprotect_ShouldRoundTrip()
    {
        var protector = CreateProtector();

        var protectedValue = protector.Protect("S3nh@Real!99");
        protectedValue.Should().NotBeNullOrWhiteSpace();
        protectedValue.Should().NotContain("S3nh@Real!99", "o texto puro nunca fica gravado");
        protectedValue.Should().StartWith("v1:", "prefixo marca a versão do formato");

        protector.Unprotect(protectedValue).Should().Be("S3nh@Real!99");
    }

    [Fact]
    public void Protect_MissingKey_ShouldThrowPtBr()
    {
        var protector = CreateProtector(string.Empty);

        var action = () => protector.Protect("qualquer");

        action.Should().Throw<InvalidOperationException>().WithMessage("*CredentialEncryption:Key*");
    }

    [Fact]
    public void Protect_KeyWithWrongLength_ShouldThrowPtBr()
    {
        var protector = CreateProtector(Convert.ToBase64String(new byte[16]));

        var action = () => protector.Protect("qualquer");

        action.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void Unprotect_TamperedValue_ShouldThrowPtBrWithoutLeakingDetails()
    {
        var protector = CreateProtector();
        var protectedValue = protector.Protect("S3nh@Real!99");
        var tampered = protectedValue[..^4] + "AAAA";

        var action = () => protector.Unprotect(tampered);

        action.Should().Throw<InvalidOperationException>().WithMessage("*decifrar*");
    }

    [Fact]
    public void Unprotect_InvalidFormat_ShouldThrowPtBr()
    {
        var protector = CreateProtector();

        var action = () => protector.Unprotect("valor-invalido");

        action.Should().Throw<InvalidOperationException>().WithMessage("*decifrar*");
    }
}
