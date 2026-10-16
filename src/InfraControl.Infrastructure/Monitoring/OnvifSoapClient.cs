using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Cliente SOAP 1.2/WS-Addressing do ONVIF (F30-T005a — docs/46 §C,
/// "prioridade alta: padrão universal"). POST em <c>/onvif/device_service</c>
/// com credencial digest/basic via <see cref="OnvifOptions"/> (env/config —
/// senha nunca vira Value nem mensagem de erro). Parsing tolerante por
/// LocalName (XDocument nativo — sem NuGet); SOAP Fault ou resposta
/// incompleta devolve <c>null</c> = "não suportado" (docs/46 §Dados
/// indisponíveis), erro de transporte/HTTP lança exceção. Seam testável:
/// HttpMessageHandler injectable nos testes (nenhuma rede real).
/// </summary>
public sealed class OnvifSoapClient : IOnvifClient
{
    private const string DeviceServicePath = "/onvif/device_service";
    private const string DeviceWsdl = "http://www.onvif.org/ver10/device/wsdl";
    private const string SoapNs = "http://www.w3.org/2003/05/soap-envelope";
    private const string WsaNs = "http://www.w3.org/2005/08/addressing";

    private readonly OnvifOptions _options;
    private readonly HttpMessageHandler? _testHandler;
    private readonly Lazy<HttpClient> _client;

    public OnvifSoapClient(
        Microsoft.Extensions.Options.IOptions<OnvifOptions> options,
        HttpMessageHandler? testHandler = null)
    {
        _options = options.Value;
        _testHandler = testHandler;
        _client = new Lazy<HttpClient>(CreateClient);
    }

    public async Task<OnvifStorageInfo?> GetStorageAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var document = await PostAsync(
            target, "GetStorage", "<tds:GetStorage />", timeout, cancellationToken);
        if (document is null)
            return null;

        var container = document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName is "Storage"
                or "StorageInformation" or "StorageInfo")
            ?? document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "GetStorageResponse");

        var total = FirstLong(container, "Total", "TotalSpace", "Capacity", "TotalCapacity");
        var free = FirstLong(container, "Free", "FreeSpace", "AvailableSpace", "FreeCapacity");
        var used = FirstLong(container, "Used", "UsedSpace", "UsedCapacity");

        if (total is null or <= 0)
            return null;

        if (free is null && used is not null)
            free = Math.Max(0, total.Value - used.Value);

        if (free is null)
            return null;

        // Free > Total = resposta inconsistente → trata como não-suportado.
        if (free > total)
            return null;

        var retention = FirstInt(container, "RetentionDays", "Retention");
        return new OnvifStorageInfo(total.Value, free.Value, retention);
    }

    public async Task<OnvifDeviceInformation?> GetDeviceInformationAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var document = await PostAsync(
            target, "GetDeviceInformation", "<tds:GetDeviceInformation />",
            timeout, cancellationToken);
        if (document is null)
            return null;

        var response = document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "GetDeviceInformationResponse");
        if (response is null)
            return null;

        var manufacturer = FirstText(response, "Manufacturer");
        if (string.IsNullOrWhiteSpace(manufacturer))
            return null;

        return new OnvifDeviceInformation(
            manufacturer,
            FirstText(response, "Model") ?? "",
            FirstText(response, "FWVersion", "FirmwareVersion") ?? "");
    }

    public async Task<OnvifRecordingStatus?> GetRecordingStatusAsync(
        string target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var document = await PostAsync(
            target, "GetRecordingStatus", "<tds:GetRecordingStatus />",
            timeout, cancellationToken);
        if (document is null)
            return null;

        var recording = FirstBool(
            document, "Recording", "RecordingActive", "RecordingStatus", "State");
        var videoLoss = FirstBool(document, "VideoLoss", "VideoLost");
        var tamper = FirstBool(document, "Tamper", "TamperDetected", "Tampering");

        if (recording is null && videoLoss is null && tamper is null)
            return null;

        return new OnvifRecordingStatus(recording, videoLoss, tamper);
    }

    /// <summary>
    /// POST SOAP; devolve o documento ou <c>null</c> para Fault. Exceção =
    /// transporte/HTTP/timeout (o provider converte em falha pt-BR).
    /// </summary>
    private async Task<XDocument?> PostAsync(
        string target,
        string operation,
        string body,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Destino do ONVIF é obrigatório.", nameof(target));

        var envelope =
            $"<s:Envelope xmlns:s=\"{SoapNs}\" xmlns:wsa=\"{WsaNs}\" "
            + $"xmlns:tds=\"{DeviceWsdl}\">"
            + $"<s:Header><wsa:Action>{DeviceWsdl}/{operation}</wsa:Action></s:Header>"
            + $"<s:Body>{body}</s:Body></s:Envelope>";

        var uri = BuildServiceUri(target.Trim());

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(timeout);

        HttpResponseMessage response;
        string payload;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml")
            };
            request.Content.Headers.ContentType!.Parameters.Add(
                new NameValueHeaderValue("action", $"\"{DeviceWsdl}/{operation}\""));

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
            throw new TimeoutException("Tempo esgotado ao consultar ONVIF.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"HTTP {(int)response.StatusCode} na resposta ONVIF "
                + $"({response.ReasonPhrase}).");
        }

        // Parse fora do try: XmlException/InvalidOperation propagam para o
        // provider (resposta inválida = falha, docs/46 §Erros).
        var document = XDocument.Parse(payload);
        var hasFault = document
            .Descendants()
            .Any(element => element.Name.LocalName == "Fault");
        return hasFault ? null : document;
    }

    private Uri BuildServiceUri(string target)
    {
        // IPv6 literal precisa de colchetes no UriBuilder (mesmo cuidado do
        // HttpWanMonitoringProvider).
        var host = target.Contains(':') && !target.StartsWith('[')
            ? $"[{target}]"
            : target;
        var builder = new UriBuilder("http", host, _options.Port, DeviceServicePath);
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

    private static long? FirstLong(XElement? scope, params string[] localNames)
    {
        if (scope is null)
            return null;
        foreach (var name in localNames)
        {
            var element = scope.Descendants()
                .FirstOrDefault(candidate => candidate.Name.LocalName == name);
            if (element is not null && long.TryParse(element.Value.Trim(), out var parsed))
                return parsed;
        }

        return null;
    }

    private static int? FirstInt(XElement? scope, params string[] localNames)
    {
        if (scope is null)
            return null;
        foreach (var name in localNames)
        {
            var element = scope.Descendants()
                .FirstOrDefault(candidate => candidate.Name.LocalName == name);
            if (element is not null && int.TryParse(element.Value.Trim(), out var parsed))
                return parsed;
        }

        return null;
    }

    private static string? FirstText(XElement scope, params string[] localNames)
    {
        foreach (var name in localNames)
        {
            var element = scope.Elements()
                .FirstOrDefault(candidate => candidate.Name.LocalName == name);
            if (element is not null)
                return element.Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Primeiro elemento cujo LocalName casa e cujo valor é bool-ish
    /// (true/false/1/0/active/stopped…); valor não reconhecível = passa
    /// para o próximo candidato.
    /// </summary>
    private static bool? FirstBool(XDocument document, params string[] localNames)
    {
        foreach (var name in localNames)
        {
            var element = document
                .Descendants()
                .FirstOrDefault(candidate => candidate.Name.LocalName == name);
            if (element is null)
                continue;

            var parsed = ParseBool(element.Value);
            if (parsed.HasValue)
                return parsed;
        }

        return null;
    }

    private static bool? ParseBool(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "true" or "1" or "on" or "active" or "yes" => true,
        "false" or "0" or "off" or "inactive" or "stopped" or "no" => false,
        _ => null
    };
}
