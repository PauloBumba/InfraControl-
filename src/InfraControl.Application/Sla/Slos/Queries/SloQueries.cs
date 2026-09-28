using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.Slos.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.Sla.Slos.Queries;

// Sem cache (IQuery direto): SLO muda a cada mutation.

public sealed record SearchSlosQuery(
    string? Term = null,
    Guid? ServiceId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<SloDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaRead.ToString()];
}

public sealed class SearchSlosQueryHandler(ISloRepository repository)
    : IRequestHandler<SearchSlosQuery, PagedResult<SloDto>>
{
    public async Task<PagedResult<SloDto>> Handle(
        SearchSlosQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(
            term, request.ServiceId, page, pageSize, cancellationToken);

        return new PagedResult<SloDto>(
            result.Items
                .Select(slo => new SloDto(
                    slo.Id, slo.Name, slo.TargetPercent, slo.WindowDays, slo.ServiceId))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetSloQuery(Guid Id) : IQuery<SloDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaRead.ToString()];
}

public sealed class GetSloQueryHandler(ISloRepository repository)
    : IRequestHandler<GetSloQuery, SloDto>
{
    public async Task<SloDto> Handle(GetSloQuery request, CancellationToken cancellationToken)
    {
        var slo = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"SLO '{request.Id}' não encontrado.");

        return new SloDto(slo.Id, slo.Name, slo.TargetPercent, slo.WindowDays, slo.ServiceId);
    }
}
