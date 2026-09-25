using InfraControl.Application.Common.Interfaces;

namespace InfraControl.Api.RateLimiting;

/// <summary>
/// Marca um endpoint com a categoria do rate limit HTTP (ADR-011). Sem a
/// marcação, o endpoint usa a categoria Default.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HttpRateLimitAttribute(RateLimitCategory category) : Attribute
{
    public RateLimitCategory Category { get; } = category;
}
