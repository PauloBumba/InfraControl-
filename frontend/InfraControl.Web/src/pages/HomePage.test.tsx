import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { HomePage } from "./HomePage";

/**
 * F26-T002 — home da landing de vendas.
 *
 * O conteúdo foi DERIVADO DO PRODUTO REAL (decisão do usuário 2026-10-11:
 * "eu derivar do produto real e te mostrar o rascunho"), não de um design
 * doc — não existe design doc no repositório. Cada pilar aponta para
 * capacidade que existe no código: Zabbix/UniFi, alerta→incidente→SLA,
 * tarefas/projetos/remoto, auditoria/sessões/convites/MFA.
 *
 * O que se tranca aqui: o h1 é o da página, os 4 pilares aparecem e os
 * CTAs apontam para /login e /recursos (a landing leva ao produto, não
 * substitui).
 */
function renderPage() {
  return render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>,
  );
}

describe("HomePage (F26-T002)", () => {
  it("should present the product in a single h1", () => {
    renderPage();

    const heading = screen.getByRole("heading", { level: 1 });
    expect(heading.textContent?.toLowerCase()).toContain("operação de infraestrutura");
  });

  it("should show the four pillars derived from the real product", () => {
    renderPage();

    for (const pillar of ["Monitorar", "Detectar", "Operar", "Governar"]) {
      expect(screen.getByRole("heading", { name: pillar })).toBeInTheDocument();
    }
    // cada pilar cita capacidade real — nada de promessa genérica. AllBy
    // porque "SLA" também aparece no lead do hero.
    for (const capability of [/Zabbix/, /UniFi/, /SLA/, /Auditoria/]) {
      expect(screen.getAllByText(capability).length).toBeGreaterThan(0);
    }
  });

  it("should lead to login and to the features page", () => {
    renderPage();

    expect(screen.getAllByRole("link", { name: /entrar/i })[0]).toHaveAttribute("href", "/login");
    expect(screen.getAllByRole("link", { name: /ver recursos/i })[0]).toHaveAttribute(
      "href",
      "/recursos",
    );
  });
});
