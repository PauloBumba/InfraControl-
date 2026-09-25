using MediatR;

namespace InfraControl.BuildingBlocks.Application;

public interface ICommand<out TResponse> : IRequest<TResponse>;
public interface ICommand : IRequest;
public interface IQuery<out TResponse> : IRequest<TResponse>;

public interface IAuthorizedRequest
{
    IReadOnlyCollection<string> RequiredPermissions { get; }
}

/// <summary>
/// Declara o resource explícito da entrada de audit deste request
/// (AuditBehavior); requests sem a interface continuam como
/// "ApplicationRequest".
/// </summary>
public interface IAuditableRequest
{
    string AuditResource { get; }
}

public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; }
}
