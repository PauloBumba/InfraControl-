namespace InfraControl.Application.Common.Interfaces;

/// <summary>
/// A definição única de "termo de busca vazio".
///
/// F29-T001A: <c>string.IsNullOrWhiteSpace(term) ? null : term.Trim()</c> estava
/// escrito 14 vezes, byte a byte igual (Rack, Device, Vlan, Connection…). É uma
/// linha, mas é ela que decide que busca sem termo significa "sem filtro" — se
/// virar <c>""</c>, cada repositório passa a procurar por nada.
///
/// A caixa (ToLower) NÃO é aqui: acontece dentro da consulta de cada
/// repositório, e trazê-la para cá mudaria silenciosamente o que 14 repositórios
/// procuram.
/// </summary>
public static class TermNormalizer
{
    public static string? Normalize(string? term)
        => string.IsNullOrWhiteSpace(term) ? null : term.Trim();
}
