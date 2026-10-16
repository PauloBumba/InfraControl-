using InfraControl.Application.Alerts;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Monitoring.Checks;

// MON-006/007 (docs/21): a execução de check vive no command — ADR-006
// (CQRS) determina que queries apenas leem. Erros nascem aqui/domínio em
// pt-BR (ArgumentException → 400, KeyNotFoundException → 404).

/// <summary>Resultado da execução de um check (MON-007).</summary>
public sealed record CheckRunResultDto(
    Guid DeviceId,
    string Name,
    string? Ip,
    string Check,
    bool Executed,
    bool Succeeded,
    long? DurationMs,
    string? Error,
    IReadOnlyDictionary<string, string>? Values);

/// <summary>Executa um check num device agora (MON-006).</summary>
public sealed record RunCheckCommand(Guid DeviceId, string Check)
    : BaseCommand<CheckRunResultDto>, IAuthorizedRequest
{
    // ADR-030 §3: mutação — mesma camada/policy HTTP (Operate); Viewer lê,
    // não executa check.
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

/// <summary>Executa um check em todos os devices (estilo Zabbix "check now").</summary>
public sealed record RunAllChecksCommand(string Check)
    : BaseCommand<IReadOnlyList<CheckRunResultDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class RunCheckCommandHandler(
    IDeviceRepository deviceRepository,
    IProviderRegistry<IMonitoringProvider> providers,
    IMonitoringResultRepository resultRepository,
    IAlertEvaluator alertEvaluator,
    ILogger<RunCheckCommandHandler> logger)
    : IRequestHandler<RunCheckCommand, CheckRunResultDto>
{
    public async Task<CheckRunResultDto> Handle(
        RunCheckCommand request, CancellationToken cancellationToken)
    {
        var device = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.DeviceId}' não encontrado.");

        var kind = ParseCheck(request.Check);
        var result = await ExecuteAsync(
            device, kind, providers, resultRepository, alertEvaluator,
            logger, cancellationToken);
        await resultRepository.SaveChangesAsync(cancellationToken);
        return result;
    }

    internal static CheckKind ParseCheck(string check)
    {
        if (string.IsNullOrWhiteSpace(check)
            || !Enum.TryParse(check.Trim(), ignoreCase: true, out CheckKind kind))
            throw new ArgumentException($"Check inválido: '{check}'.", nameof(check));

        return kind;
    }

    internal static async Task<CheckRunResultDto> ExecuteAsync(
        Domain.Entities.Device device,
        CheckKind kind,
        IProviderRegistry<IMonitoringProvider> providers,
        IMonitoringResultRepository resultRepository,
        IAlertEvaluator alertEvaluator,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var check = kind.ToString();

        if (kind is CheckKind.SnmpToner or CheckKind.SnmpDrum
                or CheckKind.SnmpColorToner or CheckKind.SnmpPrinterStatus
                or CheckKind.SnmpPrinterCounters
            && device.Type != DeviceType.Printer)
            throw new ArgumentException(
                $"Check '{check}' só se aplica a devices do tipo Printer.",
                nameof(check));

        // F30-T004 S2 (docs/52 §S2): fallback de rádio 802.11 é de Access
        // Point — mesmo molde do guard Printer acima.
        if (kind is CheckKind.SnmpRadio && device.Type != DeviceType.AccessPoint)
            throw new ArgumentException(
                $"Check '{check}' só se aplica a devices do tipo AccessPoint.",
                nameof(check));

        // F30-T003 (docs/44): saúde do servidor (HOST-RESOURCES-MIB) é de
        // Server — mesmo molde dos guards acima; reusado pelo polling.
        if (kind is CheckKind.SnmpServerCpu or CheckKind.SnmpServerMemory
                or CheckKind.SnmpServerDisk
            && device.Type != DeviceType.Server)
            throw new ArgumentException(
                $"Check '{check}' só se aplica a devices do tipo Server.",
                nameof(check));

        if (string.IsNullOrWhiteSpace(device.Ip))
        {
            // Sem IP não há tentativa — não grava histórico (F5-T002).
            logger.LogWarning(
                "Check {Check} pulado em {DeviceId} ({Name}): device sem IP.",
                check, device.Id, device.Name);

            return new CheckRunResultDto(
                device.Id, device.Name, device.Ip, check,
                Executed: false, Succeeded: false, DurationMs: null,
                Error: "Device sem IP cadastrado.", Values: null);
        }

        IMonitoringProvider provider;
        try
        {
            provider = providers.Resolve(check);
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentException($"Check não suportado: '{check}'.", nameof(check));
        }

        var result = await provider.CheckAsync(device.Ip, cancellationToken);
        var error = result.Succeeded
            ? null
            : result.Error ?? $"Check {check} falhou.";
        var durationMs = (long)result.Duration.TotalMilliseconds;

        var monitoringResult = MonitoringResult.Create(
            device.Id, kind, result.Succeeded, durationMs, error, result.Values);
        await resultRepository.AddAsync(monitoringResult, cancellationToken);

        logger.LogInformation(
            "Check {Check} em {DeviceId} ({Name}, {Ip}): {Succeeded} em {DurationMs}ms.",
            check, device.Id, device.Name, device.Ip, result.Succeeded, durationMs);

        // F6-T001: avaliação pós-check (alerta nasce/resolve no MESMO
        // SaveChanges do resultado). Falha do avaliador não derruba o check.
        try
        {
            await alertEvaluator.EvaluateAsync(
                device, kind, monitoringResult, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Falha ao avaliar alertas de {Check} em {DeviceId} ({Name}).",
                check, device.Id, device.Name);
        }

        return new CheckRunResultDto(
            device.Id, device.Name, device.Ip, check,
            Executed: true, result.Succeeded, durationMs, error, result.Values);
    }
}

public sealed class RunAllChecksCommandHandler(
    IDeviceRepository deviceRepository,
    IProviderRegistry<IMonitoringProvider> providers,
    IMonitoringResultRepository resultRepository,
    IAlertEvaluator alertEvaluator,
    ILogger<RunAllChecksCommandHandler> logger)
    : IRequestHandler<RunAllChecksCommand, IReadOnlyList<CheckRunResultDto>>
{
    public async Task<IReadOnlyList<CheckRunResultDto>> Handle(
        RunAllChecksCommand request, CancellationToken cancellationToken)
    {
        var kind = RunCheckCommandHandler.ParseCheck(request.Check);
        var devices = await deviceRepository.GetAllOrderedByNameAsync(cancellationToken);

        var results = new List<CheckRunResultDto>(devices.Count);
        foreach (var device in devices)
            results.Add(await RunCheckCommandHandler.ExecuteAsync(
                device, kind, providers, resultRepository, alertEvaluator,
                logger, cancellationToken));

        // Dia 5 (docs/20): um único SaveChanges para todos os devices —
        // N idas ao banco por execução viram 1 (o TransactionBehavior do
        // pipeline já garante atomicidade na API).
        await resultRepository.SaveChangesAsync(cancellationToken);

        return results;
    }
}
