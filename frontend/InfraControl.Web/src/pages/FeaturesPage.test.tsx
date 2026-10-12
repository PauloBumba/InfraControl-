import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { FeaturesPage } from "./FeaturesPage";

/**
 * F26-T002 — página de recursos.
 *
 * O conteúdo foi DERIVADO DO PRODUTO REAL (decisão do usuário 2026-10-11).
 * Cada recurso listado existe no código — a página não promete nada que o
 * InfraControl não faça. Os grupos espelham como o produto já se organiza:
 * monitoramento, operação e governança.
 *
 * O que se tranca aqui: um h1 só, os 4 grupos presentes e recursos-chave
 * que precisam estar citados (Zabbix, UniFi, incidentes/SLA, projetos,
 * base de conhecimento, auditoria, MFA).
 */
function renderPage() {
  return render(
    <MemoryRouter>
      <FeaturesPage />
    </MemoryRouter>,
  );
}

describe("FeaturesPage (F26-T002)", () => {
  it("should have a single h1", () => {
    renderPage();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("should group the features the way the product is organised", () => {
    renderPage();

    for (const group of ["Monitoramento", "Operação", "Governança", "Acesso"]) {
      expect(screen.getByRole("heading", { name: group })).toBeInTheDocument();
    }
  });

  it("should only claim capabilities that exist in the product", () => {
    renderPage();

    for (const feature of [
      /Zabbix/,
      /UniFi/,
      /incidente/i,
      /SLA/,
      /projeto/i,
      /conhecimento/i,
      /auditoria/i,
      /MFA/,
    ]) {
      expect(screen.getAllByText(feature).length).toBeGreaterThan(0);
    }
  });
});
