using MediatR;

namespace InfraControl.BuildingBlocks.Application;

public interface ICommand<out TResponse> : IRequest<TResponse>;
public interface ICommand : IRequest;
public interface IQuery<out TResponse> : IRequest<TResponse>;

public interface IAuthorizedRequest
{
    IReadOnlyCollection<string> RequiredPermissions { get; }
}

public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; }
}
