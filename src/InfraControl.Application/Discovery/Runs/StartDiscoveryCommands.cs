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

        foreach (var finding in findings)
        {
            var summary = finding.Summary
                ?? (finding.Succeeded
                    ? $"Discovery de {device.Ip} concluída."
                    : $"Discovery de {device.Ip} falhou: {finding.Error}");

            // Só achados do próprio device (Host) carregam o vínculo;
            // vizinhos/ARP/MAC/leases ficam sem link até o T010 casar.
            await resultRepository.AddAsync(
                DiscoveryResult.Create(
                    run.Id,
                    finding.Kind,
                    summary,
                    finding.Attributes,
                    deviceId: finding.Kind == DiscoveryResultKind.Host ? device.Id : null),
                cancellationToken);
        }

        run.Complete();
        await runRepository.SaveChangesAsync(cancellationToken);
        return Map(run);
    }

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
