using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
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

    /// <summary>
    /// Leitura do acompanhamento (ADR-033 §4): Admin, o dono do plano ou
    /// o gestor do plano — o §9 é bilateral, o gestor precisa ver os
    /// relatos para responder.
    /// </summary>
    internal static void EnsureCanView(
        ICurrentUserContext currentUser, IndividualDevelopmentPlan plan)
    {
        if (IsAdmin(currentUser))
            return;

        if (currentUser.UserId == plan.UserId
            || (plan.ManagerId is { } managerId && currentUser.UserId == managerId))
            return;

        throw new UnauthorizedAccessException(
            "Você só pode ver o acompanhamento do seu próprio PDI.");
    }

    /// <summary>
    /// Escrita do colaborador (relatos, progresso, impedimentos,
    /// exclusões): Admin ou dono do plano (§9 — lado colaborador).
    /// </summary>
    internal static void EnsureCanReport(
        ICurrentUserContext currentUser, IndividualDevelopmentPlan plan)
    {
        if (IsAdmin(currentUser) || currentUser.UserId == plan.UserId)
            return;

        throw new UnauthorizedAccessException(
            "Você só pode registrar acompanhamento no seu próprio PDI.");
    }

    /// <summary>
    /// Resposta do gestor ao check-in (§10): Admin ou o gestor
    /// definido no plano (sem gestor, só o Admin responde).
    /// </summary>
    internal static void EnsureCanRespond(
        ICurrentUserContext currentUser, IndividualDevelopmentPlan plan)
    {
        if (IsAdmin(currentUser)
            || (plan.ManagerId is { } managerId && currentUser.UserId == managerId))
            return;

        throw new UnauthorizedAccessException(
            "Apenas o gestor ou um administrador pode responder este check-in.");
    }

    /// <summary>
    /// Edição de impedimento (§11): Admin, dono ou gestor — o gestor
    /// registra ação tomada e resolução junto com o colaborador.
    /// </summary>
    internal static void EnsureCanAssist(
        ICurrentUserContext currentUser, IndividualDevelopmentPlan plan)
    {
        if (IsAdmin(currentUser)
            || currentUser.UserId == plan.UserId
            || (plan.ManagerId is { } managerId && currentUser.UserId == managerId))
            return;

        throw new UnauthorizedAccessException(
            "Você só pode editar o acompanhamento do seu próprio PDI.");
    }

    /// <summary>
    /// Revisão da evidência (§12 — Fase 4): Admin ou gestor definido
    /// no plano. O colaborador não revisa o próprio envio (sem gestor
    /// no plano, só o Admin).
    /// </summary>
    internal static void EnsureCanReview(
        ICurrentUserContext currentUser, IndividualDevelopmentPlan plan)
    {
        if (IsAdmin(currentUser))
            return;

        if (plan.ManagerId is { } managerId && currentUser.UserId == managerId)
            return;

        throw new UnauthorizedAccessException(
            "Apenas o gestor ou um administrador pode revisar evidências.");
    }

    /// <summary>
    /// Validação/rejeição de fase (§15 — Fase 5): Admin ou o gestor
    /// definido no plano. O colaborador não valida a própria fase.
    /// </summary>
    internal static void EnsureCanValidatePhase(
        ICurrentUserContext currentUser, IndividualDevelopmentPlan plan)
    {
        if (IsAdmin(currentUser))
            return;

        if (plan.ManagerId is { } managerId && currentUser.UserId == managerId)
            return;

        throw new UnauthorizedAccessException(
            "Apenas o gestor ou um administrador pode validar esta fase.");
    }

    /// <summary>
    /// Avaliação do gestor e resultado final (§15.3/§16 — Fase 6):
    /// Admin ou o gestor definido no plano. O colaborador avalia pelo
    /// lado próprio (Self), nunca pelo lado gestor.
    /// </summary>
    internal static void EnsureCanEvaluate(
        ICurrentUserContext currentUser, IndividualDevelopmentPlan plan)
    {
        if (IsAdmin(currentUser))
            return;

        if (plan.ManagerId is { } managerId && currentUser.UserId == managerId)
            return;

        throw new UnauthorizedAccessException(
            "Apenas o gestor ou um administrador pode avaliar este PDI.");
    }

    /// <summary>Admin exclusivo (feedback é histórico — ADR-033 §6).</summary>
    internal static void EnsureIsAdmin(ICurrentUserContext currentUser)
    {
        if (!IsAdmin(currentUser))
            throw new UnauthorizedAccessException(
                "Apenas administradores podem excluir feedbacks.");
    }

    internal static bool IsAdmin(ICurrentUserContext currentUser)
        => currentUser.Roles.Contains(UserRole.Admin.ToString());
}
