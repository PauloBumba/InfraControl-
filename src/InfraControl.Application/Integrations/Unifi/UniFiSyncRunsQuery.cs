using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using MediatR;

namespace InfraControl.Application.Integrations.Unifi;

/// <summary>F26 Fase 9.3: histórico de sincronizações (9.1/9.4).</summary>
public sealed record UniFiSyncRunDto(
    Guid Id,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    bool Succeeded,
    int Created,
    int Updated,
    int References,
    int Errors,
    int Sites,
    string? Message)
{
    public TimeSpan Duration => FinishedAtUtc - StartedAtUtc;
}

public sealed record ListUniFiSyncRunsQuery(
    int Limit = 50, bool? Succeeded = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null)
    : IQuery<IReadOnlyList<UniFiSyncRunDto>>
{
}

public sealed class ListUniFiSyncRunsQueryHandler(
    IUniFiSyncRunsRepository syncRunsRepository)
    : IRequestHandler<ListUniFiSyncRunsQuery, IReadOnlyList<UniFiSyncRunDto>>
{
    public async Task<IReadOnlyList<UniFiSyncRunDto>> Handle(
        ListUniFiSyncRunsQuery request, CancellationToken cancellationToken)
    {
        var runs = await syncRunsRepository.ListRecentAsync(
            request.Limit, request.Succeeded, request.FromUtc, request.ToUtc,
            cancellationToken);
        return runs.Select(run => new UniFiSyncRunDto(
            run.Id, run.StartedAtUtc, run.FinishedAtUtc, run.Succeeded,
            run.Created, run.Updated, run.References, run.Errors, run.Sites,
            UniFiSyncRun.Sanitize(run.Message))).ToList();
    }
}
