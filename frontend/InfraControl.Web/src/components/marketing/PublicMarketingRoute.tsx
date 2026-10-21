import type { ReactNode } from "react";

/**
 * F26-T002 — rotas públicas de vendas (landing) não dependem do backend.
 *
 * As páginas públicas (/, /recursos, /contato) devem funcionar mesmo sem
 * backend disponível. A verificação de autenticação só acontece no lado
 * do servidor quando o usuário tenta acessar áreas protegidas.
 */
export function PublicMarketingRoute({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
