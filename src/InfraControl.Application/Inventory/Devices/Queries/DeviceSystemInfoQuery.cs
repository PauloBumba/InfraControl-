using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Inventory.Devices.Queries;

// F30-T001 (docs/42 §A L63-70): identificação do switch lida do HISTÓRICO de
// polls. Firmware/Uptime não têm coluna em Device e o MonitoringResult já
// guarda sysDescr/uptimeSeconds por poll — a leitura sai de lá, sem migração
// (decisão 1 da §7 do plano docs/51).
// REGRA: sem histórico os campos são null (nunca 0, nunca string vazia) e o
// carimbo é o do poll que forneceu o dado, não o de agora. DeviceDto não foi
// tocado de propósito: ToDto(device) é estático e não tem acesso ao
// histórico; enfiar campos de runtime lá mudaria todo chamador.

/// <summary>Identificação/runtime de um device, segundo o último poll bom.</summary>
public sealed record DeviceSystemInfoDto(
    long? UptimeSeconds,
    string? SysDescr,
    DateTime? CapturedAtUtc);

public sealed record GetDeviceSystemInfoQuery(Guid Id)
    : BaseQuery<DeviceSystemInfoDto>, IAuthorizedRequest
{
    // 30s como o status consolidado: uptime muda a cada poll e o cache existe
    // para não varrer a tabela de histórico a cada render.
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetDeviceSystemInfoQueryHandler(
    IDeviceRepository deviceRepository,
    IMonitoringResultRepository resultRepository)
    : IRequestHandler<GetDeviceSystemInfoQuery, DeviceSystemInfoDto>
{
    private const int RecentPolls = 5;

    public async Task<DeviceSystemInfoDto> Handle(
        GetDeviceSystemInfoQuery request,
        CancellationToken cancellationToken)
    {
        _ = await deviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.Id}' não encontrado.");

        var polls = await resultRepository.GetRecentByDeviceAndKindAsync(
            request.Id, CheckKind.SnmpSystem, RecentPolls, cancellationToken);

        // O poll mais recente pode ter falhado (SNMP fora do ar): o último
        // dado bom vale mais que null — desde que o carimbo diga de quando é.
        var poll = polls.FirstOrDefault(candidate => candidate.Succeeded);
        if (poll is null || poll.Values is null)
            return new DeviceSystemInfoDto(null, null, null);

        long? uptime = null;
        if (poll.Values.TryGetValue("uptimeSeconds", out var raw)
            && long.TryParse(raw, out var seconds)
            && seconds >= 0)
        {
            uptime = seconds;
        }

        string? sysDescr = null;
        if (poll.Values.TryGetValue("sysDescr", out var description)
            && !string.IsNullOrWhiteSpace(description))
        {
            sysDescr = description.Trim();
        }

        return new DeviceSystemInfoDto(uptime, sysDescr, poll.CreateDateTime);
    }
}
