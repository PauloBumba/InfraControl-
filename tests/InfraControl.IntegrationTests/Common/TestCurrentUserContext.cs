using InfraControl.BuildingBlocks.Application;

namespace InfraControl.IntegrationTests.Common;

/// <summary>
/// Usuário de teste controlável — as permissões vêm de RolePermissions
/// (role → Permission → claim string), o mesmo contrato do login real.
/// </summary>
public sealed class TestCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public string? UserName { get; set; } = "integration-test";
    public bool IsAuthenticated { get; set; } = true;
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public IReadOnlyCollection<string> Permissions { get; set; } = [];
}
