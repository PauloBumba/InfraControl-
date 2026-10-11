import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { useAppSelector } from "@/app/store/hooks";

/**
 * F26-T002 — "qualquer usuário logado que acesse [a landing] é redirecionado
 * ao dashboard (padrão /login)".
 *
 * Fica fora do `AppRouter` de propósito: o router importa TODAS as páginas
 * (lazy), então testar o guard isolado é rápido e cobre a regra sem
 * arrastar o bundle inteiro para o jsdom.
 *
 * Só `authenticated` redireciona. O `SessionGate` já segurou `checking` e
 * `unavailable` antes de chegar aqui, então o estado restante é `failed`
 * (sem sessão) — que é exatamente o público da landing.
 */
export function PublicMarketingRoute({ children }: { children: ReactNode }) {
  const authStatus = useAppSelector((state) => state.auth.status);

  if (authStatus === "authenticated") {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
