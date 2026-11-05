using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Auth;

/// <summary>
/// F43-T002/ADR-064 — troca do <c>authorization code</c> pelo
/// <c>id_token</c> <b>dentro do servidor</b>: o browser nunca vê o Client
/// Secret nem o code de novo (o code já foi consumido no redirect).
/// </summary>
public interface IGoogleTokenClient
{
    /// <returns>
    /// o <c>id_token</c> (JWT) trocado, ou <c>null</c> quando o Google
    /// recusa a troca / responde algo inesperado. O code e a resposta
    /// completa (access_token etc.) jamais são logados.
    /// </returns>
    Task<string?> ExchangeCodeForIdTokenAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// POST <c>application/x-www-form-urlencoded</c> no
/// <see cref="GoogleOAuthFlow.TokenEndpoint"/> (RFC 6749 §4.1.3). Só o
/// <c>id_token</c> sai daqui — corpo da resposta contém tokens demais para
/// logar qualquer coisa além do status.
/// </summary>
public sealed class GoogleTokenClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleAuthOptions> googleAuthOptions,
    ILogger<GoogleTokenClient> logger) : IGoogleTokenClient
{
    public const string HttpClientName = "google-oauth";

    public async Task<string?> ExchangeCodeForIdTokenAsync(
        string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var options = googleAuthOptions.Value;

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsync(
                GoogleOAuthFlow.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                    ["redirect_uri"] = options.RedirectUri,
                    ["grant_type"] = "authorization_code"
                }),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Só o status: o corpo de erro do Google (e de sucesso) não
                // pode ir para log — ele ecoa fragmentos do exchange.
                logger.LogWarning(
                    "Google token endpoint recusou a troca de code (HTTP {StatusCode})",
                    (int)response.StatusCode);
                return null;
            }

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.TryGetProperty("id_token", out var idToken)
                ? idToken.GetString()
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException
                                   && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao trocar authorization code no Google");
            return null;
        }
    }
}
