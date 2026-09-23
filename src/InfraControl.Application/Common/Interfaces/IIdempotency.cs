namespace InfraControl.Application.Common.Interfaces;

public interface IIdempotentRequest
{
    string IdempotencyKey { get; }
    TimeSpan? IdempotencyDuration => TimeSpan.FromMinutes(5);
}

public interface IIdempotencyStore
{
    Task<bool> TryAcquireAsync(string key, TimeSpan duration, CancellationToken cancellationToken = default);
}

public interface IValidatableRequest
{
    IReadOnlyCollection<string> Validate();
}

public sealed class DuplicateRequestException(string key)
    : InvalidOperationException($"A requisição com chave de idempotência '{key}' já foi aceita.");

public sealed class RequestValidationException(IReadOnlyCollection<string> errors)
    : InvalidOperationException($"Falha na validação da requisição: {string.Join("; ", errors)}")
{
    public IReadOnlyCollection<string> Errors { get; } = errors;
}
