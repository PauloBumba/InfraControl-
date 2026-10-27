using InfraControl.Application.Reports.Modules.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Reports.Modules.Queries;

/// <summary>
/// F25-T004 §9 — relatório de Tasks por período (Operate).
/// </summary>
public sealed record GetTasksReportQuery(DateOnly? From = null, DateOnly? To = null)
    : BaseQuery<TasksReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetTasksReportQueryHandler(IModuleReportsRepository repository)
    : IRequestHandler<GetTasksReportQuery, TasksReportDto>
{
    public Task<TasksReportDto> Handle(
        GetTasksReportQuery request, CancellationToken cancellationToken)
        => repository.GetTasksReportAsync(request.From, request.To, cancellationToken);
}

/// <summary>
/// F25-T004 §10 — relatório de PDI por período (Operate).
/// </summary>
public sealed record GetPdiReportQuery(DateOnly? From = null, DateOnly? To = null)
    : BaseQuery<PdiReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetPdiReportQueryHandler(IModuleReportsRepository repository)
    : IRequestHandler<GetPdiReportQuery, PdiReportDto>
{
    public Task<PdiReportDto> Handle(
        GetPdiReportQuery request, CancellationToken cancellationToken)
        => repository.GetPdiReportAsync(request.From, request.To, cancellationToken);
}

/// <summary>
/// F25-T004 §11 — relatório de Knowledge por período (Operate).
/// </summary>
public sealed record GetKnowledgeReportQuery(DateOnly? From = null, DateOnly? To = null)
    : BaseQuery<KnowledgeReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetKnowledgeReportQueryHandler(IModuleReportsRepository repository)
    : IRequestHandler<GetKnowledgeReportQuery, KnowledgeReportDto>
{
    public Task<KnowledgeReportDto> Handle(
        GetKnowledgeReportQuery request, CancellationToken cancellationToken)
        => repository.GetKnowledgeReportAsync(request.From, request.To, cancellationToken);
}

/// <summary>
/// F25-T004 §12 — relatório gerencial do período (Operate).
/// </summary>
public sealed record GetGerencialReportQuery(DateOnly? From = null, DateOnly? To = null)
    : BaseQuery<GerencialReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetGerencialReportQueryHandler(IModuleReportsRepository repository)
    : IRequestHandler<GetGerencialReportQuery, GerencialReportDto>
{
    public Task<GerencialReportDto> Handle(
        GetGerencialReportQuery request, CancellationToken cancellationToken)
        => repository.GetGerencialReportAsync(request.From, request.To, cancellationToken);
}
