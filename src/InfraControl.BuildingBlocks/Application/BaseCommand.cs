namespace InfraControl.BuildingBlocks.Application;

/// <summary>
/// Base para comandos com resposta. Mantém a invalidação de cache declarativa e opt-in.
/// </summary>
public abstract record BaseCommand<TResponse> : ICommand<TResponse>, ICacheInvalidatingCommand
{
    public virtual IReadOnlyCollection<string> CacheKeysToInvalidate => [];
}

/// <summary>
/// Base para comandos sem resposta.
/// </summary>
public abstract record BaseCommand : ICommand, ICacheInvalidatingCommand
{
    public virtual IReadOnlyCollection<string> CacheKeysToInvalidate => [];
}

public interface ICacheInvalidatingCommand
{
    IReadOnlyCollection<string> CacheKeysToInvalidate { get; }
}
