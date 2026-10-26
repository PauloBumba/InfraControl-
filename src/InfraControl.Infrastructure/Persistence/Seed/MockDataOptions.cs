namespace InfraControl.Infrastructure.Persistence.Seed;

/// <summary>
/// F46-T001 — dados mock de diagnóstico (engenharia de produto). Ligar com
/// <c>MockData:Enabled=true</c> (popula idempotente) ou
/// <c>MockData:Purge=true</c> (remove só o marcado; não popula na mesma
/// partida). Tudo é marcado — nomes com <see cref="Marker"/> e e-mails em
/// <see cref="EmailDomain"/> — para o operador identificar e limpar.
/// </summary>
public sealed class MockDataOptions
{
    public const string SectionName = "MockData";

    /// <summary>Prefixo de identificação nos nomes/títulos de domínio.</summary>
    public const string Marker = "[MOCK]";

    /// <summary>Domínio dos e-mails mock (login dev pelo front).</summary>
    public const string EmailDomain = "mock.test";

    /// <summary>
    /// Senha única dos usuários mock — política Identity forte
    /// (12+, maiúscula, minúscula, dígito, não-alfanumérico). Só dev/teste.
    /// </summary>
    public const string Password = "Mock_Dev_2026!";

    public bool Enabled { get; set; }
    public bool Purge { get; set; }
}
