/**
 * Roles canônicas do InfraControl (ADR-016). Espelha `RoleAccess`
 * (src/InfraControl.Domain/Enums/RoleAccess.cs) — o backend é a fonte da
 * verdade; aqui só existe para tipar a exibição, nunca para autorizar.
 *
 * Nenhuma decisão de acesso é tomada no frontend: quem autoriza é o backend
 * (cookie HttpOnly + policies Operate/Administer). Um usuário sem role não
 * deixa de ver um botão aqui, apenas recebe 401 ao chamar a API.
 */

export const USER_ROLES = ["Admin", "Operator", "Viewer"] as const;

export type UserRole = (typeof USER_ROLES)[number];

/** Os 3 níveis de acesso (ADR-016), do mais baixo ao mais alto. */
export type AccessLevel = "read" | "operate" | "administer";

/** Hierarquia: Ler ⊃ Operar ⊃ Administrar. */
export const ROLE_ACCESS: Record<UserRole, AccessLevel> = {
  Admin: "administer",
  Operator: "operate",
  Viewer: "read",
};

export const ACCESS_LEVEL_LABEL: Record<AccessLevel, string> = {
  read: "Leitura",
  operate: "Operação",
  administer: "Administração",
};

export const ROLE_LABEL: Record<UserRole, string> = {
  Admin: "Administrador",
  Operator: "Operador",
  Viewer: "Visualizador",
};

const ACCESS_RANK: Record<AccessLevel, number> = { read: 0, operate: 1, administer: 2 };

/** true quando a role alcança o nível pedido (usado só para exibição). */
export function roleGrants(role: string, level: AccessLevel): boolean {
  const access = ROLE_ACCESS[role as UserRole];
  return access !== undefined && ACCESS_RANK[access] >= ACCESS_RANK[level];
}

/** Maior nível entre as roles do usuário — o rótulo exibido no header. */
export function highestAccessLevel(roles: readonly string[] | undefined): AccessLevel | null {
  let best: AccessLevel | null = null;
  for (const role of roles ?? []) {
    const access = ROLE_ACCESS[role as UserRole];
    if (access && (best === null || ACCESS_RANK[access] > ACCESS_RANK[best])) best = access;
  }
  return best;
}
