using System.Security.Claims;
using InfraControl.BuildingBlocks.Application;

namespace InfraControl.Api.Services;

public sealed class HttpCurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid? UserId => Guid.TryParse(
        Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal.FindFirstValue("sub"),
        out var value) ? value : null;

    public string? UserName => Principal.Identity?.Name;
    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public IReadOnlyCollection<string> Roles => Principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
    public IReadOnlyCollection<string> Permissions => Principal.FindAll("permission").Select(claim => claim.Value).ToArray();
}
