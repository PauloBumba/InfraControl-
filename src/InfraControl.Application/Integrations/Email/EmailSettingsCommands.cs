using InfraControl.Application.Integrations.Email.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.Email;

/// <summary>
/// F41-T001: visão da configuração SMTP para a UI. A senha nunca sai —
/// só o indicador <see cref="PasswordSet"/>; <see cref="Source"/> diz de
/// onde veio a config efetiva ("database" = linha persistida,
/// "environment" = env). O estado/erros vêm do último teste persistido.
/// </summary>
public sealed record EmailSettingsDto(
    string Host,
    int Port,
    string Username,
    bool PasswordSet,
    string From,
    string? FromName,
    bool UseSsl,
    bool? NotificationsEnabled,
    string Source,
    DateTime? LastTestedAtUtc,
    bool LastTestOk,
    string? LastTestError,
    DateTime? UpdatedAtUtc);

/// <summary>Resultado do teste de envio — nunca lança por falha de rede.</summary>
public sealed record EmailTestResultDto(
    bool Ok,
    string? Error);

public sealed record GetEmailSettingsQuery
    : IQuery<EmailSettingsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed record SaveEmailSettingsRequest(
    string Host,
    int Port,
    string Username,
    string? Password,
    string From,
    string? FromName,
    bool UseSsl,
    bool? NotificationsEnabled);

public sealed record SaveEmailSettingsCommand(SaveEmailSettingsRequest Request)
    : BaseCommand<EmailSettingsDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "EmailSettings";
}

public sealed record SendEmailTestCommand
    : BaseCommand<EmailTestResultDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class GetEmailSettingsQueryHandler(
    IEmailSettingsRepository repository,
    IEmailEnvOptionsAccessor envAccessor)
    : IRequestHandler<GetEmailSettingsQuery, EmailSettingsDto>
{
    public async Task<EmailSettingsDto> Handle(
        GetEmailSettingsQuery request, CancellationToken cancellationToken)
    {
        var setting = await repository.GetAsync(cancellationToken);
        if (setting is not null)
            return ToDto(setting, "database");

        var env = envAccessor.Current;
        return new EmailSettingsDto(
            env.Host,
            env.Port,
            env.Username,
            PasswordSet: !string.IsNullOrEmpty(env.Password),
            env.From,
            env.FromName,
            env.UseSsl,
            NotificationsEnabled: null,
            Source: "environment",
            LastTestedAtUtc: null,
            LastTestOk: false,
            LastTestError: null,
            UpdatedAtUtc: null);
    }

    internal static EmailSettingsDto ToDto(
        EmailIntegrationSetting setting, string source) =>
        new(
            setting.Host,
            setting.Port,
            setting.Username,
            PasswordSet: !string.IsNullOrEmpty(setting.PasswordEncrypted),
            setting.From,
            setting.FromName,
            setting.UseSsl,
            setting.NotificationsEnabled,
            source,
            setting.LastTestedAtUtc,
            setting.LastTestOk,
            setting.LastTestError,
            setting.UpdateDateTime);
}

public sealed class SaveEmailSettingsCommandHandler(
    IEmailSettingsRepository repository,
    ICredentialProtector protector)
    : IRequestHandler<SaveEmailSettingsCommand, EmailSettingsDto>
{
    public async Task<EmailSettingsDto> Handle(
        SaveEmailSettingsCommand request, CancellationToken cancellationToken)
    {
        var payload = request.Request;
        var existing = await repository.GetAsync(cancellationToken);

        // senha cifrada existente é preservada quando o payload não envia uma nova
        var encryptedPassword = string.IsNullOrEmpty(payload.Password)
            ? existing?.PasswordEncrypted
            : protector.Protect(payload.Password);

        if (string.IsNullOrEmpty(encryptedPassword))
            throw new ArgumentException(
                "A senha SMTP é obrigatória na primeira configuração.",
                nameof(payload));

        var setting = existing ?? EmailIntegrationSetting.Create(
            payload.Host,
            payload.Port,
            payload.Username,
            encryptedPassword,
            payload.From,
            payload.FromName,
            payload.UseSsl);

        if (existing is not null)
            existing.Update(
                payload.Host,
                payload.Port,
                payload.Username,
                encryptedPassword,
                payload.From,
                payload.FromName,
                payload.UseSsl,
                payload.NotificationsEnabled);

        await repository.SaveAsync(setting, cancellationToken);

        return GetEmailSettingsQueryHandler.ToDto(setting, "database");
    }
}

/// <summary>
/// F41-T001: teste de envio com a configuração efetiva (banco → env).
/// Envia um e-mail de prova para o próprio remetente; falha de
/// rede/credencial vem no resultado (nunca exception opaca) e é
/// persistida na linha quando ela existir.
/// </summary>
public sealed class SendEmailTestCommandHandler(
    IEmailOptionsResolver optionsResolver,
    IEmailSettingsRepository repository,
    IEmailTestClient testClient,
    ILogger<SendEmailTestCommandHandler> logger)
    : IRequestHandler<SendEmailTestCommand, EmailTestResultDto>
{
    public async Task<EmailTestResultDto> Handle(
        SendEmailTestCommand request, CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        if (!options.IsSmtpConfigured)
            return new EmailTestResultDto(
                false,
                "Configuração incompleta — informe host e remetente do SMTP.");

        try
        {
            await testClient.SendTestAsync(options, cancellationToken);
            await RecordTestAsync(ok: true, error: null, cancellationToken);
            return new EmailTestResultDto(true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Teste de envio de e-mail falhou para {Host}", options.Host);
            await RecordTestAsync(ok: false, error: ex.Message, cancellationToken);
            return new EmailTestResultDto(false, ex.Message);
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
            logger.LogWarning(ex,
                "Falha ao persistir resultado do teste de envio de e-mail");
        }
    }
}
