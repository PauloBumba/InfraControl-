using System.Diagnostics;
using InfraControl.Application.Integrations.Email.Ports;
using InfraControl.Application.Reports.Scheduled;
using InfraControl.Application.Reports.Scheduled.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers;

/// <summary>
/// Relatório geral agendado por e-mail: varre agendas ativas vencidas
/// (NextRunAtUtc <= agora) a cada minuto e envia o relatório a TODOS os
/// destinatários configurados pelo admin. Falha de envio marca a agenda
/// com erro e retry curto (15min) — SMTP fora não silencia o relatório.
/// Handler/builder chamados DIRETAMENTE (sem pipeline MediatR): workers
/// não têm sessão HTTP; leitura pura não exige autorização.
/// </summary>
public sealed class ScheduledReportWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledReportWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(5);

    private int _running;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scheduled report worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
                continue;

            try
            {
                using var activity = WorkerTracing.Source.StartActivity(
                    "worker scheduled-report", ActivityKind.Internal);
                await RunOnceAsync(stoppingToken);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(RunTimeout);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider
                .GetRequiredService<IScheduledReportRepository>();
            var builder = scope.ServiceProvider
                .GetRequiredService<GeneralReportBuilder>();
            var sender = scope.ServiceProvider
                .GetRequiredService<IReportEmailSender>();

            var due = await repository.ListDueAsync(
                DateTime.UtcNow, timeout.Token);

            foreach (var report in due)
            {
                timeout.Token.ThrowIfCancellationRequested();
                await SendReportAsync(report, builder, sender, repository, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("Scheduled report cancelled by timeout after {Timeout}.", RunTimeout);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduled report cycle failed.");
        }
    }

    private async Task SendReportAsync(
        ScheduledReport report,
        GeneralReportBuilder builder,
        IReportEmailSender sender,
        IScheduledReportRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = report.Kind == ScheduledReport.GeneralKind
                ? await builder.BuildAsync(cancellationToken)
                : throw new InvalidOperationException(
                    $"Tipo de relatório não suportado: '{report.Kind}'.");

            var (subject, html, text) =
                GeneralReportBuilder.ComposeEmail(content);

            foreach (var recipient in report.Recipients)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await sender.SendAsync(
                    recipient, subject, html, text, cancellationToken);
            }

            report.MarkSent(DateTime.UtcNow);
            await repository.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Relatório '{Kind}' enviado para {Count} destinatário(s) " +
                "(próximo: {NextRun}).",
                report.Kind, report.Recipients.Count, report.NextRunAtUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao enviar relatório '{Kind}' — retry em 15min.",
                report.Kind);
            report.MarkFailed(DateTime.UtcNow, exception.Message);
            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}
