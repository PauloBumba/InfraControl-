using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// F26 Fase 2.2: visão da configuração UniFi para a UI. A senha nunca sai —
/// só o indicador <see cref="PasswordSet"/>; <see cref="Source"/> diz de
/// onde veio a config efetiva ("database" = linha persistida,
/// "environment" = env da F20).
/// </summary>
public sealed record UniFiSettingsDto(
    string BaseUrl,
    string Username,
    string Site,
    bool AllowInvalidTlsCertificate,
    bool PasswordSet,
    string Source,
    DateTime? UpdatedAtUtc);

/// <summary>Resultado do teste de conexão — nunca lança: erro vira <see cref="Error"/>.</summary>
public sealed record UniFiConnectionTestResultDto(
    bool Ok,
    int DevicesCount,
    string? Error,
    string? Version = null);

public sealed record GetUniFiSettingsQuery
    : IQuery<UniFiSettingsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed record SaveUniFiSettingsRequest(
    string BaseUrl,
    string Username,
    string? Password,
    string Site,
    bool AllowInvalidTlsCertificate);

public sealed record SaveUniFiSettingsCommand(SaveUniFiSettingsRequest Request)
    : BaseCommand<UniFiSettingsDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "UniFiSettings";
}

public sealed record TestUniFiConnectionCommand
    : BaseCommand<UniFiConnectionTestResultDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

/// <summary>F26 Fase 3.1: sites persistidos (visão da UI).</summary>
public sealed record UniFiSiteViewDto(
    string ShortId,
    string Name,
    string Description,
    bool Hidden,
    bool Missing,
    DateTime UpdatedAtUtc);

public sealed record GetUniFiSitesQuery
    : IQuery<IReadOnlyList<UniFiSiteViewDto>>
{
}

public sealed class GetUniFiSettingsQueryHandler(
    IUniFiSettingsRepository repository,
    IUnifiEnvOptionsAccessor envAccessor)
    : IRequestHandler<GetUniFiSettingsQuery, UniFiSettingsDto>
{
    public async Task<UniFiSettingsDto> Handle(
        GetUniFiSettingsQuery request, CancellationToken cancellationToken)
    {
        var setting = await repository.GetAsync(cancellationToken);
        if (setting is not null)
            return new UniFiSettingsDto(
                setting.BaseUrl,
                setting.Username,
                setting.Site,
                setting.AllowInvalidTlsCertificate,
                PasswordSet: !string.IsNullOrEmpty(setting.PasswordEncrypted),
                Source: "database",
                setting.UpdateDateTime);

        var env = envAccessor.Current;
        return new UniFiSettingsDto(
            env.BaseUrl,
            env.Username,
            env.Site,
            env.AllowInvalidTlsCertificate,
            PasswordSet: !string.IsNullOrEmpty(env.Password),
            Source: "environment",
            UpdatedAtUtc: null);
    }
}

/// <summary>F26 Fase 4: view viva para a UI (DTO do cliente, sem mapear).</summary>
public sealed record GetUnifiLiveClientsQuery
    : IQuery<IReadOnlyList<UnifiConnectedClientDto>>
{
}

public sealed record GetUnifiNetworksQuery
    : IQuery<IReadOnlyList<UnifiNetworkDto>>
{
}

public sealed class GetUnifiLiveClientsQueryHandler(
    IUnifiApiClient unifiApiClient)
    : IRequestHandler<GetUnifiLiveClientsQuery, IReadOnlyList<UnifiConnectedClientDto>>
{
    public Task<IReadOnlyList<UnifiConnectedClientDto>> Handle(
        GetUnifiLiveClientsQuery request, CancellationToken cancellationToken)
        => unifiApiClient.ListConnectedClientsAsync(cancellationToken);
}

public sealed record GetUnifiLivePortsQuery(string DeviceMac)
    : IQuery<IReadOnlyList<UnifiSwitchPortDto>>
{
}

public sealed class GetUnifiLivePortsQueryHandler(
    IUnifiApiClient unifiApiClient)
    : IRequestHandler<GetUnifiLivePortsQuery, IReadOnlyList<UnifiSwitchPortDto>>
{
    public Task<IReadOnlyList<UnifiSwitchPortDto>> Handle(
        GetUnifiLivePortsQuery request, CancellationToken cancellationToken)
        => unifiApiClient.ListSwitchPortsAsync(request.DeviceMac, cancellationToken);
}

public sealed record GetUnifiTopologyQuery : IQuery<UnifiTopologyDto>
{
}

