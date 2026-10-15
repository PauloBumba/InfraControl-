/**
 * F26-T002 — rotas públicas de vendas (landing), fora do AppLayout.
 *
 * A lista é EXATA de propósito. A rota raiz `/` é a landing pública (index).
 * O dashboard protegido foi movido para `/dashboard`.
 *
 * Decisão do usuário (2026-10-14): landing em `/` (index), `/recursos` e
 * `/contato`. Dashboard autenticado em `/dashboard`.
 */
export const PUBLIC_MARKETING_PATHS = ["/", "/recursos", "/contato"] as const;

export function isPublicMarketingPath(pathname: string): boolean {
  return PUBLIC_MARKETING_PATHS.some((path) => path === pathname);
}
