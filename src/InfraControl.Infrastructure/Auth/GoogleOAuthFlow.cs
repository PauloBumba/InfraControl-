using System.Security.Cryptography;

namespace InfraControl.Infrastructure.Auth;

/// <summary>
/// F43-T001/ADR-064 — authorization code flow com callback no backend:
/// start (redirect para o consent screen do Google) e state CSRF.
/// <list type="bullet">
/// <item><c>state</c>: 32 bytes aleatórios (critográficos) em base64url,
/// emitido em cookie HttpOnly e conferido no callback — login CSRF não
/// autorizado (RFC 6749 §10.12).</item>
/// <item>Cookie <c>SameSite=Lax</c> de propósito: o Google volta num
/// redirect top-level cross-site e <c>Strict</c> NÃO seria enviado —
/// <c>Lax</c> entrega cookies só em navegação GET de primeiro nível (que é
/// exatamente o callback).</item>
/// <item>O Client Secret jamais entra na URL — só na troca code → token
/// dentro do servidor.</item>
/// </list>
/// </summary>
public static class GoogleOAuthFlow
{
    public const string StateCookieName = ".InfraControl.GoogleState";

    /// <summary>
    /// F43-T003 (rev. 2026-10-24): código do motivo "sem conta ativa" no
    /// redirect <c>/login?error=google&amp;reason=account</c>. Só o CÓDIGO
    /// viaja na URL (texto livre na query seria injetável); a mensagem vem de
    /// <c>GET /auth/google/failure</c>, que a UI consome.
    /// </summary>
    public const string AccountUnavailableReason = "account";

    public const string AuthorizationEndpoint =
        "https://accounts.google.com/o/oauth2/v2/auth";

    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    public static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    /// <summary>state criptográfico (32 bytes) em base64url — sem '+'/'/'/'='.</summary>
    public static string CreateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// URL do consent screen (response_type=code, scope openid/email/
    /// profile). Montada à mão para não depender de lib de OAuth e para o
    /// escopo/params ficarem explícitos e testáveis.
    /// </summary>
    public static string CreateAuthorizationUrl(GoogleAuthOptions options, string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        return AuthorizationEndpoint
            + "?client_id=" + Uri.EscapeDataString(options.ClientId)
            + "&redirect_uri=" + Uri.EscapeDataString(options.RedirectUri)
            + "&response_type=code"
            + "&scope=" + Uri.EscapeDataString("openid email profile")
            + "&state=" + Uri.EscapeDataString(state);
    }
}
