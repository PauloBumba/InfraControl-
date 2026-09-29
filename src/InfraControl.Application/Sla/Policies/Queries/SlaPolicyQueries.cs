using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.Policies.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Sla.Policies.Queries;

// Sem cache (IQuery direto, não BaseQuery): SLA/SLO muda a cada mutation.

public sealed record SearchSlaPoliciesQuery(
    string? Term = null,
    Guid? ServiceId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<SlaPolicyDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchSlaPoliciesQueryHandler(ISlaPolicyRepository repository)
    : IRequestHandler<SearchSlaPoliciesQuery, PagedResult<SlaPolicyDto>>
{
    public async Task<PagedResult<SlaPolicyDto>> Handle(
        SearchSlaPoliciesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(
            term, request.ServiceId, page, pageSize, cancellationToken);

        return new PagedResult<SlaPolicyDto>(
            result.Items
                .Select(policy => new SlaPolicyDto(
                    policy.Id, policy.Name,
                    policy.ResponseMinutes, policy.ResolutionMinutes, policy.ServiceId))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetSlaPolicyQuery(Guid Id) : IQuery<SlaPolicyDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetSlaPolicyQueryHandler(ISlaPolicyRepository repository)
    : IRequestHandler<GetSlaPolicyQuery, SlaPolicyDto>
{
    public async Task<SlaPolicyDto> Handle(
        GetSlaPolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Política de SLA '{request.Id}' não encontrada.");

        return new SlaPolicyDto(
            policy.Id, policy.Name,
            policy.ResponseMinutes, policy.ResolutionMinutes, policy.ServiceId);
    }
}
