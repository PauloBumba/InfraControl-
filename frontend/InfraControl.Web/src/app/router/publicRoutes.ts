/**
 * F26-T002 — rotas públicas de vendas (landing), fora do AppLayout.
 *
 * A lista é EXATA de propósito. `/` continua sendo o dashboard protegido e
 * não pode virar pública por acidente: `routeLevel()` devolve `read` para
 * qualquer caminho fora do mapa, então um prefixo tipo `/home*` publicaria
 * `/home/qualquer-coisa` sem ninguém pedir.
 *
 * Decisão do usuário (2026-10-11): landing em `/home`, `/recursos` e
 * `/contato`, mantendo `/` como dashboard — nada de mover a rota raiz.
 */
export const PUBLIC_MARKETING_PATHS = ["/home", "/recursos", "/contato"] as const;

export function isPublicMarketingPath(pathname: string): boolean {
  return PUBLIC_MARKETING_PATHS.some((path) => path === pathname);
}
