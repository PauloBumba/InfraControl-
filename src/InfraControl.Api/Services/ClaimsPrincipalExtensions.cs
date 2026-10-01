using System.Security.Claims;

namespace InfraControl.Api.Services;

/// <summary>
/// Identidade da sessão como a API a lê.
///
/// O cookie nasce em <c>AuthenticationController.CreatePrincipal</c> com
/// <see cref="ClaimTypes.NameIdentifier"/> (URI longa). Os literais curtos
/// ("sub"/"nameidentifier") só aparecem quando o token vem de um caminho JWT
/// com claims não mapeadas — por isso são fallback, nunca o primeiro alvo.
///
/// Antes só se olhava os fallbacks: userId/convidante saíam null para todo
/// mundo logado, quebrando /auth/me, logout, change-password e convites sem
/// erro de build nem de teste (o frontend lia payload.userId como null).
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    internal static string? GetSessionUserId(this ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? principal.FindFirst("sub")?.Value
           ?? principal.FindFirst("nameidentifier")?.Value;

    internal static Guid? GetSessionUserIdOrNull(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.GetSessionUserId(), out var userId) ? userId : null;
}
