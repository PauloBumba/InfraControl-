import { useAppSelector } from "@/app/store/hooks";
import { type AccessLevel, roleGrants } from "@/types/roles";

/**
 * F14-T009 — nível mínimo (ADR-016) por rota. Padrão `read`: qualquer role
 * autenticada alcança. As 4 rotas `administer` espelham os controllers
 * Administer do backend (Users/Roles/Invitations, Audit, RetentionPolicies,
 * DeviceCredentials) e Sessões — decisão do usuário (2026-09-30): a UI é mais
 * estrita que o self-service do backend nesta página. O backend continua sendo
 * a autoridade (403 no envelope é a rede de segurança); aqui é só a
 * conformidade da UI — menu, rota e botão escondidos, nunca autorização real.
 */
export const ROUTE_ACCESS: Readonly<Record<string, AccessLevel>> = {
  "/users": "administer",
  "/audit": "administer",
  "/settings": "administer",
  "/integrations": "administer",
  // F27 Fase 2.2 — configuração Zabbix (espelha a policy Administer do controller).
  "/integrations/zabbix": "administer",
  // FAI-T011b (ADR-057) — cadastro de providers de IA (Administer).
  "/admin/ai-providers": "administer",
  // UniFi ao vivo: consulta (Operate) — Operator + Admin.
  "/unifi": "operate",
  "/unifi/dashboard": "operate",
  "/sessions": "administer",
  // PDI: Operate (Operator + Admin) — Viewer não alcança (2026-10-04).
  "/pdi": "operate",
  // ADR-049 — relatório transversal: Operate (espelha a policy do controller).
  "/reports": "operate",
  // Projetos: Operate (Operator + Admin) — leitura para qualquer usuário logado,
  // mas ações de aprovação/submit exigem a policy do controlador.
  "/projects": "operate",
};

/** Nível exigido pela rota (curinga/dinâmica = `read`). */
export function routeLevel(pathname: string): AccessLevel {
  return ROUTE_ACCESS[pathname] ?? "read";
}

/** `true` quando ALGUMA das roles alcança o nível pedido (multi-role). */
export function canReach(
  roles: readonly string[] | undefined,
  level: AccessLevel
): boolean {
  return (roles ?? []).some((role) => roleGrants(role, level));
}

/** Hook de convenção: as roles do usuário logado vs. o nível pedido. */
export function useCan(level: AccessLevel): boolean {
  const roles = useAppSelector((state) => state.auth.user?.roles);
  return canReach(roles, level);
}
