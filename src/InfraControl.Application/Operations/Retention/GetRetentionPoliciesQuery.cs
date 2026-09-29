using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Operations.Retention.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace InfraControl.Application.Operations.Retention;

/// <summary>
/// Estado efetivo de uma política de retenção (F12-T002): <c>Days</c> é o
/// valor da policy quando <c>Source = "Policy"</c>, senão o default de
/// <c>Retention:*</c> (<c>Source = "Config"</c>).
/// </summary>
public sealed record RetentionPolicyDto(string DataType, int Days, string Source);

/// <summary>
/// Lista as políticas de retenção dos 7 tipos do cleanup; com
/// <c>DataType</c> informado, devolve só esse tipo (sempre existe — o
/// default vem da config quando não há policy).
/// </summary>
public sealed record GetRetentionPoliciesQuery(RetentionDataType? DataType = null)
    : IQuery<IReadOnlyList<RetentionPolicyDto>>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "RetentionPolicy";
}

public sealed class GetRetentionPoliciesQueryHandler(
    IRetentionPolicyRepository repository,
    IOptions<RetentionOptions> options)
    : IRequestHandler<GetRetentionPoliciesQuery, IReadOnlyList<RetentionPolicyDto>>
{
    public async Task<IReadOnlyList<RetentionPolicyDto>> Handle(
        GetRetentionPoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await repository.GetAllAsync(cancellationToken);
        var byType = policies.ToDictionary(p => p.DataType, p => p.Days);
        var defaults = options.Value;

        var dtos = Enum.GetValues<RetentionDataType>()
            .Select(dataType => new RetentionPolicyDto(
                dataType.ToString(),
                byType.TryGetValue(dataType, out var days)
                    ? days
                    : ConfigDays(defaults, dataType),
                byType.ContainsKey(dataType) ? "Policy" : "Config"))
            .Where(dto => request.DataType is null || dto.DataType == request.DataType.Value.ToString())
            .ToArray();

        return dtos;
    }

    private static int ConfigDays(RetentionOptions options, RetentionDataType dataType) => dataType switch
    {
        RetentionDataType.OutboxMessages => options.OutboxProcessedDays,
        RetentionDataType.ProcessedMessages => options.ProcessedMessagesDays,
        RetentionDataType.AuditEntries => options.AuditEntriesDays,
        RetentionDataType.MonitoringResults => options.MonitoringResultsDays,
        RetentionDataType.Notifications => options.NotificationsDays,
        RetentionDataType.RemoteSessions => options.RemoteSessionsDays,
        RetentionDataType.DiscoveryRuns => options.DiscoveryRunsDays,
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Tipo de dado de retenção inválido.")
    };
}
