using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Monitoring.PrinterSupplies;

/// <summary>Nível de um suprimento (toner/drum) em percentual.</summary>
public sealed record PrinterSupplyDto(
    string Check,
    bool Succeeded,
    int? LevelPercent,
    string? Description,
    string? Error);

/// <summary>Suprimentos de uma impressora (F5-T004), sob demanda.</summary>
public sealed record PrinterSuppliesDto(
    Guid DeviceId,
    string Name,
    string? Ip,
    IReadOnlyList<PrinterSupplyDto> Supplies);

/// <summary>Toner + drum de uma impressora via SNMP Printer MIB (F5-T004).</summary>
public sealed record GetPrinterSuppliesQuery(Guid Id)
    : IQuery<PrinterSuppliesDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.MonitoringRead.ToString()];
}

public sealed class GetPrinterSuppliesQueryHandler(
    IDeviceRepository repository,
    IProviderRegistry<IMonitoringProvider> providers,
    IMonitoringResultRepository resultRepository)
    : IRequestHandler<GetPrinterSuppliesQuery, PrinterSuppliesDto>
{
    private static readonly string[] SupplyKeys =
        [CheckKind.SnmpToner.ToString(), CheckKind.SnmpDrum.ToString()];

    public async Task<PrinterSuppliesDto> Handle(
        GetPrinterSuppliesQuery request,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.Id}' não encontrado.");

        if (device.Type != DeviceType.Printer)
            throw new ArgumentException(
                "O check de suprimentos só se aplica a devices do tipo Printer.",
                nameof(request.Id));

        if (string.IsNullOrWhiteSpace(device.Ip))
            return new PrinterSuppliesDto(
                device.Id,
                device.Name,
                device.Ip,
                SupplyKeys.Select(key => new PrinterSupplyDto(
                    key, false, null, null, "Device sem IP cadastrado.")).ToArray());

        var supplies = new List<PrinterSupplyDto>(SupplyKeys.Length);
        foreach (var key in SupplyKeys)
        {
            var provider = providers.Resolve(key);
            var result = await provider.CheckAsync(device.Ip, cancellationToken);

            // F5-T002: cada tentativa (toner/drum) vira uma linha no histórico.
            if (Enum.TryParse<CheckKind>(key, out var kind))
            {
                await resultRepository.AddAsync(
                    MonitoringResult.Create(
                        device.Id,
                        kind,
                        result.Succeeded,
                        (long)result.Duration.TotalMilliseconds,
                        result.Error,
                        result.Values),
                    cancellationToken);
            }

            int? levelPercent = null;
            string? description = null;
            if (result.Succeeded)
            {
                if (result.Values is not null)
                {
                    if (result.Values.TryGetValue("levelPercent", out var levelText))
                        levelPercent = int.TryParse(levelText, out var level) ? level : null;
                    if (result.Values.TryGetValue("description", out var desc))
                        description = desc;
                }

                if (levelPercent is null)
                {
                    supplies.Add(new PrinterSupplyDto(
                        key, false, null, null,
                        "Resposta SNMP sem nível de suprimento."));
                    continue;
                }
            }

            supplies.Add(new PrinterSupplyDto(
                key,
                result.Succeeded,
                levelPercent,
                description,
                result.Succeeded ? null : result.Error ?? "Falha ao consultar o suprimento."));
        }

        await resultRepository.SaveChangesAsync(cancellationToken);

        return new PrinterSuppliesDto(device.Id, device.Name, device.Ip, supplies);
    }
}
