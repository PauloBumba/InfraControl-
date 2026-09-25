namespace InfraControl.Application.RemoteAccess.Credentials.Ports;

/// <summary>
/// Cifra/descifra a senha da credencial de acesso remoto (F11-T006).
/// Implementação usa chave-símétrica vinda só de env/config — o texto puro
/// nunca é persistido nem logado.
/// </summary>
public interface ICredentialProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedText);
}
