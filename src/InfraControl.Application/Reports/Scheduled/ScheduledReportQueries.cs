using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Reports.Scheduled.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Reports.Scheduled;

// Relatório geral agendado por e-mail — somente "General" hoje.
// Admin define destinatários + periodicidade; o worker envia a TODOS.

public sealed record ScheduledReportDto(
    Guid? Id,
    string Kind,
    IReadOnlyList<string> Recipients,
    string Frequency,
    string? SendTimeUtc,
    int? DayOfWeek,
    bool IsActive,
    DateTime? LastSentAtUtc,
    DateTime? NextRunAtUtc,
    string? LastError);

public sealed record ScheduledReportUpsertInput(
    IReadOnlyList<string> Recipients,
    string Frequency,
    string SendTimeUtc,
    int? DayOfWeek,
    bool IsActive);

public static class ScheduledReportMapping
{
    public static ScheduledReportDto Map(ScheduledReport? report) => report is null
        ? new(null, ScheduledReport.GeneralKind, [], "Daily", null, null,
            false, null, null, null)
        : new(
            report.Id,
            report.Kind,
            report.Recipients,
            report.Frequency.ToString(),
            report.SendTimeUtc.ToString("HH:mm"),
            report.DayOfWeek,
            report.IsActive,
            report.LastSentAtUtc,
            report.NextRunAtUtc,
            report.LastError);

    public static (ReportFrequency Frequency, TimeOnly Time, int? DayOfWeek) Parse(
        ScheduledReportUpsertInput input)
    {
        var frequency = input.Frequency?.Trim() switch
        {
            "Daily" => ReportFrequency.Daily,
            "Weekly" => ReportFrequency.Weekly,
            var other => throw new ArgumentException(
                $"Frequência inválida: '{other}'. Use Daily ou Weekly.",
                nameof(input)),
        };

        if (!TimeOnly.TryParseExact(
                input.SendTimeUtc, "HH:mm", out var time))
            throw new ArgumentException(
                "Horário inválido — use HH:mm (UTC).", nameof(input));

        return (frequency, time, input.DayOfWeek);
    }
}

public sealed record GetScheduledReportQuery()
    : IQuery<ScheduledReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetScheduledReportQueryHandler(IScheduledReportRepository repository)
    : IRequestHandler<GetScheduledReportQuery, ScheduledReportDto>
{
    public async Task<ScheduledReportDto> Handle(
        GetScheduledReportQuery request, CancellationToken cancellationToken)
        => ScheduledReportMapping.Map(
            await repository.GetByKindAsync(
                ScheduledReport.GeneralKind, cancellationToken));
}

/// <summary>
/// Upsert da agenda "General" — cria na primeira vez, atualiza depois.
/// Administer apenas.
/// </summary>
public sealed record UpsertScheduledReportCommand(ScheduledReportUpsertInput Input)
    : BaseCommand<ScheduledReportDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;

    public string AuditResource => "ScheduledReport";
}

public sealed class UpsertScheduledReportCommandHandler(
    IScheduledReportRepository repository)
    : IRequestHandler<UpsertScheduledReportCommand, ScheduledReportDto>
{
    public async Task<ScheduledReportDto> Handle(
        UpsertScheduledReportCommand request, CancellationToken cancellationToken)
    {
        var (frequency, time, dayOfWeek) =
            ScheduledReportMapping.Parse(request.Input);

        var report = await repository.GetByKindAsync(
            ScheduledReport.GeneralKind, cancellationToken);

        if (report is null)
        {
            report = ScheduledReport.Create(
                request.Input.Recipients, frequency, time, dayOfWeek,
                request.Input.IsActive);
            await repository.AddAsync(report, cancellationToken);
        }
        else
        {
            report.Update(
                request.Input.Recipients, frequency, time, dayOfWeek,
                request.Input.IsActive);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return ScheduledReportMapping.Map(report);
    }
}
