using Microsoft.AspNetCore.Identity;

namespace InfraControl.Infrastructure.Identity;

/// <summary>
/// Adaptador técnico para as credenciais do ASP.NET Core Identity.
/// Regras de negócio ficam no agregado InfraControl.Domain.Entities.User.
/// </summary>
public sealed class InfraControlUser : IdentityUser<Guid>
{
}
