using InfraControl.Application.Reports.Modules;
using InfraControl.Application.Reports.Modules.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Reports.Automatics.Queries;

/// <summary>
/// F25-T006 §15 — relatório gerencial gerado automaticamente pela cadência
/// (mensal/trimestral/semestral/anual) sobre a janela do período corrente.
/// Reusa o relatório §12; o agendamento em background fica para ADR.
/// </summary>
public sealed record GetAutomaticReportQuery(string Cadence = "mensal")
    : BaseQuery<AutomaticReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed record AutomaticReportDto(
    string Cadence,
    DateOnly From,
    DateOnly To,
    GerencialReportDto Report);

public sealed class GetAutomaticReportQueryHandler(
    IModuleReportsRepository repository)
    : IRequestHandler<GetAutomaticReportQuery, AutomaticReportDto>
{
    public async Task<AutomaticReportDto> Handle(
        GetAutomaticReportQuery request,
        CancellationToken cancellationToken)
    {
        var cadence = ReportCadenceCalculator.Parse(request.Cadence);
        var today = DateOnly.FromDateTime(
            InfraControl.Domain.Common.AppTimeProvider.Current
                .GetUtcNow().UtcDateTime.Date);
        var (from, to) = ReportCadenceCalculator.Window(cadence, today);
        var report = await repository.GetGerencialReportAsync(
            from, to, cancellationToken);

        return new AutomaticReportDto(
            cadence.ToString().ToLowerInvariant(), from, to, report);
    }
}
