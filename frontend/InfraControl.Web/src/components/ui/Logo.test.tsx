import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Logo } from "./Logo";

/**
 * F50-T005 (F5) — logo unificado: `Logo.tsx` é a fonte única do símbolo do
 * InfraControl (rack circular com LEDs + conexões, o mesmo desenho do
 * `favicon.svg`). Sidebar e login deixam de desenhar o badge quadrado
 * inline e passam a renderizar este componente — o teste tranca o markup
 * oficial para a cópia paralela não voltar.
 */
describe("Logo (F50-T005 — logo unificado)", () => {
  it("should render the official logo with accessible name", () => {
    render(<Logo />);

    const logo = screen.getByRole("img", { name: "Logo do InfraControl" });
    expect(logo.tagName.toLowerCase()).toBe("svg");
    // Marcador do desenho oficial (gradiente do rack) — o badge antigo usava
    // `infraLoginGrad`/`infraSidebarGrad` e não pode aparecer aqui.
    expect(logo.innerHTML).toContain("logoPrimaryGrad");
    expect(logo.innerHTML).not.toContain("infraLoginGrad");
    expect(logo.innerHTML).not.toContain("infraSidebarGrad");
  });

  it("should honor the size prop", () => {
    const { container } = render(<Logo size={22} />);
    const logo = container.querySelector("svg");
    expect(logo).toHaveAttribute("width", "22");
    expect(logo).toHaveAttribute("height", "22");
  });
});
