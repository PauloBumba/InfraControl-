import { describe, expect, it } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { useServerSearch } from "./useServerSearch";

/**
 * F29-T001B — a máquina de estados da busca no servidor.
 *
 * `termInput` + `term` + `page` com "aplicar no Enter e voltar para a página 1"
 * estava reimplementada em 5 páginas (Clients, Knowledge, ZabbixHosts, Audit,
 * Tasks), cada uma com o mesmo par de estados e os mesmos dois handlers.
 *
 * O ponto que o teste tranca: o texto digitado NÃO é o termo aplicado. Ele vira
 * filtro só quando o usuário manda (Enter/Filtrar) — se fosse reativo, cada
 * tecla dispararia uma consulta no servidor.
 */
describe("useServerSearch", () => {
  it("should start empty on the first page", () => {
    const { result } = renderHook(() => useServerSearch());

    expect(result.current.input).toBe("");
    expect(result.current.term).toBe("");
    expect(result.current.page).toBe(1);
    expect(result.current.hasTerm).toBe(false);
  });

  it("should keep the typed text out of the applied term until applyInput", () => {
    const { result } = renderHook(() => useServerSearch());

    act(() => result.current.setInput("  rack a  "));

    expect(result.current.input).toBe("  rack a  ");
    expect(result.current.term).toBe("");
    expect(result.current.hasTerm).toBe(false);
  });

  it("applyInput should trim, apply and go back to the first page", () => {
    const { result } = renderHook(() => useServerSearch());

    act(() => result.current.setInput("  rack a  "));
    act(() => result.current.setPage(3));
    act(() => result.current.applyInput());

    expect(result.current.term).toBe("rack a");
    expect(result.current.hasTerm).toBe(true);
    expect(result.current.page).toBe(1);
  });

  it("applyTerm should accept a value coming from outside (filters applied together)", () => {
    const { result } = renderHook(() => useServerSearch());

    act(() => result.current.setPage(4));
    act(() => result.current.applyTerm("  switch 01 "));

    expect(result.current.term).toBe("switch 01");
    expect(result.current.page).toBe(1);
  });

  it("clear should wipe the field, the term and the page", () => {
    const { result } = renderHook(() => useServerSearch());

    act(() => result.current.setInput("rack"));
    act(() => result.current.applyInput());
    act(() => result.current.setPage(2));
    act(() => result.current.clear());

    expect(result.current.input).toBe("");
    expect(result.current.term).toBe("");
    expect(result.current.page).toBe(1);
    expect(result.current.hasTerm).toBe(false);
  });
});
