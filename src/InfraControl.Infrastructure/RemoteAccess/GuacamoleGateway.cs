using System.Net.Http.Headers;
using System.Text.Json;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using Microsoft.Extensions.Configuration;

namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Header auth do gateway Guacamole (ADR-060 D2, F34-T007): o container da
/// API fala server-to-server com <c>{BaseUrl}/api/tokens</c> enviando
/// <c>X-Remote-User</c> — contrato validado ao vivo em 2026-10-18 (usuário
/// existente e inexistente → 200 authToken, dataSource "header"; a sessão
/// do navegador entra por <c>/guacamole/?token=…</c> sem senha no cliente).
/// Com BaseUrl vazio (profile remote fora do compose padrão) o erro é
/// explícito; resposta != 200 vira 409 pt-BR no handler.
/// </summary>
public sealed class GuacamoleGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
    : IGuacamoleGateway
{
    public const string HttpClientName = "guacamole";
    public const string BaseUrlKey = "RemoteAccess:Guacamole:BaseUrl";

    public async Task<string> CreateTokenAsync(
        string userName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuration[BaseUrlKey]))
            throw new InvalidOperationException(
                "Gateway de acesso remoto não configurado "
                + "(RemoteAccess:Guacamole:BaseUrl) — suba o profile 'remote'.");

        var client = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{client.BaseAddress!.AbsoluteUri.TrimEnd('/')}/api/tokens");
        request.Headers.Add("X-Remote-User", userName);
        // O endpoint exige content-type de formulário mesmo sem campos
        // (@FormParam → 500 sem isso — capturado no spike do F34-T007).
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>());

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Gateway de acesso remoto indisponível — verifique se o profile "
                + "'remote' está de pé (docker compose --profile remote up -d).",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Gateway de acesso remoto recusou a sessão "
                    + $"(HTTP {(int)response.StatusCode}).");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(
                stream, default, cancellationToken);
            if (!json.RootElement.TryGetProperty("authToken", out var tokenElement))
                throw new InvalidOperationException(
                    "Gateway de acesso remoto devolveu resposta sem token.");

            var token = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException(
                    "Gateway de acesso remoto devolveu token vazio.");

            return token;
        }
    }
}
