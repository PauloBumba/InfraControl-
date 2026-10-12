import { describe, expect, it } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PublicLayout } from "./PublicLayout";

/**
 * F26-T002 — header/footer COMPARTILHADOS entre as rotas públicas de vendas.
 *
 * O layout é um componente só (não copia por página), tem navegação por
 * `Link` (nada de `<a href>` que recarregue o app), marca a rota atual com
 * `aria-current="page"` e leva sempre ao login — a landing é porta de
 * entrada, não substitui o produto.
 */
function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route
          path="/home"
          element={
            <PublicLayout>
              <p>conteúdo da home</p>
            </PublicLayout>
          }
        />
        <Route
          path="/recursos"
          element={
            <PublicLayout>
              <p>conteúdo de recursos</p>
            </PublicLayout>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

describe("PublicLayout (F26-T002)", () => {
  it("should render the brand and the shared navigation", () => {
    renderAt("/home");

    const nav = screen.getByRole("navigation", { name: "Navegação principal" });
    expect(within(nav).getByRole("link", { name: "Home" })).toHaveAttribute("href", "/home");
    expect(within(nav).getByRole("link", { name: "Recursos" })).toHaveAttribute("href", "/recursos");
    expect(within(nav).getByRole("link", { name: "Contato" })).toHaveAttribute("href", "/contato");
  });

  it("should always offer the login entry point", () => {
    renderAt("/home");

    expect(screen.getAllByRole("link", { name: /entrar/i }).length).toBeGreaterThan(0);
    for (const link of screen.getAllByRole("link", { name: /entrar/i })) {
      expect(link).toHaveAttribute("href", "/login");
    }
  });

  it("should mark the current route with aria-current", () => {
    renderAt("/recursos");

    // "Recursos" existe no header E no rodapé — os dois marcam a rota atual.
    const current = screen.getAllByRole("link", { name: "Recursos" });
    expect(current.length).toBeGreaterThan(1);
    for (const link of current) {
      expect(link).toHaveAttribute("aria-current", "page");
    }
    for (const link of screen.getAllByRole("link", { name: "Home" })) {
      expect(link).not.toHaveAttribute("aria-current");
    }
  });

  it("should render children inside a main landmark and keep a footer", () => {
    const { container } = renderAt("/home");

    expect(screen.getByText("conteúdo da home")).toBeInTheDocument();
    expect(container.querySelector("main")).not.toBeNull();
    expect(container.querySelector("footer")).not.toBeNull();
  });
});
