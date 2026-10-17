using System.Net;
using System.Text.Json;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Cliente HTTP/JSON da API Yealink (F30-T006a — docs/47 §C, "vendor
/// documentada"): GET nas rotas da ficha (/api/phone/line,
/// /api/phone/call/quality, /api/phone/status) com credencial digest/basic
/// via <see cref="YealinkOptions"/> (env/config — senha nunca vira Value
/// nem mensagem de erro). Parsing tolerante por chave (JsonDocument nativo
/// — sem NuGet, mesmo espírito do LocalName do OnvifSoapClient): resposta
/// sem campo reconhecível devolve <c>null</c> = "não suportado" (docs/47
/// §Dados indisponíveis), erro de transporte/HTTP lança exceção. Seam
/// testável: HttpMessageHandler injectable nos testes (nenhuma rede real).
/// </summary>
public sealed class YealinkPhoneClient : IYealinkPhoneClient
{
    private const string SipPath = "/api/phone/line";
    private const string QualityPath = "/api/phone/call/quality";
    private const string StatusPath = "/api/phone/status";

    private readonly YealinkOptions _options;
    private readonly HttpMessageHandler? _testHandler;
    private readonly Lazy<HttpClient> _client;

    public YealinkPhoneClient(
        Microsoft.Extensions.Options.IOptions<YealinkOptions> options,
        HttpMessageHandler? testHandler = null)
    {
        _options = options.Value;
        _testHandler = testHandler;
        _client = new Lazy<HttpClient>(CreateClient);
    }

    public async Task<YealinkSipInfo?> GetSipInfoAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var document = await GetJsonAsync(target, SipPath, timeout, cancellationToken);
        if (document is null)
            return null;

        // Linha pode vir como objeto único ou lista (multi-line): usa o
        // primeiro elemento com status reconhecível, senão o primeiro.
        var line = FirstObjectWith(document.RootElement, "reg_status", "registered")
            ?? FirstArrayElement(document.RootElement, "line", "lines")
            ?? document.RootElement;

        var registered = ReadBool(line, "reg_status", "registered", "status");
        var extension = ReadText(line, "extension", "line", "account");
        var sipServer = ReadText(line, "sip_server", "sipserver", "server", "domain");
        var codec = ReadText(line, "codec", "audio_codec", "preferred_codec");

        if (registered is null && extension is null
            && sipServer is null && codec is null)
            return null;

        return new YealinkSipInfo(registered, extension, sipServer, codec);
    }

    public async Task<YealinkCallQualityInfo?> GetCallQualityAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var document = await GetJsonAsync(target, QualityPath, timeout, cancellationToken);
        if (document is null)
            return null;

        var mos = ReadDecimal(document.RootElement, "mos", "mean_opinion_score");
        var jitter = ReadDecimal(document.RootElement, "jitter", "jitter_ms");
        var latency = ReadDecimal(document.RootElement, "latency", "latency_ms", "rtt");
        var loss = ReadDecimal(
            document.RootElement, "packet_loss", "packet_loss_percent", "loss");

        if (mos is null && jitter is null && latency is null && loss is null)
            return null;

        return new YealinkCallQualityInfo(mos, jitter, latency, loss);
    }

    public async Task<YealinkPoeInfo?> GetPoeStatusAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var document = await GetJsonAsync(target, StatusPath, timeout, cancellationToken);
        if (document is null)
            return null;

        var powered = ReadBool(document.RootElement, "poe", "poe_status", "powered");
        var watts = ReadDecimal(document.RootElement, "power", "poe_power", "watts");

        if (powered is null && watts is null)
            return null;

        return new YealinkPoeInfo(powered, watts);
    }

    /// <summary>
    /// GET JSON; devolve o documento ou <c>null</c> para resposta vazia.
    /// Exceção = transporte/HTTP/timeout (o provider converte em falha
    /// pt-BR).
    /// </summary>
    private async Task<JsonDocument?> GetJsonAsync(
        string target,
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Destino da API Yealink é obrigatório.", nameof(target));

        var uri = BuildApiUri(target.Trim(), path);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(timeout);

        HttpResponseMessage response;
        string payload;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            response = await _client.Value.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            using (response)
            {
                payload = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Tempo esgotado ao consultar API Yealink.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"HTTP {(int)response.StatusCode} na resposta Yealink "
                + $"({response.ReasonPhrase}).");
        }

        if (string.IsNullOrWhiteSpace(payload))
            return null;

        // Parse fora do try: JsonException/InvalidOperation propagam para o
        // provider (resposta inválida = falha, docs/47 §Erros).
        return JsonDocument.Parse(payload);
    }

    private Uri BuildApiUri(string target, string path)
    {
        // IPv6 literal precisa de colchetes no UriBuilder (mesmo cuidado do
        // OnvifSoapClient).
        var host = target.Contains(':') && !target.StartsWith('[')
            ? $"[{target}]"
            : target;
        var builder = new UriBuilder("http", host, _options.Port, path);
        return builder.Uri;
    }

    private HttpClient CreateClient()
    {
        if (_testHandler is not null)
        {
            return new HttpClient(_testHandler, disposeHandler: false)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        var sockets = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        if (!string.IsNullOrEmpty(_options.Username))
        {
            sockets.Credentials = new NetworkCredential(
                _options.Username, _options.Password);
        }

        return new HttpClient(sockets) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>
    /// Primeiro objeto do documento (busca recursiva) que contenha alguma
    /// das chaves — cobre o envelope {"code":0,"data":{...}} do Yealink.
    /// </summary>
    private static JsonElement? FirstObjectWith(
        JsonElement element, params string[] keys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var key in keys)
                {
                    if (TryGetPropertyIgnoreCase(element, key, out _))
                        return element;
                }

                foreach (var property in element.EnumerateObject())
                {
                    var found = FirstObjectWith(property.Value, keys);
                    if (found.HasValue)
                        return found;
                }

                return null;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var found = FirstObjectWith(item, keys);
                    if (found.HasValue)
                        return found;
                }

                return null;

            default:
                return null;
        }
    }

    /// <summary>Primeiro elemento de uma propriedade-array, ou <c>null</c>.</summary>
    private static JsonElement? FirstArrayElement(
        JsonElement element, params string[] keys)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var key in keys)
        {
            if (!TryGetPropertyIgnoreCase(element, key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Array
                && value.EnumerateArray().FirstOrDefault() is var first
                && first.ValueKind != JsonValueKind.Undefined)
                return first;

            if (value.ValueKind == JsonValueKind.Object)
                return value;
        }

        return null;
    }

    private static bool? ReadBool(JsonElement scope, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryFindProperty(scope, key, out var value))
                continue;

            var parsed = ParseBool(Text(value));
            if (parsed.HasValue)
                return parsed;
        }

        return null;
    }

    private static string? ReadText(JsonElement scope, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryFindProperty(scope, key, out var value))
                continue;

            var text = Text(value);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonElement scope, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryFindProperty(scope, key, out var value))
                continue;

            var text = Text(value);
            if (decimal.TryParse(
                    text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed))
                return parsed;
        }

        return null;
    }

    /// <summary>
    /// Busca recursiva da primeira propriedade com o nome (case-insensitive)
    /// — cobre o envelope {"code":0,"data":{...}} do Yealink sem depender da
    /// profundidade da chave.
    /// </summary>
    private static bool TryFindProperty(
        JsonElement element, string key, out JsonElement value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (TryGetPropertyIgnoreCase(element, key, out value))
                    return true;

                foreach (var property in element.EnumerateObject())
                {
                    if (TryFindProperty(property.Value, key, out value))
                        return true;
                }

                value = default;
                return false;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindProperty(item, key, out value))
                        return true;
                }

                value = default;
                return false;

            default:
                value = default;
                return false;
        }
    }

    private static string? Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()?.Trim(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null
    };

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element, string key, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static bool? ParseBool(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "true" or "1" or "on" or "registered" or "up" or "ok" or "yes" => true,
        "false" or "0" or "off" or "unregistered" or "down" or "fail"
            or "failed" or "no" => false,
        _ => null
    };
}
