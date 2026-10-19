using MediatR;

namespace InfraControl.BuildingBlocks.Application;

public interface ICommand<out TResponse> : IRequest<TResponse>;
public interface ICommand : IRequest;
public interface IQuery<out TResponse> : IRequest<TResponse>;

public interface IAuthorizedRequest
{
    IReadOnlyCollection<string> RequiredRoles { get; }
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

    /// <summary>
    /// Tags de invalidação do CQRS (F1 / docs/08): a resposta cacheada é
    /// indexada sob <c>tagidx:{tag}</c> (CacheKeyBuilder.TagIndexKey) e um
    /// comando com <c>CacheKeysToInvalidate</c> igual remove a entrada de
    /// todas as queries que declararam a tag. A chave construída pelo
    /// CachingBehavior leva usuário/roles/hash e nunca pode ser deduzida a
    /// partir do lado da mutation — a tag é o único elo que fecha.
    /// </summary>
    IReadOnlyCollection<string> CacheTags { get; }
}
