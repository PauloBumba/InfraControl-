using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.WhatsApp;

/// <summary>QR decodificado para exibição na UI (bytes PNG + pairing code).</summary>
public sealed record WhatsAppQrDto(byte[]? Png, string? PairingCode, string? State);

/// <summary>Estado de conexão da instância (open/connecting/close).</summary>
public sealed record WhatsAppConnectionStateDto(string? State);

/// <summary>Resultado da operação de criação da instância configurada.</summary>
public sealed record WhatsAppInstanceDto(string InstanceId, bool Created, string? State);

/// <summary>Grupo do WhatsApp para seleção na UI.</summary>
public sealed record WhatsAppGroupDto(
    string Id,
    string Subject,
    int Size,
    string? Owner,
    string? Desc,
    bool Restrict,
    bool Announce);

// ── Conexão / QR (ADR-062) ───────────────────────────────────────────

public sealed record GenerateWhatsAppQrCommand
    : BaseCommand<WhatsAppQrDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed record DisconnectWhatsAppInstanceCommand
    : BaseCommand<WhatsAppConnectionStateDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "WhatsAppInstance";
}

public sealed record CreateWhatsAppInstanceCommand
    : BaseCommand<WhatsAppInstanceDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "WhatsAppInstance";
}

public sealed record GetWhatsAppConnectionStateQuery
    : BaseQuery<WhatsAppConnectionStateDto>, IAuthorizedRequest
{
    public override IReadOnlyCollection<string> CacheTags => [];
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(5);
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed record ListWhatsAppGroupsQuery
    : BaseQuery<IReadOnlyList<WhatsAppGroupDto>>, IAuthorizedRequest
{
    public override IReadOnlyCollection<string> CacheTags => [];
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class GenerateWhatsAppQrCommandHandler(
    IWhatsAppOptionsResolver optionsResolver,
    IWhatsAppSettingsRepository repository,
    IEvolutionInstanceClient client,
    ILogger<GenerateWhatsAppQrCommandHandler> logger)
    : IRequestHandler<GenerateWhatsAppQrCommand, WhatsAppQrDto>
{
    public async Task<WhatsAppQrDto> Handle(
        GenerateWhatsAppQrCommand request, CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);

        // Workaround #1408 (spike F33-T001): instância presa em close/
        // connecting só gera QR novo após logout best-effort.
        await TryLogoutAsync(options, cancellationToken);

        var qr = await client.ConnectAsync(options, cancellationToken);
        var state = await TryGetStateAsync(options, cancellationToken);
        await RecordStateAsync(state, cancellationToken);

        return new WhatsAppQrDto(DecodePng(qr.Base64), qr.PairingCode, state);
    }

    private async Task TryLogoutAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken)
    {
        try
        {
            await client.LogoutAsync(options, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "WhatsApp: logout best-effort antes do connect falhou (ok).");
        }
    }

    private async Task<string?> TryGetStateAsync(
        WhatsAppChannelOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var state = await client.GetConnectionStateAsync(options, cancellationToken);
            return string.IsNullOrWhiteSpace(state) ? null : state;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "WhatsApp: falha ao ler estado após connect.");
            return null;
        }
    }

    private async Task RecordStateAsync(string? state, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await repository.GetAsync(cancellationToken);
            if (setting is null)
                return;
            setting.RecordConnectionState(state);
            await repository.SaveAsync(setting, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao persistir estado de conexão WhatsApp");
        }
    }

    /// <summary>Decodifica o data-URI base64 do QR em bytes PNG.</summary>
    internal static byte[]? DecodePng(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;

        var payload = base64;
        var commaIndex = payload.IndexOf(',');
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            && commaIndex > 0)
            payload = payload[(commaIndex + 1)..];

        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

public sealed class DisconnectWhatsAppInstanceCommandHandler(
    IWhatsAppOptionsResolver optionsResolver,
    IWhatsAppSettingsRepository repository,
    IEvolutionInstanceClient client,
    ILogger<DisconnectWhatsAppInstanceCommandHandler> logger)
    : IRequestHandler<DisconnectWhatsAppInstanceCommand, WhatsAppConnectionStateDto>
{
    public async Task<WhatsAppConnectionStateDto> Handle(
        DisconnectWhatsAppInstanceCommand request, CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        await client.LogoutAsync(options, cancellationToken);

        var state = "close";
        try
        {
            var live = await client.GetConnectionStateAsync(options, cancellationToken);
            if (!string.IsNullOrWhiteSpace(live))
                state = live;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "WhatsApp: falha ao ler estado após logout.");
        }

        await RecordStateAsync(state, cancellationToken);
        return new WhatsAppConnectionStateDto(state);
    }

    private async Task RecordStateAsync(string state, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await repository.GetAsync(cancellationToken);
            if (setting is null)
                return;
            setting.RecordConnectionState(state);
            await repository.SaveAsync(setting, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao persistir estado de conexão WhatsApp");
        }
    }
}

public sealed class CreateWhatsAppInstanceCommandHandler(
    IWhatsAppOptionsResolver optionsResolver,
    IEvolutionInstanceClient client,
    ILogger<CreateWhatsAppInstanceCommandHandler> logger)
    : IRequestHandler<CreateWhatsAppInstanceCommand, WhatsAppInstanceDto>
{
    public async Task<WhatsAppInstanceDto> Handle(
        CreateWhatsAppInstanceCommand request, CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        var creation = await client.CreateInstanceAsync(options, cancellationToken);

        string? state = null;
        try
        {
            state = await client.GetConnectionStateAsync(options, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "WhatsApp: falha ao ler estado após create.");
        }

        return new WhatsAppInstanceDto(options.InstanceId, creation.Created, state);
    }
}

public sealed class GetWhatsAppConnectionStateQueryHandler(
    IWhatsAppOptionsResolver optionsResolver,
    IEvolutionInstanceClient client)
    : IRequestHandler<GetWhatsAppConnectionStateQuery, WhatsAppConnectionStateDto>
{
    public async Task<WhatsAppConnectionStateDto> Handle(
        GetWhatsAppConnectionStateQuery request, CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        try
        {
            var state = await client.GetConnectionStateAsync(options, cancellationToken);
            return new WhatsAppConnectionStateDto(
                string.IsNullOrWhiteSpace(state) ? null : state);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new WhatsAppConnectionStateDto(ex is HttpRequestException ? null : "error");
        }
    }
}

public sealed class ListWhatsAppGroupsQueryHandler(
    IWhatsAppOptionsResolver optionsResolver,
    IEvolutionInstanceClient client)
    : IRequestHandler<ListWhatsAppGroupsQuery, IReadOnlyList<WhatsAppGroupDto>>
{
    public async Task<IReadOnlyList<WhatsAppGroupDto>> Handle(
        ListWhatsAppGroupsQuery request, CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        var groups = await client.ListGroupsAsync(options, cancellationToken);

        return groups
            .Select(g => new WhatsAppGroupDto(
                g.Id, g.Subject, g.Size, g.Owner, g.Desc, g.Restrict, g.Announce))
            .OrderBy(g => g.Subject, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
