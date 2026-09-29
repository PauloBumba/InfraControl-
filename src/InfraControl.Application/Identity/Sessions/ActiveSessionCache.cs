namespace InfraControl.Application.Identity.Sessions;

/// <summary>
/// ADR-020: a chave do check de sessão é contrato entre quem escreve a
/// revogação (SessionService, logout, troca/reset de senha) e quem lê
/// (ActiveSessionMiddleware). O prefixo é o mesmo da ADR-018
/// ("active-session:{sid}") — quem já tem a chave continua encontrando.
/// </summary>
public static class ActiveSessionCache
{
    public static string Key(Guid sessionId) => "active-session:" + sessionId;
}
