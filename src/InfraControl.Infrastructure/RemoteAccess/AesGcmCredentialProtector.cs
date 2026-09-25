using System.Security.Cryptography;
using System.Text;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Cifra AES-256-GCM da credencial (F11-T006): nonce + tag + texto cifrado
/// em base64 com prefixo de versão `v1:`. Chave vem de
/// RemoteAccess:CredentialEncryption:Key (env) — falha com mensagem pt-BR,
/// nunca com detalhe criptográfico vazando.
/// </summary>
public sealed class AesGcmCredentialProtector(IOptions<CredentialEncryptionOptions> options)
    : ICredentialProtector
{
    private const string Prefix = "v1:";
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    public string Protect(string plainText)
    {
        var key = LoadKey();

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, payload, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSizeBytes + TagSizeBytes, ciphertext.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedText)
    {
        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Falha ao decifrar credencial armazenada: formato inválido.");

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(protectedText[Prefix.Length..]);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Falha ao decifrar credencial armazenada: conteúdo inválido.");
        }

        if (payload.Length < NonceSizeBytes + TagSizeBytes)
            throw new InvalidOperationException(
                "Falha ao decifrar credencial armazenada: conteúdo inválido.");

        try
        {
            var key = LoadKey();
            var nonce = payload[..NonceSizeBytes];
            var tag = payload[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
            var ciphertext = payload[(NonceSizeBytes + TagSizeBytes)..];
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                "Falha ao decifrar credencial armazenada: chave ou conteúdo inválido.");
        }
    }

    private byte[] LoadKey()
    {
        var configured = options.Value.Key;

        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "Chave de cifra de credenciais não configurada. " +
                "Defina RemoteAccess:CredentialEncryption:Key (base64 de 32 bytes).");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configured);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Chave de cifra de credenciais inválida: não é base64 válido. " +
                "Defina RemoteAccess:CredentialEncryption:Key (base64 de 32 bytes).");
        }

        if (key.Length != KeySizeBytes)
            throw new InvalidOperationException(
                $"Chave de cifra de credenciais inválida: são necessários {KeySizeBytes} bytes " +
                "(base64). Defina RemoteAccess:CredentialEncryption:Key.");

        return key;
    }
}
