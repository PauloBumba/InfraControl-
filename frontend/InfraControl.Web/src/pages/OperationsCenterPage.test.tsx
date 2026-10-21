import { describe, expect, it } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { OperationsCenterPage } from "./OperationsCenterPage";

/**
 * F37-T007 — Central de Operações (ADR-063, kiosk).
 *
 * Duas frentes tranca aqui:
 *
 * 1. COMPORTAMENTO (F37-T001/T002 + painel fixo 2026-10-21): os dados fake
 *    dos 6 módulos aparecem TODOS ao mesmo tempo (painel fixo — sem rotação
 *    automática e sem abas), e os controles de áudio/tela cheia existem.
 *    É a rede de proteção da refatoração de estilo — nenhum comportamento
 *    pode mudar.
 *
 * 2. ESTILO (F37-T007): a página tinha sido escrita com classes Tailwind que
 *    o projeto NÃO possui (o stack é PrimeFlex + `uni-*` + variáveis
 *    `--uni-*`), então grid, chips, barras de progresso, cantos e sombras
 *    morriam — e no dark ficava ilegível (texto claro sobre boxes brancos).
 *    Os hooks `.uni-ops-*` garantem que o painel usa o CSS próprio
 *    (`styles/pages/operations-center.css`) em vez das classes mortas.
 */
function renderPage() {
  return render(<OperationsCenterPage />);
}

describe("OperationsCenterPage (F37-T001 + F37-T007 + painel fixo)", () => {
  it("should present the kiosk header and every module section at once", () => {
    renderPage();

    expect(screen.getByRole("heading", { name: "InfraControle" })).toBeInTheDocument();
    expect(screen.getByText("Central de Operações")).toBeInTheDocument();

    // painel fixo: os 6 módulos (seções) aparecem simultaneamente
    // (nomes exatos: regex casaria também com os cards dentro das seções)
    expect(screen.getByRole("heading", { name: "Servidores" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Switches" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Impressoras" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Rede/Wi-Fi" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Backup" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Câmeras/Telefonia" })).toBeInTheDocument();

    // dados fake de todos os módulos visíveis ao mesmo tempo
    expect(screen.getByText("SRV-PROD-01")).toBeInTheDocument();
    const offlineCard = screen.getByText("SRV-APP-02").closest(".uni-ops-card");
    expect(offlineCard).not.toBeNull();
    expect(within(offlineCard as HTMLElement).getByText("Offline")).toBeInTheDocument();
    expect(screen.getByText("SW-CORE-01")).toBeInTheDocument();
    expect(screen.getByText("HP-LaserJet-02")).toBeInTheDocument();
    expect(screen.getByText("Toner baixo")).toBeInTheDocument();
    expect(screen.getByText("Internet Gateway")).toBeInTheDocument();
    expect(screen.getByText("PostgreSQL Daily")).toBeInTheDocument();
    expect(screen.getByText("CAM-ENTRADA-01")).toBeInTheDocument();

    const card = screen.getByText("SRV-PROD-01").closest(".uni-ops-card");
    expect(card).not.toBeNull();
    expect(within(card as HTMLElement).getByText("45%")).toBeInTheDocument(); // CPU
    expect(within(card as HTMLElement).getByText("78%")).toBeInTheDocument(); // disco
  });

  it("should NOT rotate or expose tab navigation (fixed panel, 2026-10-21)", () => {
    const { container } = renderPage();

    // sem barra de abas nem badge de rotação (removidos do painel fixo)
    expect(container.querySelector(".uni-ops-tab")).toBeNull();
    expect(container.querySelector(".uni-ops-modules")).toBeNull();
    expect(container.querySelector(".uni-ops-rotation-badge")).toBeNull();
    expect(screen.queryByText(/Rotação automática/)).not.toBeInTheDocument();
  });

  it("should expose audio and fullscreen controls (F37-T002)", () => {
    renderPage();

    expect(screen.getByRole("button", { name: /Ativar áudio/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Tela cheia/ })).toBeInTheDocument();
  });

  it("should render through the project's own uni-ops styles, not dead Tailwind classes", () => {
    const { container } = renderPage();

    // raiz + board + seções + grid + cards + chip de status vêm do
    // operations-center.css
    expect(container.querySelector(".uni-ops-page")).not.toBeNull();
    expect(container.querySelector(".uni-ops-board")).not.toBeNull();
    expect(container.querySelectorAll(".uni-ops-section").length).toBe(6);
    expect(container.querySelector(".uni-ops-grid")).not.toBeNull();
    expect(container.querySelectorAll(".uni-ops-card").length).toBeGreaterThan(0);
    expect(container.querySelectorAll(".uni-ops-chip").length).toBeGreaterThan(0);

    // classes Tailwind mortas não podem voltar (não existem no projeto)
    expect(container.querySelector(".grid-cols-1")).toBeNull();
    expect(container.querySelector(".bg-white")).toBeNull();
  });
});
