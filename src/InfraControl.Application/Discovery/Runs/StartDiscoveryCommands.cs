using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Results.Ports;
using InfraControl.Application.Discovery.Runs.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Discovery.Runs;

// F4-T003: gatilho manual de discovery por device (POST — decisão do
// usuário junto com o primeiro provider). Ping falho = ACHADO NEGATIVO:
// run Complete + DiscoveryResult(Host, reachable=false) — só erro de
// infraestrutura falha o run. Escrita de achados futuros (SNMP, LLDP…)
// vem com os providers F4-T004+.

public sealed record StartDiscoveryCommand(Guid DeviceId, string Provider)
    : BaseCommand<DiscoveryRunDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.MonitoringManage.ToString()];

    public string AuditResource => "DiscoveryRun";
}

public sealed class StartDiscoveryCommandHandler(
    IDeviceRepository deviceRepository,
    IProviderRegistry<IDiscoveryProvider> providers,
    IDiscoveryRunRepository runRepository,
    IDiscoveryResultRepository resultRepository)
    : IRequestHandler<StartDiscoveryCommand, DiscoveryRunDto>
{
    public async Task<DiscoveryRunDto> Handle(
        StartDiscoveryCommand request,
        CancellationToken cancellationToken)
    {
        var device = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.DeviceId}' não encontrado.");

        var provider = ResolveProvider(request.Provider);

        if (string.IsNullOrWhiteSpace(device.Ip))
            throw new ArgumentException("Device sem IP cadastrado.", nameof(request.DeviceId));

        var run = DiscoveryRun.Start(device.Id);
        await runRepository.AddAsync(run, cancellationToken);

        IReadOnlyList<DiscoveryProviderResult> findings;
        try
        {
            findings = await provider.DiscoverAsync(device.Ip, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            run.Fail(Truncate(exception.Message));
            await runRepository.SaveChangesAsync(cancellationToken);
            return Map(run);
        }

        // F4-T010: casa achados com devices existentes (ip > mac > nome);
        // achado Succeeded sem match vira candidato (new_device=true).
        var matchCache = new Dictionary<string, Guid?>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var finding in findings)
        {
            var summary = finding.Summary
                ?? (finding.Succeeded
                    ? $"Discovery de {device.Ip} concluída."
                    : $"Discovery de {device.Ip} falhou: {finding.Error}");

            Guid? linkedDeviceId;
            IReadOnlyDictionary<string, string> values = finding.Attributes;

            if (finding.Kind == DiscoveryResultKind.Host)
            {
                linkedDeviceId = device.Id;
            }
            else if (!finding.Succeeded)
            {
                // Achado negativo ("Nenhuma entrada…") não é device candidato.
                linkedDeviceId = null;
            }
            else
            {
                linkedDeviceId = await MatchDeviceAsync(
                    finding.Attributes, matchCache, cancellationToken);
                if (linkedDeviceId is null)
                {
                    var flagged = new Dictionary<string, string>(finding.Attributes)
                    {
                        ["new_device"] = "true"
                    };
                    values = flagged;
                }
            }

            await resultRepository.AddAsync(
                DiscoveryResult.Create(
                    run.Id,
                    finding.Kind,
                    summary,
                    values,
                    deviceId: linkedDeviceId),
                cancellationToken);
        }

        run.Complete();
        await runRepository.SaveChangesAsync(cancellationToken);
        return Map(run);
    }

    /// <summary>Casa atributos do achado com device existente: ip → mac → nome
    /// (sys_name/device_id/hostname), prioridade na ordem citada (F4-T010).</summary>
    private async Task<Guid?> MatchDeviceAsync(
        IReadOnlyDictionary<string, string> attributes,
        Dictionary<string, Guid?> cache,
        CancellationToken cancellationToken)
    {
        if (attributes.TryGetValue("ip", out var ip)
            && !string.IsNullOrWhiteSpace(ip))
        {
            var byIp = await LookupAsync($"ip:{ip}",
                () => deviceRepository.GetByIpAsync(ip, cancellationToken));
            if (byIp is not null)
                return byIp;
        }

        if (attributes.TryGetValue("mac", out var mac)
            && !string.IsNullOrWhiteSpace(mac))
        {
            var byMac = await LookupAsync($"mac:{mac}",
                () => deviceRepository.GetByMacAsync(mac, cancellationToken));
            if (byMac is not null)
                return byMac;
        }

        foreach (var key in NameAttributeKeys)
        {
            if (!attributes.TryGetValue(key, out var name)
                || string.IsNullOrWhiteSpace(name))
                continue;

            var byName = await LookupAsync($"name:{name}",
                () => deviceRepository.GetByNameAsync(name, cancellationToken));
            if (byName is not null)
                return byName;
        }

        return null;

        async Task<Guid?> LookupAsync(string cacheKey, Func<Task<Device?>> query)
        {
            if (!cache.TryGetValue(cacheKey, out var cached))
            {
                var found = await query();
                cache[cacheKey] = cached = found?.Id;
            }

            return cached;
        }
    }

    private static readonly string[] NameAttributeKeys =
        ["sys_name", "device_id", "hostname"];

    private IDiscoveryProvider ResolveProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException(
                "O provider de discovery é obrigatório.", nameof(provider));

        var key = provider.Trim();
        try
        {
            // Registry lança InvalidOperation (→409) com msg em inglês;
            // para entrada do usuário o 400 em pt-BR é o contrato da API.
            return providers.Resolve(key);
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentException(
                $"Provider de discovery '{key}' não registrado.", nameof(provider));
        }
    }

    private static string Truncate(string message)
        => message.Length <= DiscoveryRun.ErrorMessageMaxLength
            ? message
            : message[..DiscoveryRun.ErrorMessageMaxLength];

    private static DiscoveryRunDto Map(DiscoveryRun run) => new(
        run.Id,
        run.DeviceId,
        run.Status.ToString(),
        run.StartedAt,
        run.FinishedAt,
        run.ErrorMessage);
}
