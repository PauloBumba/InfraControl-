namespace InfraControl.Domain.RemoteAccess;

/// <summary>Estado do ciclo de vida de uma sessão de acesso remoto.</summary>
public enum RemoteSessionStatus
{
    Active,
    Closed,
    Failed
}
