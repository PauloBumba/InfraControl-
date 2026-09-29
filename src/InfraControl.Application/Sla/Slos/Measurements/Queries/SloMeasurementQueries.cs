using InfraControl.Application.Sla.Slos.Measurements.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Sla.Slos.Measurements.Queries;

// Sem cache (IQuery direto): medições mudam a cada registro.

public sealed record SearchSloMeasurementsQuery(
    Guid SloId,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<SloMeasurementDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchSloMeasurementsQueryHandler(ISloMeasurementRepository repository)
    : IRequestHandler<SearchSloMeasurementsQuery, PagedResult<SloMeasurementDto>>
{
    public async Task<PagedResult<SloMeasurementDto>> Handle(
        SearchSloMeasurementsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await repository.SearchBySloIdAsync(
            request.SloId, page, pageSize, cancellationToken);

        return new PagedResult<SloMeasurementDto>(
            result.Items
                .Select(m => new SloMeasurementDto(m.Id, m.SloId, m.MeasuredAt, m.ValuePercent))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetSloMeasurementQuery(Guid Id)
    : IQuery<SloMeasurementDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetSloMeasurementQueryHandler(ISloMeasurementRepository repository)
    : IRequestHandler<GetSloMeasurementQuery, SloMeasurementDto>
{
    public async Task<SloMeasurementDto> Handle(
        GetSloMeasurementQuery request, CancellationToken cancellationToken)
    {
        var measurement = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Medição de SLO '{request.Id}' não encontrada.");

        return new SloMeasurementDto(
            measurement.Id, measurement.SloId,
            measurement.MeasuredAt, measurement.ValuePercent);
    }
}
