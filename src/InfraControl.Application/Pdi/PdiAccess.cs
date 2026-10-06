using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Pdi;

/// <summary>
/// Regra de acesso única da estrutura do PDI (ADR-032): Admin gerencia
/// qualquer plano; Operador só o próprio (a policy Operate do controller
/// já barrou o Viewer).
/// </summary>
internal static class PdiAccess
{
    internal static void EnsureCanManage(ICurrentUserContext currentUser, Guid planUserId)
    {
        var isAdmin = currentUser.Roles.Contains(UserRole.Admin.ToString());
        if (!isAdmin && currentUser.UserId != planUserId)
            throw new UnauthorizedAccessException(
                "Você só pode editar a estrutura do seu próprio PDI.");
    }
}
