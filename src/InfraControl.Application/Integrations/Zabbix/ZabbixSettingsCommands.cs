using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.Zabbix;

/// <summary>
/// F27 Fase 2.2: visão da configuração Zabbix para a UI. A senha nunca sai
/// — só o indicador <see cref="PasswordSet"/>; <see cref="Source"/> diz de
/// onde veio a config efetiva ("database" = linha persistida,
/// "environment" = env). O estado/versão/erros vêm do último teste de
/// conexão persistido na própria linha.
/// </summary>
public sealed record ZabbixSettingsDto(
    string ApiUrl,
    string Username,
    bool PasswordSet,
    string Source,
    string? DetectedVersion,
    DateTime? LastCommunicationAtUtc,
    DateTime? LastInventorySyncAtUtc,
    DateTime? LastTestedAtUtc,
    bool LastTestOk,
    string? LastTestError,
    DateTime? UpdatedAtUtc);

/// <summary>Resultado do teste de conexão — nunca lança por falha de rede.</summary>
public sealed record ZabbixConnectionTestResultDto(
    bool Ok,
    string? Version,
    string? Error);

public sealed record GetZabbixSettingsQuery
    : IQuery<ZabbixSettingsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed record SaveZabbixSettingsRequest(
    string ApiUrl,
    string Username,
    string? Password);

public sealed record SaveZabbixSettingsCommand(SaveZabbixSettingsRequest Request)
    : BaseCommand<ZabbixSettingsDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "ZabbixSettings";
}

public sealed record TestZabbixConnectionCommand
    : BaseCommand<ZabbixConnectionTestResultDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class GetZabbixSettingsQueryHandler(
    IZabbixSettingsRepository repository,
    IZabbixApiOptionsAccessor envAccessor)
    : IRequestHandler<GetZabbixSettingsQuery, ZabbixSettingsDto>
{
    public async Task<ZabbixSettingsDto> Handle(
        GetZabbixSettingsQuery request, CancellationToken cancellationToken)
    {
        var setting = await repository.GetAsync(cancellationToken);
        if (setting is not null)
            return ToDto(setting, "database");

        var env = envAccessor.Current;
        return new ZabbixSettingsDto(
            env.ApiUrl,
            env.Username,
            PasswordSet: !string.IsNullOrEmpty(env.Password),
            Source: "environment",
            DetectedVersion: null,
            LastCommunicationAtUtc: null,
            LastInventorySyncAtUtc: null,
            LastTestedAtUtc: null,
            LastTestOk: false,
            LastTestError: null,
            UpdatedAtUtc: null);
    }

    internal static ZabbixSettingsDto ToDto(
        ZabbixIntegrationSetting setting, string source) =>
        new(
            setting.ApiUrl,
            setting.Username,
            PasswordSet: !string.IsNullOrEmpty(setting.PasswordEncrypted),
            source,
            setting.DetectedVersion,
            setting.LastCommunicationAtUtc,
            setting.LastInventorySyncAtUtc,
            setting.LastTestedAtUtc,
            setting.LastTestOk,
            setting.LastTestError,
            setting.UpdateDateTime);
}

public sealed class SaveZabbixSettingsCommandHandler(
    IZabbixSettingsRepository repository,
    ICredentialProtector protector)
    : IRequestHandler<SaveZabbixSettingsCommand, ZabbixSettingsDto>
{
    public async Task<ZabbixSettingsDto> Handle(
        SaveZabbixSettingsCommand request, CancellationToken cancellationToken)
    {
        var payload = request.Request;
        var existing = await repository.GetAsync(cancellationToken);

        // senha cifrada existente é preservada quando o payload não envia uma nova
        var encryptedPassword = string.IsNullOrEmpty(payload.Password)
            ? existing?.PasswordEncrypted
            : protector.Protect(payload.Password);

        if (string.IsNullOrEmpty(encryptedPassword))
            throw new ArgumentException(
                "A senha da API do Zabbix é obrigatória na primeira configuração.",
                nameof(payload));

        var setting = existing ?? ZabbixIntegrationSetting.Create(
            payload.ApiUrl,
            payload.Username,
            encryptedPassword);

        if (existing is not null)
            existing.Update(
                payload.ApiUrl,
                payload.Username,
                encryptedPassword);

        await repository.SaveAsync(setting, cancellationToken);

        return GetZabbixSettingsQueryHandler.ToDto(setting, "database");
    }
}

public sealed class TestZabbixConnectionCommandHandler(
    IZabbixOptionsResolver optionsResolver,
    IZabbixSettingsRepository repository,
    IZabbixClient client,
    ILogger<TestZabbixConnectionCommandHandler> logger)
    : IRequestHandler<TestZabbixConnectionCommand, ZabbixConnectionTestResultDto>
{
    public async Task<ZabbixConnectionTestResultDto> Handle(
        TestZabbixConnectionCommand request, CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        if (!options.IsApiConfigured)
            return new ZabbixConnectionTestResultDto(
                false, null,
                "Configuração incompleta — informe URL, usuário e senha da API.");

        try
        {
            var version = await client.GetVersionAsync(cancellationToken);
            await RecordTestAsync(ok: true, version, error: null, cancellationToken);
            return new ZabbixConnectionTestResultDto(true, version, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Teste de conexão Zabbix falhou para {ApiUrl}", options.ApiUrl);
            await RecordTestAsync(ok: false, version: null, error: ex.Message, cancellationToken);
            return new ZabbixConnectionTestResultDto(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Persiste o resultado do teste na linha de configuração, quando ela
    /// existir (config só de env não gera linha). Falha ao persistir o
    /// resultado nunca invalida o próprio teste.
    /// </summary>
    private async Task RecordTestAsync(
        bool ok, string? version, string? error, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await repository.GetAsync(cancellationToken);
            if (setting is null)
                return;

            setting.RecordTestResult(
                ok, version, AppTimeProvider.Current.GetUtcNow().UtcDateTime, error);
            await repository.SaveAsync(setting, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Falha ao persistir resultado do teste de conexão Zabbix");
        }
    }
}
