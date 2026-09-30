import type { ReactNode } from "react";
import type { AccessLevel } from "@/types/roles";
import { useCan } from "@/app/router/access";

/**
 * F14-T009 — renderiza os filhos só quando a role alcança `level`
 * (ex.: botões de escrita atrás de `operate`). É exibição: o backend
 * continua autorizando cada chamada (403 no envelope se algo escapar).
 */
export function Can({
  level,
  children,
}: {
  level: AccessLevel;
  children: ReactNode;
}) {
  const allowed = useCan(level);
  return allowed ? <>{children}</> : null;
}
