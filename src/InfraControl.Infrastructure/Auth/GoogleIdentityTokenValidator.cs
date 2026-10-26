using Google.Apis.Auth;
using InfraControl.Application.Identity.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Auth;

/// <summary>
/// F1-T014 (GSI id_token) + F43/ADR-064 (authorization code com callback no
/// backend). O Client ID é público por natureza (identificador da
/// aplicação, não segredo). O Client Secret (F43) existe SOMENTE no
/// servidor — usado na troca <c>code → token</c> no callback; nunca chega
/// ao browser nem à URL de autorização.
/// </summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "Authentication:Google";

    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// F43-T001/ADR-064: segredo do OAuth Client (tipo Web application),
    /// usado só pelo backend na troca code → token. Nunca em log/URL.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// F43-T001: redirect URI CADASTRADA no Console do Google
    /// (ex.: https://host/api/v1/auth/google/callback). O Google só devolve
    /// o code para URIs batendo exatamente com o cadastro.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Domínios Google Workspace permitidos (ex.: "empresa.com.br"). Vazio = sem restrição.</summary>
    public string[] AllowedDomains { get; set; } = [];

    /// <summary>
    /// Fluxo GSI (POST /auth/google com id_token): só o ClientId basta —
    /// é a audiência do token validado por JWKS. Endpoint mantido como
    /// fallback/rollback do fluxo code (F43).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>
    /// F43-T001: code flow completo — exige também o secret (troca no
    /// servidor) e a redirect URI registrada no Console. Sem os três,
    /// GET /auth/google responde 503 (start desligado).
    /// </summary>
    public bool IsCodeFlowConfigured =>
        IsConfigured
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);

    /// <summary>
    /// Emissores aceitos para o <c>iss</c> do id_token. A biblioteca do Google
    /// já valida o emissor, mas o backend NÃO delega essa decisão a um
    /// detalhe de biblioteca: a allow-list é conferida aqui de forma
    /// explícita (assim um token assinado por chave válida porém com
    /// <c>iss</c> trocado é rejeitado mesmo que a biblioteca evolua).
    /// </summary>
    public bool IsIssuerAllowed(string? issuer) =>
        string.Equals(issuer, "accounts.google.com", StringComparison.Ordinal)
        || string.Equals(issuer, "https://accounts.google.com", StringComparison.Ordinal);

    /// <summary>
    /// Allow-list de domínios Google Workspace. Entradas vazias são ignoradas
    /// (uma variável de ambiente sobrando como `AllowedDomains__0=` não pode
    /// trancar o login de todo mundo). Lista vazia = sem restrição.
    /// </summary>
    public bool IsDomainAllowed(string email)
    {
        var allowedDomains = AllowedDomains
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(domain => domain.Trim().TrimStart('@'))
            .ToArray();

        if (allowedDomains.Length == 0) return true;

        var separator = email.IndexOf('@');
        if (separator < 0 || separator == email.Length - 1) return false;

        var domain = email[(separator + 1)..];
        return allowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Valida o id_token emitido pelo Google Identity Services.
///
/// Regra de segurança (docs/security, docs/05): o NOME e o E-MAIL que chegam do
/// browser NUNCA são confiáveis. Só o que passa aqui é usado: assinatura
/// conferida contra as chaves públicas do Google (JWKS), audiência igual ao
/// Client Id da aplicação, expiração e `email_verified`. O emissor é
/// verificado pela própria biblioteca contra as issuers do Google
/// (`accounts.google.com`). O id_token em si nunca é logado.
/// </summary>
public sealed class GoogleIdentityTokenValidator(
    IOptions<GoogleAuthOptions> options,
    ILogger<GoogleIdentityTokenValidator> logger) : IGoogleIdentityTokenValidator
{
    private readonly GoogleAuthOptions _options = options.Value;

    public async Task<GoogleTokenValidation> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            logger.LogInformation(
                "Google login is not configured ({Section}); the social login endpoint stays disabled.",
                GoogleAuthOptions.SectionName);
            return GoogleTokenValidation.NotConfigured();
        }

        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 8192)
        {
            logger.LogWarning("Google login rejected: the credential is empty or oversized.");
            return GoogleTokenValidation.Invalid();
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId],
                    HostedDomain = null,
                    IssuedAtClockTolerance = TimeSpan.FromMinutes(1),
                    ExpirationTimeClockTolerance = TimeSpan.FromMinutes(1)
                });

            var email = payload.Email?.Trim();
            var subject = payload.Subject;

            if (!_options.IsIssuerAllowed(payload.Issuer))
            {
                logger.LogWarning("Google login rejected: the token issuer is not a Google issuer.");
                return GoogleTokenValidation.Invalid();
            }

            if (payload.EmailVerified != true
                || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(subject))
            {
                logger.LogWarning(
                    "Google login rejected: the token has no verified e-mail (emailVerified={EmailVerified}).",
                    payload.EmailVerified);
                return GoogleTokenValidation.Invalid();
            }

            if (!_options.IsDomainAllowed(email))
            {
                logger.LogWarning("Google login rejected: e-mail domain is not in the allow list.");
                return GoogleTokenValidation.Invalid();
            }

            return GoogleTokenValidation.Valid(new GoogleIdentity(
                subject,
                email.ToLowerInvariant(),
                string.IsNullOrWhiteSpace(payload.Name) ? email : payload.Name.Trim()));
        }
        catch (Exception exception) when (exception is InvalidJwtException or ArgumentException)
        {
            // Mensagem genérica e SEM o token/assinatura: o log serve para
            // diagnóstico, não vira canal de vazamento.
            logger.LogWarning(exception, "Google login rejected: the credential could not be validated.");
            return GoogleTokenValidation.Invalid();
        }
    }
}
