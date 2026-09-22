using System.Text.Json.Serialization;

namespace InfraControl.BuildingBlocks.Application.Response;

public interface IApiResponseMarker
{
    bool IsSuccess { get; }
}

public sealed class ApiResponse<T> : IApiResponseMarker
{
    private ApiResponse() { }

    [JsonConstructor]
    private ApiResponse(bool isSuccess, T? data, IReadOnlyList<string>? errors)
    {
        IsSuccess = isSuccess;
        Data = data;
        Errors = errors ?? [];
    }

    public bool IsSuccess { get; private init; }
    public T? Data { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static ApiResponse<T> Ok(T data) => new() { IsSuccess = true, Data = data };
    public static ApiResponse<T> Ok() => new() { IsSuccess = true };
    public static ApiResponse<T> Fail(IEnumerable<string> errors)
    {
        var validErrors = errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToArray();
        if (validErrors.Length == 0)
            throw new ArgumentException("A failed response must contain at least one error.", nameof(errors));

        return new() { IsSuccess = false, Errors = validErrors };
    }
}
