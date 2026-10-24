using InfraControl.Application.Notifications.Channels;

namespace InfraControl.Application.Integrations.WhatsApp.Ports;

/// <summary>
/// ADR-062: cliente de gestão da Evolution API (instância, conexão/QR,
/// grupos e envio de teste). Consome o HttpClient nomeado "evolution-api".
/// Todos os métodos recebem as options efetivas já resolvidas (banco → env)
/// para que a troca de config sem restart seja refletida por chamada.
/// A ApiKey vai no header <c>apikey</c> e nunca aparece em log/ exceção.
/// </summary>
public interface IEvolutionInstanceClient
{
    /// <summary>GET /instance/connectionState/{instance} → open/connecting/close.</summary>
    Task<string> GetConnectionStateAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// GET /instance/connect/{instance} → QR (base64 data-URI) + pairing code.
    /// Workaround #1408: o chamador deve fazer logout (best-effort) antes
    /// quando a instância está presa em close/connecting.
    /// </summary>
    Task<EvolutionQrResult> ConnectAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken);

    /// <summary>DELETE /instance/logout/{instance} — desconecta a instância.</summary>
    Task LogoutAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// POST /instance/create (body) — cria a instância.
    /// HTTP 409 indica que ela já existe; outras falhas HTTP são explícitas.
    /// </summary>
    Task<EvolutionInstanceCreationResult> CreateInstanceAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// GET /group/fetchAllGroups/{instance}?getParticipants=false — lista os
    /// grupos do número conectado (id=@g.us, subject, size, owner, desc).
    /// </summary>
    Task<IReadOnlyList<EvolutionGroupDto>> ListGroupsAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// POST /message/sendText/{instance} — envia texto (usado pelo teste da UI).
    /// Lança em falha HTTP (o chamador trata e grava last_test_error).
    /// </summary>
    Task SendTextAsync(
        WhatsAppChannelOptions options, string number, string text,
        CancellationToken cancellationToken);
}

/// <summary>Resultado da geração de QR (base64 data-URI do PNG + pairing code).</summary>
public sealed record EvolutionQrResult(string? Base64, string? PairingCode);

/// <summary>Resultado real da criação de instância na Evolution.</summary>
public sealed record EvolutionInstanceCreationResult(bool Created);

/// <summary>Grupo do WhatsApp exposto pela Evolution (contrato fetchAllGroups).</summary>
public sealed record EvolutionGroupDto(
    string Id,
    string Subject,
    int Size,
    string? Owner,
    string? Desc,
    bool Restrict,
    bool Announce);
