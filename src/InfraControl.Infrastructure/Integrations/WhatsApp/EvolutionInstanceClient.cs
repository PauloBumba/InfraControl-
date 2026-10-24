using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Integrations.WhatsApp;

/// <summary>
/// ADR-062: cliente de gestão da Evolution API (v2.3.7). Fala com a mesma
/// instância HTTP do dispatcher (HttpClient nomeado "evolution-api").
/// Endpoints validados no spike F33-T001/T005 e documentados no ADR-062.
/// A ApiKey vai no header <c>apikey</c> e NUNCA é logada.
/// </summary>
public sealed class EvolutionInstanceClient(
    IHttpClientFactory httpClientFactory,
    ILogger<EvolutionInstanceClient> logger)
    : IEvolutionInstanceClient
{
    /// <summary>Reusa o client nomeado do dispatcher (resiliência já registrada).</summary>
    public const string HttpClientName = EvolutionApiWhatsAppNotificationDispatcher.HttpClientName;

    public async Task<string> GetConnectionStateAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken)
    {
        var client = CreateClient(options);
        var url = $"{options.BaseUrl.TrimEnd('/')}/instance/connectionState/{options.InstanceId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyApiKey(request, options);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ConnectionStateResponse>(
            cancellationToken: cancellationToken);
        return payload?.Instance?.State ?? string.Empty;
    }

    public async Task<EvolutionQrResult> ConnectAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken)
    {
        var client = CreateClient(options);
        var url = $"{options.BaseUrl.TrimEnd('/')}/instance/connect/{options.InstanceId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyApiKey(request, options);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ConnectResponse>(
            cancellationToken: cancellationToken);

        // O campo base64 vem como data-URI ("data:image/png;base64,...");
        // devolvemos cru — o consumidor (handler/UI) decodifica.
        return new EvolutionQrResult(payload?.Base64, payload?.PairingCode);
    }

    public async Task LogoutAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken)
    {
        var client = CreateClient(options);
        var url = $"{options.BaseUrl.TrimEnd('/')}/instance/logout/{options.InstanceId}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        ApplyApiKey(request, options);

        using var response = await client.SendAsync(request, cancellationToken);
        // 400 "not connected" é esperado quando já está desconectada — não é erro.
        if (!response.IsSuccessStatusCode
            && response.StatusCode != System.Net.HttpStatusCode.BadRequest)
            response.EnsureSuccessStatusCode();

        logger.LogInformation(
            "WhatsApp: logout da instância {InstanceId} (HTTP {StatusCode}).",
            options.InstanceId, (int)response.StatusCode);
    }

    public async Task<EvolutionInstanceCreationResult> CreateInstanceAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken)
    {
        var client = CreateClient(options);
        // Rota real da v2.3.7 é /instance/create (body), não /instance/create/{name}.
        var url = $"{options.BaseUrl.TrimEnd('/')}/instance/create";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new CreateInstanceRequest(
                options.InstanceId,
                Integration: "WHATSAPP-BAILEYS",
                Qrcode: true))
        };
        ApplyApiKey(request, options);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            logger.LogInformation(
                "WhatsApp: instância {InstanceId} já existe na Evolution.",
                options.InstanceId);
            return new EvolutionInstanceCreationResult(Created: false);
        }

        response.EnsureSuccessStatusCode();

        logger.LogInformation(
            "WhatsApp: create instância {InstanceId} (HTTP {StatusCode}).",
            options.InstanceId, (int)response.StatusCode);
        return new EvolutionInstanceCreationResult(Created: true);
    }

    public async Task<IReadOnlyList<EvolutionGroupDto>> ListGroupsAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken)
    {
        var client = CreateClient(options);
        var url = $"{options.BaseUrl.TrimEnd('/')}/group/fetchAllGroups/{options.InstanceId}"
                  + "?getParticipants=false";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyApiKey(request, options);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var groups = await response.Content.ReadFromJsonAsync<List<GroupResponse>>(
            cancellationToken: cancellationToken);

        return (groups ?? [])
            .Select(g => new EvolutionGroupDto(
                g.Id ?? string.Empty,
                g.Subject ?? string.Empty,
                g.Size,
                g.Owner,
                g.Desc,
                g.Restrict,
                g.Announce))
            .Where(g => !string.IsNullOrWhiteSpace(g.Id))
            .ToList();
    }

    public async Task SendTextAsync(
        WhatsAppChannelOptions options, string number, string text,
        CancellationToken cancellationToken)
    {
        var client = CreateClient(options);
        var url = $"{options.BaseUrl.TrimEnd('/')}/message/sendText/{options.InstanceId}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new SendTextRequest(number, text))
        };
        ApplyApiKey(request, options);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        logger.LogInformation(
            "WhatsApp: teste enviado para {Number} (HTTP {StatusCode}).",
            number, (int)response.StatusCode);
    }

    private HttpClient CreateClient(WhatsAppChannelOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Evolution:BaseUrl configurado (Notifications:Channels:WhatsApp:BaseUrl).");
        if (string.IsNullOrWhiteSpace(options.InstanceId))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Evolution:InstanceId configurado (Notifications:Channels:WhatsApp:InstanceId).");
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException(
                "Canal WhatsApp sem Evolution:ApiKey configurado (Notifications:Channels:WhatsApp:ApiKey).");

        return httpClientFactory.CreateClient(HttpClientName);
    }

    private static void ApplyApiKey(HttpRequestMessage request, WhatsAppChannelOptions options)
        => request.Headers.TryAddWithoutValidation("apikey", options.ApiKey);

    // ── Contratos JSON (v2.3.7) ───────────────────────────────────────

    private sealed record ConnectionStateResponse(
        [property: JsonPropertyName("instance")] InstanceState? Instance);

    private sealed record InstanceState(
        [property: JsonPropertyName("instanceName")] string? InstanceName,
        [property: JsonPropertyName("state")] string? State);

    private sealed record ConnectResponse(
        [property: JsonPropertyName("pairingCode")] string? PairingCode,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("base64")] string? Base64);

    private sealed record CreateInstanceRequest(
        [property: JsonPropertyName("instanceName")] string InstanceName,
        [property: JsonPropertyName("integration")] string Integration,
        [property: JsonPropertyName("qrcode")] bool Qrcode);

    private sealed record SendTextRequest(
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("text")] string Text);

    private sealed record GroupResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("size")] int Size,
        [property: JsonPropertyName("owner")] string? Owner,
        [property: JsonPropertyName("desc")] string? Desc,
        [property: JsonPropertyName("restrict")] bool Restrict,
        [property: JsonPropertyName("announce")] bool Announce);
}
