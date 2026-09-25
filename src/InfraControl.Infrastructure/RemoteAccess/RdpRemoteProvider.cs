using System.Buffers.Binary;
using System.Net.Sockets;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Provider RDP por probe TCP/X.224 (F11-T003, ADR-013): conecta na porta
/// RDP e completa a negociação de conexão (X.224 CR/CC + RDP Negotiation,
/// MS-RDPBCGR 2.2.1.2) para confirmar que o endpoint fala RDP. Não
/// autentica (sem CredSSP/NLA); o socket fica aberto como conexão da
/// sessão até o encerramento. Erros viram RemoteConnectResult com mensagem
/// pt-BR — credencial (não usada) nunca aparece no erro.
/// </summary>
public sealed class RdpRemoteProvider(IOptions<RemoteRdpOptions> options)
    : IRemoteProvider
{
    // X.224/RDP Negotiation (MS-RDPBCGR 2.2.1.2.1/2.2.1.2.2)
    private const byte X224ConnectionConfirm = 0xD0;
    private const byte NegotiationResponse = 0x02;
    private const byte NegotiationFailure = 0x03;
    private const int MaxResponseLength = 4096;

    /// <summary>
    /// TPKT + X.224 Connection Request + RDP Negotiation Request
    /// (requestedProtocols = SSL | HYBRID), sem cookie.
    /// </summary>
    private static readonly byte[] ConnectionRequest =
    [
        0x03, 0x00, 0x00, 0x13, 0x0E, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x08, 0x00, 0x09, 0x00, 0x00, 0x00
    ];

    public string Name => "Rdp";

    public bool CanHandle(string providerKey) =>
        string.Equals(providerKey, "rdp", StringComparison.OrdinalIgnoreCase);

    public async Task<RemoteConnectResult> ConnectAsync(
        string host,
        RemoteCredential credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settings = options.Value;
        var client = new TcpClient();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

            await client.ConnectAsync(host, settings.Port, timeout.Token);

            var stream = client.GetStream();
            await stream.WriteAsync(ConnectionRequest, timeout.Token);

            var error = await NegotiateAsync(stream, timeout.Token);
            if (error is not null)
            {
                client.Dispose();
                return new RemoteConnectResult(false, null, error);
            }

            return new RemoteConnectResult(true, client, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            return new RemoteConnectResult(
                false, null,
                $"Falha ao conectar via RDP: tempo esgotado após {settings.TimeoutSeconds}s.");
        }
        catch (OperationCanceledException)
        {
            client.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            client.Dispose();
            return new RemoteConnectResult(
                false, null, $"Falha ao conectar via RDP: {exception.Message}");
        }
    }

    /// <summary>Lê e valida o X.224 Connection Confirm; null = RDP confirmado.</summary>
    private static async Task<string?> NegotiateAsync(
        NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(stream, header, cancellationToken)
            || header[0] != 0x03 || header[1] != 0x00)
            return "Endpoint não respondeu como RDP.";

        var length = (header[2] << 8) | header[3];
        if (length < 11 || length > MaxResponseLength)
            return "Endpoint não respondeu como RDP.";

        var payload = new byte[length - 4];
        if (!await ReadExactAsync(stream, payload, cancellationToken))
            return "Endpoint não respondeu como RDP.";

        // X.224 CC fixo: LI(1) + 0xD0(1) + dst-ref(2) + src-ref(2) + classe(1)
        if (payload.Length < 7
            || payload[1] != X224ConnectionConfirm
            || payload[0] + 1 != payload.Length)
            return "Endpoint não respondeu como RDP.";

        if (payload.Length == 7)
            return null; // CC sem RDP Negotiation — segurança padrão do RDP

        var negotiation = payload.AsSpan(7);
        if (negotiation.Length < 8)
            return "Endpoint não respondeu como RDP.";

        if (negotiation[0] == NegotiationFailure)
        {
            var code = BinaryPrimitives.ReadUInt32LittleEndian(negotiation[4..8]);
            return $"Servidor RDP recusou a negociação: {DescribeFailure(code)}";
        }

        return negotiation[0] == NegotiationResponse
            ? null
            : "Endpoint não respondeu como RDP.";
    }

    private static string DescribeFailure(uint code) => code switch
    {
        1 => "servidor exige SSL",
        2 => "servidor não possui certificado SSL",
        3 => "sinalizadores inconsistentes na negociação",
        4 => "servidor exige autenticação híbrida (NLA)",
        5 => "servidor exige SSL com autenticação de usuário",
        _ => $"código 0x{code:X8}"
    };

    private static async Task<bool> ReadExactAsync(
        NetworkStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken);
            if (count == 0)
                return false;
            read += count;
        }
        return true;
    }
}
