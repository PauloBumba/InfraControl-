import { describe, expect, it } from "vitest";
import { PUBLIC_MARKETING_PATHS, isPublicMarketingPath } from "./publicRoutes";

/**
 * F26-T002 — rotas públicas de vendas (landing), fora do AppLayout.
 *
 * A lista é EXATA de propósito. Decisão do usuário (2026-10-14): landing em
 * `/` (index), `/recursos` e `/contato`; o dashboard autenticado ficou em
 * `/dashboard`. O `routeLevel()` devolve `read` para qualquer caminho
 * desconhecido, então a garantia é por igualdade exata: `/recursosx`,
 * `/dashboard/qualquer-coisa` ou `/login` NÃO são públicos.
 *
 * Atualizado 2026-10-15 (fusão a1208c6): o contrato anterior jurava que `/`
 * era o dashboard protegido — superseded pela decisão acima, que o código já
 * implementava (o teste é que ficou para trás).
 */
describe("isPublicMarketingPath (F26-T002)", () => {
  it.each(PUBLIC_MARKETING_PATHS)("should accept %s", (path) => {
    expect(isPublicMarketingPath(path)).toBe(true);
  });

  it.each([
    "/login",
    "/dashboard",
    "/dashboard/qualquer-coisa",
    "/recursosx",
    "/home/extra",
    "/contato/1",
    "/Recursos",
  ])("should reject %s", (path) => {
    expect(isPublicMarketingPath(path)).toBe(false);
  });

  it("should keep the protected dashboard and login out of the list", () => {
    expect(PUBLIC_MARKETING_PATHS).not.toContain("/dashboard");
    expect(PUBLIC_MARKETING_PATHS).not.toContain("/login");
  });

  it("should cover exactly index, recursos and contato", () => {
    expect([...PUBLIC_MARKETING_PATHS]).toEqual(["/", "/recursos", "/contato"]);
  });
});
