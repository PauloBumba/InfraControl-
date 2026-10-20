using System.Text;
using System.Text.Json;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.Domain.RemoteAccess;
using Microsoft.Extensions.Configuration;

namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Header auth + ciclo de vida da conexão no gateway Guacamole (ADR-060 D2/D4,
/// F34-T007/T008): o container da API fala server-to-server com
/// <c>{BaseUrl}/api</c> — token via <c>X-Remote-User</c> (contrato validado ao
/// vivo em 2026-10-18) e conexão/usuário/permissão via REST de administração
/// (guacadmin + senha do gateway, só em config de servidor). A sessão do
/// navegador entra por <c>/guacamole/?token=…</c> sem senha no cliente; a
/// credencial do ALVO é gravada na conexão Guacamole (fonte única D4) e o
/// grant de leitura é idempotente. Com BaseUrl vazio (profile remote fora do
/// compose padrão) o erro é explícito; respostas != 2xx viram
/// <see cref="InvalidOperationException"/> com mensagem pt-BR.
/// </summary>
public sealed class GuacamoleGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
    : IGuacamoleGateway
{
    public const string HttpClientName = "guacamole";
    public const string BaseUrlKey = "RemoteAccess:Guacamole:BaseUrl";
    public const string AdminUserKey = "RemoteAccess:Guacamole:AdminUser";
    public const string AdminPasswordKey = "RemoteAccess:Guacamole:AdminPassword";
    public const string DataSourceKey = "RemoteAccess:Guacamole:DataSource";

    private const string DefaultAdminUser = "guacadmin";
    private const string DefaultDataSource = "postgresql";
    private const int SshPort = 22;
    private const int RdpPort = 3389;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<string> CreateTokenAsync(
        string userName, CancellationToken cancellationToken = default)
    {
        var apiBase = GetApiBase();
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{apiBase}/api/tokens");
        request.Headers.Add("X-Remote-User", userName);
        // O endpoint exige content-type de formulário mesmo sem campos
        // (@FormParam → 500 sem isso — capturado no spike do F34-T007).
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>());

        using var response = await SendSafeAsync(client, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Gateway de acesso remoto recusou a sessão "
                + $"(HTTP {(int)response.StatusCode}).");

        using var json = await ReadJsonAsync(response, cancellationToken);
        if (!json.RootElement.TryGetProperty("authToken", out var tokenElement))
            throw new InvalidOperationException(
                "Gateway de acesso remoto devolveu resposta sem token.");

        var token = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "Gateway de acesso remoto devolveu token vazio.");

        return token;
    }

    public async Task EnsureDeviceConnectionAsync(
        string deviceName,
        string hostname,
        RemoteProtocol protocol,
        RemoteCredential credential,
        string grantToUser,
        CancellationToken cancellationToken = default)
    {
        var apiBase = GetApiBase();
        var adminToken = await GetAdminTokenAsync(apiBase, cancellationToken);
        var dataSource = configuration[DataSourceKey] ?? DefaultDataSource;
        var protocolName = protocol == RemoteProtocol.Rdp ? "rdp" : "ssh";
        var port = protocol == RemoteProtocol.Rdp ? RdpPort : SshPort;

        var connectionsUrl =
            $"{apiBase}/api/session/data/{dataSource}/connections";

        // 1) localiza a conexão pelo NOME do device (chave de negócio — o
        //    identifier interno muda se recriarmos).
        using (var connections = await SendAsync(
            HttpMethod.Get, connectionsUrl, adminToken, null, cancellationToken))
        {
            string? connectionId = null;
            var isActive = true;
            foreach (var property in connections.RootElement.EnumerateObject())
            {
                if (property.Value.TryGetProperty("name", out var name)
                    && name.GetString() == deviceName)
                {
                    connectionId = property.Value.TryGetProperty("identifier", out var id)
                        ? id.GetString()
                        : property.Name;
                    isActive = property.Value.TryGetProperty(
                            "activeConnections", out var active)
                        && active.GetInt32() > 0;
                    break;
                }
            }

            // 2) divergência de IP/SO/credencial não é legível via REST (1.6
            //    não devolve parameters e não expõe PATCH de conexão): a
            //    atualização segura é recriar a conexão com os valores atuais
            //    — mas SÓ quando ociosa, para nunca derrubar túnel de quem
            //    está conectado (com sessão ativa, reaproveita o registro).
            if (connectionId is not null && !isActive)
            {
                using var deleted = await SendAsync(
                    HttpMethod.Delete, $"{connectionsUrl}/{connectionId}",
                    adminToken, null, cancellationToken);
                _ = deleted;
                connectionId = null;
            }

            // 3) cria se não existe — credencial do alvo gravada NA conexão
            //    (fonte única D4, injetada aqui server-to-server).
            if (connectionId is null)
            {
                var body = JsonSerializer.Serialize(new
                {
                    parentIdentifier = (string?)null,
                    protocol = protocolName,
                    name = deviceName,
                    proxyPort = 0,
                    proxyHostname = "localhost",
                    maxConnections = 0,
                    maxConnectionTime = 0,
                    maxIdleTime = 0,
                    failoverOnly = false,
                    parameters = new Dictionary<string, string>
                    {
                        ["hostname"] = hostname,
                        ["port"] = port.ToString(),
                        ["username"] = credential.Username,
                        ["password"] = credential.Password,
                    },
                    attributes = new Dictionary<string, string>(),
                }, JsonOptions);

                using var created = await SendAsync(
                    HttpMethod.Post, connectionsUrl, adminToken, body, cancellationToken);
                connectionId = created.RootElement.TryGetProperty(
                        "identifier", out var newId)
                    ? newId.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(connectionId))
                    throw new InvalidOperationException(
                        "Gateway de acesso remoto criou conexão sem identificador.");
            }

            // 4) o header auth não persiste usuário — garante o registro para
            //    que a permissão tenha dono (senha nunca é a do alvo, D4).
            var usersUrl = $"{apiBase}/api/session/data/{dataSource}/users";
            using (var users = await SendAsync(
                HttpMethod.Get, usersUrl, adminToken, null, cancellationToken))
            {
                if (!users.RootElement.TryGetProperty(grantToUser, out _))
                {
                    var userBody = JsonSerializer.Serialize(new
                    {
                        username = grantToUser,
                        password = Guid.NewGuid().ToString("N"),
                        attributes = new Dictionary<string, string>(),
                    }, JsonOptions);
                    using var createdUser = await SendAsync(
                        HttpMethod.Post, usersUrl, adminToken, userBody, cancellationToken);
                    _ = createdUser;
                }
            }

            // 5) grant de leitura — só quando falta (204 idempotente também
            //    seria aceito, mas evitamos chamada sem necessidade).
            var userPermissionsUrl = $"{usersUrl}/{grantToUser}/permissions";
            bool alreadyGranted;
            using (var permissions = await SendAsync(
                HttpMethod.Get, userPermissionsUrl, adminToken, null, cancellationToken))
            {
                alreadyGranted =
                    permissions.RootElement.TryGetProperty(
                        "connectionPermissions", out var connectionPermissions)
                    && connectionPermissions.TryGetProperty(connectionId, out var granted)
                    && granted.EnumerateArray().Any(
                        value => value.GetString() == "READ");
            }

            if (!alreadyGranted)
            {
                // Formato validado ao vivo (1.6.0): JSON Patch (RFC 6902) com
                // value=READ — /connectionPermissions/{id}/READ → 500.
                var patchBody = JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        op = "add",
                        path = $"/connectionPermissions/{connectionId}",
                        value = "READ",
                    },
                });
                using var patched = await SendAsync(
                    new HttpMethod("PATCH"), userPermissionsUrl, adminToken,
                    patchBody, cancellationToken);
                _ = patched;
            }
        }
    }

    private async Task<string> GetAdminTokenAsync(
        string apiBase, CancellationToken cancellationToken)
    {
        var adminUser = configuration[AdminUserKey] ?? DefaultAdminUser;
        var adminPassword = configuration[AdminPasswordKey];
        if (string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException(
                "Gateway de acesso remoto sem credencial de administração "
                + "(RemoteAccess:Guacamole:AdminPassword) — não é possível "
                + "garantir a conexão do device.");

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{apiBase}/api/tokens");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["username"] = adminUser,
                ["password"] = adminPassword,
            });

        using var response = await SendSafeAsync(client, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                "Gateway de acesso remoto recusou a credencial de administração "
                + $"(HTTP {(int)response.StatusCode}).");

        using var json = await ReadJsonAsync(response, cancellationToken);
        var token = json.RootElement.TryGetProperty("authToken", out var element)
            ? element.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "Gateway de acesso remoto devolveu sessão de administração sem token.");
        return token;
    }

    private async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string url,
        string adminToken,
        string? jsonBody,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(
            method, $"{url}?token={adminToken}");
        request.Headers.Add("Cookie", $"JSESSIONID={adminToken}");
        if (jsonBody is not null)
        {
            request.Content = new StringContent(
                jsonBody, Encoding.UTF8, "application/json");
        }

        var response = await SendSafeAsync(client, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                throw new InvalidOperationException(
                    $"Gateway de acesso remoto falhou em {method.Method} "
                    + $"{url} (HTTP {(int)response.StatusCode}).");
            }
        }

        // 204/201 sem corpo (DELETE/PATCH/POST vazio) → documento neutro.
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        response.Dispose();
        return bytes.Length == 0
            ? JsonDocument.Parse("{}")
            : JsonDocument.Parse(bytes);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, default, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendSafeAsync(
        HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Gateway de acesso remoto indisponível — verifique se o profile "
                + "'remote' está de pé (docker compose --profile remote up -d).",
                exception);
        }
    }

    private string GetApiBase()
    {
        var baseUrl = configuration[BaseUrlKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "Gateway de acesso remoto não configurado "
                + "(RemoteAccess:Guacamole:BaseUrl) — suba o profile 'remote'.");
        return baseUrl.TrimEnd('/');
    }
}
