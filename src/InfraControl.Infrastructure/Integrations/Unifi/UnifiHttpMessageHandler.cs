using System.Net.Security;

namespace InfraControl.Infrastructure.Integrations.Unifi;

/// <summary>
/// Fase 1 (F26-T001) da integração UniFi: handler primário do named client
/// <c>unifi</c>. O controlador local de teste usa certificado self-signed
/// (docs/35 §2) — a validação TLS só é contornada quando o administrador
/// liga <c>Integrations:Unifi:AllowInvalidTlsCertificate</c>; default =
/// validação estrita do runtime (.NET rejeita certificado autoassinado).
/// </summary>
public static class UnifiHttpMessageHandler
{
    public static SocketsHttpHandler CreatePrimaryHandler(bool allowInvalidTlsCertificate)
    {
        var handler = new SocketsHttpHandler();

        if (allowInvalidTlsCertificate)
            handler.SslOptions.RemoteCertificateValidationCallback =
                static (_, _, _, _) => true;

        return handler;
    }
}