public sealed class GetUnifiTopologyQueryHandler(
    IUnifiApiClient unifiApiClient)
    : IRequestHandler<GetUnifiTopologyQuery, UnifiTopologyDto>
{
    public Task<UnifiTopologyDto> Handle(
        GetUnifiTopologyQuery request, CancellationToken cancellationToken)
        => unifiApiClient.GetTopologyAsync(cancellationToken);
}

public sealed class GetUnifiNetworksQueryHandler(
    IUnifiApiClient unifiApiClient)
    : IRequestHandler<GetUnifiNetworksQuery, IReadOnlyList<UnifiNetworkDto>>
{
    public Task<IReadOnlyList<UnifiNetworkDto>> Handle(
        GetUnifiNetworksQuery request, CancellationToken cancellationToken)
        => unifiApiClient.ListNetworksAsync(cancellationToken);
}

public sealed class GetUniFiSitesQueryHandler(
    IUniFiSitesRepository sitesRepository)
    : IRequestHandler<GetUniFiSitesQuery, IReadOnlyList<UniFiSiteViewDto>>
{
    public async Task<IReadOnlyList<UniFiSiteViewDto>> Handle(
        GetUniFiSitesQuery request, CancellationToken cancellationToken)
    {
        var sites = await sitesRepository.ListAsync(cancellationToken);
        return sites
            .Select(site => new UniFiSiteViewDto(
                site.ShortId,
                site.Name,
                site.Description,
                site.Hidden,
                Missing: site.MissingSinceUtc is not null,
                site.UpdateDateTime))
            .ToList();
    }
}

public sealed class SaveUniFiSettingsCommandHandler(
    IUniFiSettingsRepository repository,
    ICredentialProtector protector)
    : IRequestHandler<SaveUniFiSettingsCommand, UniFiSettingsDto>
{
    public async Task<UniFiSettingsDto> Handle(
        SaveUniFiSettingsCommand request, CancellationToken cancellationToken)
    {
        var payload = request.Request;
        var existing = await repository.GetAsync(cancellationToken);

        // senha cifrada existente é preservada quando o payload não envia uma nova
        var encryptedPassword = string.IsNullOrEmpty(payload.Password)
            ? existing?.PasswordEncrypted
            : protector.Protect(payload.Password);

        if (string.IsNullOrEmpty(encryptedPassword))
            throw new ArgumentException(
                "A senha do controlador é obrigatória na primeira configuração.",
                nameof(payload));

        var setting = existing ?? UniFiIntegrationSetting.Create(
            payload.BaseUrl,
            payload.Username,
            encryptedPassword,
            payload.Site,
            payload.AllowInvalidTlsCertificate);

        if (existing is not null)
            existing.Update(
                payload.BaseUrl,
                payload.Username,
                encryptedPassword,
                payload.Site,
                payload.AllowInvalidTlsCertificate);

        await repository.SaveAsync(setting, cancellationToken);

        return new UniFiSettingsDto(
            setting.BaseUrl,
            setting.Username,
            setting.Site,
            setting.AllowInvalidTlsCertificate,
            PasswordSet: true,
            Source: "database",
            setting.UpdateDateTime);
    }
}

public sealed class TestUniFiConnectionCommandHandler(
    IUniFiOptionsResolver optionsResolver,
    IUnifiClientFactory clientFactory,
    ILogger<TestUniFiConnectionCommandHandler> logger)
    : IRequestHandler<TestUniFiConnectionCommand, UniFiConnectionTestResultDto>
{
    public async Task<UniFiConnectionTestResultDto> Handle(
        TestUniFiConnectionCommand request, CancellationToken cancellationToken)
    {
        var options = await optionsResolver.ResolveAsync(cancellationToken);
        if (!options.IsConfigured)
            return new UniFiConnectionTestResultDto(
                false, 0,
                "Configuração incompleta — informe URL, usuário e senha.");

        try
        {
            var client = clientFactory.Create(options);
            var devices = await client.ListDevicesAsync(cancellationToken);
            var version = await TryGetVersionAsync(client, cancellationToken);
            return new UniFiConnectionTestResultDto(true, devices.Count, null, version);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Teste de conexão UniFi falhou para {BaseUrl}", options.BaseUrl);
            return new UniFiConnectionTestResultDto(false, 0, ex.Message);
        }
    }

    /// <summary>Versão é opcional: falha nela não invalida o teste de conexão.</summary>
    private async Task<string?> TryGetVersionAsync(
        IUnifiApiClient client, CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetSystemVersionAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Versão do controller indisponível no teste");
            return null;
        }
    }
}
