using System.Net.Security;
using FluentAssertions;
using InfraControl.Infrastructure.Integrations.Unifi;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// Fase 1 (F26-T001) da integração UniFi: o controlador local de teste usa
/// certificado self-signed (docs/35 §2) — a validação TLS só pode ser
/// contornada quando o administrador explicita a opção; default = validação
/// estrita do .NET.
/// </summary>
public sealed class UnifiHttpMessageHandlerTests
{
    [Fact]
    public void CreatePrimaryHandler_WhenInvalidCertificateAllowed_ShouldBypassValidation()
    {
        using var handler = UnifiHttpMessageHandler.CreatePrimaryHandler(
            allowInvalidTlsCertificate: true);

        handler.SslOptions.RemoteCertificateValidationCallback.Should().NotBeNull(
            "a opção AllowInvalidTlsCertificate=true instala o callback de contorno");

        var accept = handler.SslOptions.RemoteCertificateValidationCallback!(
            null!, null!, null!, SslPolicyErrors.RemoteCertificateNameMismatch);

        accept.Should().BeTrue("com a opção ligada qualquer erro de certificado é aceito");
    }

    [Fact]
    public void CreatePrimaryHandler_WhenValidationEnabled_ShouldKeepDefaultValidation()
    {
        using var handler = UnifiHttpMessageHandler.CreatePrimaryHandler(
            allowInvalidTlsCertificate: false);

        handler.SslOptions.RemoteCertificateValidationCallback.Should().BeNull(
            "sem a opção a validação padrão do runtime permanece intacta");
    }
}
