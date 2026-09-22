using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InfraControl.BuildingBlocks.Application;

public static class CacheKeyBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Build(string requestTypeName, object request, ICurrentUserContext user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestTypeName);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);

        var userId = user.UserId?.ToString("N") ?? "anon";
        var roles = user.Roles.Count == 0
            ? "none"
            : string.Join(',', user.Roles.Order(StringComparer.OrdinalIgnoreCase));
        var paramsHash = HashParams(request);
        return $"q:{userId}:{roles}:{requestTypeName}:{paramsHash}";
    }

    public static string TagIndexKey(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return $"tagidx:{tag}";
    }

    public static string HashParams(object request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = JsonSerializer.Serialize(request, request.GetType(), JsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
