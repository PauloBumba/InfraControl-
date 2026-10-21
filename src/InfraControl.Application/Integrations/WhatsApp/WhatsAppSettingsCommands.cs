using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.WhatsApp;

/// <summary>
/// ADR-062: visão da configuração do canal WhatsApp para a UI. A ApiKey
/// nunca sai — só o indicador <see cref="ApiKeySet"/>; <see cref="Source"/>
/// diz de onde veio a config efetiva ("database" = linha persistida,
/// "environment" = env). O estado de conexão e o último teste vêm da linha.
/// </summary>
public sealed record WhatsAppSettingsDto(
    string BaseUrl,
    bool ApiKeySet,
    string InstanceId,
    string? GroupId,
    string? Phone,
    string? ConnectionState,
    string Source,
    DateTime? LastTestedAtUtc,
    bool LastTestOk,
    string? LastTestError,
    DateTime? UpdatedAtUtc);

/// <summary>Resultado do envio de teste — nunca lança por falha de rede.</summary>
public sealed record WhatsAppTestResultDto(bool Ok, string? Error);

public sealed record GetWhatsAppSettingsQuery
    : BaseQuery<WhatsAppSettingsDto>, IAuthorizedRequest
{
    public override IReadOnlyCollection<string> CacheTags => ["whatsapp-settings"];
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed record SaveWhatsAppSettingsRequest(
    string BaseUrl,
    string InstanceId,
    string? ApiKey,
    string? GroupId,
    string? Phone);

public sealed record SaveWhatsAppSettingsCommand(SaveWhatsAppSettingsRequest Request)
    : BaseCommand<WhatsAppSettingsDto>, IAuthorizedRequest, IAuditableRequest
{
    public override IReadOnlyCollection<string> CacheKeysToInvalidate => ["whatsapp-settings"];
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "WhatsAppSettings";
}

public sealed record SendWhatsAppTestCommand
    : BaseCommand<WhatsAppTestResultDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class GetWhatsAppSettingsQueryHandler(
    IWhatsAppSettingsRepository repository,
    IWhatsAppEnvOptionsAccessor envAccessor)
    : IRequestHandler<GetWhatsAppSettingsQuery, WhatsAppSettingsDto>
{
    public async Task<WhatsAppSettingsDto> Handle(
        GetWhatsAppSettingsQuery request, CancellationToken cancellationToken)
    {
        var setting = await repository.GetAsync(cancellationToken);
        if (setting is not null)
            return ToDto(setting, "database");

        var env = envAccessor.Current;
        return new WhatsAppSettingsDto(
            env.BaseUrl,
            ApiKeySet: !string.IsNullOrEmpty(env.ApiKey),
            env.InstanceId,
            GroupId: string.IsNullOrWhiteSpace(env.GroupId) ? null : env.GroupId,
            Phone: string.IsNullOrWhiteSpace(env.Phone) ? null : env.Phone,
            ConnectionState: null,
            Source: "environment",
            LastTestedAtUtc: null,
            LastTestOk: false,
            LastTestError: null,
            UpdatedAtUtc: null);
    }

    internal static WhatsAppSettingsDto ToDto(
        WhatsAppIntegrationSetting setting, string source) =>
        new(
            setting.BaseUrl,
            ApiKeySet: !string.IsNullOrEmpty(setting.ApiKeyEncrypted),
            setting.InstanceId,
            setting.GroupId,
            setting.Phone,
            setting.ConnectionState,
            source,
            setting.LastTestedAtUtc,
            setting.LastTestOk,
            setting.LastTestError,
            setting.UpdateDateTime);
}

public sealed class SaveWhatsAppSettingsCommandHandler(
    IWhatsAppSettingsRepository repository,
    ICredentialProtector protector)
    : IRequestHandler<SaveWhatsAppSettingsCommand, WhatsAppSettingsDto>
{
    public async Task<WhatsAppSettingsDto> Handle(
        SaveWhatsAppSettingsCommand request, CancellationToken cancellationToken)
    {
        var payload = request.Request;
        var existing = await repository.GetAsync(cancellationToken);

        // ApiKey cifrada existente é preservada quando o payload não envia uma nova
        var encryptedApiKey = string.IsNullOrEmpty(payload.ApiKey)
            ? existing?.ApiKeyEncrypted
            : protector.Protect(payload.ApiKey);

        if (string.IsNullOrEmpty(encryptedApiKey))
            throw new ArgumentException(
                "A ApiKey da Evolution é obrigatória na primeira configuração.",
                nameof(payload));

        var setting = existing ?? WhatsAppIntegrationSetting.Create(
            payload.BaseUrl,
            encryptedApiKey,
            payload.InstanceId,
            payload.GroupId,
            payload.Phone);

        if (existing is not null)
            existing.Update(
                payload.BaseUrl,
                encryptedApiKey,
                payload.InstanceId,
                payload.GroupId,
                payload.Phone);

        await repository.SaveAsync(setting, cancellationToken);

        return GetWhatsAppSettingsQueryHandler.ToDto(setting, "database");
    }
}

public sealed class SendWhatsAppTestCommandHandler(
    IWhatsAppOptionsResolver optionsResolver,
    IWhatsAppSettingsRepository repository,
    IEvolutionInstanceClient client,
    ILogger<SendWhatsAppTestCommandHandler> logger)
    : IRequestHandler<SendWhatsAppTestCommand, WhatsAppTestResultDto>
{
    public async Task<WhatsAppTestResultDto> Handle(
        SendWhatsAppTestCommand request, CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);

        var number = !string.IsNullOrWhiteSpace(options.GroupId)
            ? options.GroupId
            : options.Phone;

        if (string.IsNullOrWhiteSpace(number))
            return new WhatsAppTestResultDto(
                false,
                "Sem destino — escolha um grupo (ou informe um número) antes de testar.");

        try
        {
            await client.SendTextAsync(
                options, number,
                "✅ InfraControl: teste do canal WhatsApp (ADR-062).",
                cancellationToken);

            await RecordTestAsync(ok: true, error: null, cancellationToken);
            return new WhatsAppTestResultDto(true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Teste de envio WhatsApp falhou para {Number}", number);
            await RecordTestAsync(ok: false, error: ex.Message, cancellationToken);
            return new WhatsAppTestResultDto(false, ex.Message);
        }
    }

    /// <summary>
    /// Persiste o resultado do teste na linha de configuração, quando ela
    /// existir (config só de env não gera linha). Falha ao persistir o
    /// resultado nunca invalida o próprio teste.
    /// </summary>
    private async Task RecordTestAsync(
        bool ok, string? error, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await repository.GetAsync(cancellationToken);
            if (setting is null)
                return;

            setting.RecordTestResult(
                ok, AppTimeProvider.Current.GetUtcNow().UtcDateTime, error);
            await repository.SaveAsync(setting, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao persistir resultado do teste WhatsApp");
        }
    }
}
