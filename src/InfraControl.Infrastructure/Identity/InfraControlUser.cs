using Microsoft.AspNetCore.Identity;

namespace InfraControl.Infrastructure.Identity;

/// <summary>
/// Usuário persistido pelo ASP.NET Core Identity.
/// Regras de negócio e permissões específicas serão adicionadas nas Tasks de Identity seguintes.
/// </summary>
public sealed class InfraControlUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
}
