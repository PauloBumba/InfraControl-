using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.Common.Audit.Queries;

public sealed record SearchAuditEntriesQuery(
    string? Action = null,
    string? Outcome = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20) : BaseQuery<PagedResult<AuditEntrySummary>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(1);

    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.AuditRead.ToString()];
}

public sealed class SearchAuditEntriesQueryHandler(IAuditReader reader)
    : IRequestHandler<SearchAuditEntriesQuery, PagedResult<AuditEntrySummary>>
{
    public Task<PagedResult<AuditEntrySummary>> Handle(
        SearchAuditEntriesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        return reader.SearchAsync(
            request.Action?.Trim(),
            request.Outcome?.Trim(),
            request.From,
            request.To,
            page,
            pageSize,
            cancellationToken);
    }
}
