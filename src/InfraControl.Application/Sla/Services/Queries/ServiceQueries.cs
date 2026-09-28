using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.Services.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.Sla.Services.Queries;

// Sem cache (IQuery direto, não BaseQuery): SLA/SLO muda a cada mutation.

public sealed record SearchServicesQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ServiceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaRead.ToString()];
}

public sealed class SearchServicesQueryHandler(IServiceRepository repository)
    : IRequestHandler<SearchServicesQuery, PagedResult<ServiceDto>>
{
    public async Task<PagedResult<ServiceDto>> Handle(
        SearchServicesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(term, page, pageSize, cancellationToken);

        return new PagedResult<ServiceDto>(
            result.Items
                .Select(service => new ServiceDto(service.Id, service.Name, service.Description))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetServiceQuery(Guid Id) : IQuery<ServiceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaRead.ToString()];
}

public sealed class GetServiceQueryHandler(IServiceRepository repository)
    : IRequestHandler<GetServiceQuery, ServiceDto>
{
    public async Task<ServiceDto> Handle(
        GetServiceQuery request, CancellationToken cancellationToken)
    {
        var service = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Serviço '{request.Id}' não encontrado.");

        return new ServiceDto(service.Id, service.Name, service.Description);
    }
}
