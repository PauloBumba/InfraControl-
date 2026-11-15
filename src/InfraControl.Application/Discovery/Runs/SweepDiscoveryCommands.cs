using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Runs.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Discovery.Runs;

// Network Explorer F4 — varredura completa de um device: executa TODOS os
// providers registrados (ICMP → SNMP → LLDP → CDP → ARP → DHCP → MAC) em
// sequência, cada um gerando seu próprio DiscoveryRun (mesma trilha de
// auditoria do gatilho manual). O handler de StartDiscoveryCommand é
// chamado DIRETAMENTE (mesmo padrão do UnifiSyncWorker): o pipeline
// AuthorizationBehavior exige sessão HTTP e workers/agendamentos não têm.
// Falha de um provider não aborta os demais — cada run registra seu
// próprio status (Complete/Fail) com ErrorMessage.

/// <summary>Ordem canônica dos providers de varredura (F4).</summary>
public static class DiscoverySweepProviders
{
    public static readonly IReadOnlyList<string> Keys =
        ["Icmp", "Snmp", "Lldp", "Cdp", "Arp", "Dhcp", "Mac"];
}

public sealed record RunDiscoverySweepCommand(Guid DeviceId)
    : BaseCommand<IReadOnlyList<DiscoveryRunDto>>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;

    public string AuditResource => "DiscoveryRun";
}

public sealed class RunDiscoverySweepCommandHandler(
    IDeviceRepository deviceRepository,
    IProviderRegistry<IDiscoveryProvider> providers,
    IRequestHandler<StartDiscoveryCommand, DiscoveryRunDto> startHandler,
    ILogger<RunDiscoverySweepCommandHandler> logger)
    : IRequestHandler<RunDiscoverySweepCommand, IReadOnlyList<DiscoveryRunDto>>
{
    public async Task<IReadOnlyList<DiscoveryRunDto>> Handle(
        RunDiscoverySweepCommand request,
        CancellationToken cancellationToken)
    {
        var device = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.DeviceId}' não encontrado.");

        var runs = new List<DiscoveryRunDto>();
        foreach (var key in DiscoverySweepProviders.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var run = await startHandler.Handle(
                    new StartDiscoveryCommand(device.Id, key), cancellationToken);
                runs.Add(run);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Provider sem suporte ao device (ex.: SNMP sem credencial) —
                // registra e segue para o próximo; a varredura não morre.
                logger.LogWarning(
                    exception,
                    "Discovery sweep: provider {Provider} falhou para device {DeviceId}.",
                    key, device.Id);
            }
        }

        return runs;
    }
}
