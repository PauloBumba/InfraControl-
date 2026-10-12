import { describe, expect, it } from "vitest";
import { PUBLIC_MARKETING_PATHS, isPublicMarketingPath } from "./publicRoutes";

/**
 * F26-T002 — rotas públicas de vendas (landing), fora do AppLayout.
 *
 * A lista é EXATA de propósito: `/` continua sendo o dashboard protegido e
 * não pode cair aqui por acidente (o `routeLevel()` devolve `read` para
 * qualquer caminho desconhecido, então um prefixo tipo `/home*` deixaria
 * `/home/qualquer-coisa` pública sem ninguém pedir).
 */
describe("isPublicMarketingPath (F26-T002)", () => {
  it.each(PUBLIC_MARKETING_PATHS)("should accept %s", (path) => {
    expect(isPublicMarketingPath(path)).toBe(true);
  });

  it.each([
    "/",
    "/login",
    "/recursosx",
    "/home/extra",
    "/contato/1",
    "/Recursos",
  ])("should reject %s", (path) => {
    expect(isPublicMarketingPath(path)).toBe(false);
  });

  it("should keep / out of the list — it is the dashboard", () => {
    expect(PUBLIC_MARKETING_PATHS).not.toContain("/");
    expect(PUBLIC_MARKETING_PATHS).not.toContain("/login");
  });

  it("should cover exactly home, recursos and contato", () => {
    expect([...PUBLIC_MARKETING_PATHS]).toEqual(["/home", "/recursos", "/contato"]);
  });
});
