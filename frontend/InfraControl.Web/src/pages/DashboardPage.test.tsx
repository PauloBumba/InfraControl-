import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { DashboardPage } from "./DashboardPage";
import { listAlerts } from "@/services/api/alerts";
import { listClients } from "@/services/api/clients";
import { listAuditEntries } from "@/services/api/audit";
import { listDevices } from "@/services/api/inventory";
import { listIncidents } from "@/services/api/incidents";
import { listTasks } from "@/services/api/tasks";
import {
  acceptInvitation,
  getMyInvitations,
  type InvitationItem,
} from "@/services/api/invitations";

vi.mock("@/services/api/alerts", () => ({ listAlerts: vi.fn() }));
vi.mock("@/services/api/clients", () => ({ listClients: vi.fn() }));
vi.mock("@/services/api/inventory", () => ({ listDevices: vi.fn() }));
vi.mock("@/services/api/incidents", () => ({ listIncidents: vi.fn() }));
vi.mock("@/services/api/tasks", () => ({ listTasks: vi.fn() }));
vi.mock("@/services/api/invitations", () => ({
  getMyInvitations: vi.fn(),
  acceptInvitation: vi.fn(),
  rejectInvitation: vi.fn(),
}));
// AUDIT_OUTCOME_SUCCEEDED é consumido em tempo de execução pela página.
vi.mock("@/services/api/audit", () => ({
  AUDIT_OUTCOME_SUCCEEDED: "Succeeded",
  listAuditEntries: vi.fn(),
}));
vi.mock("@/components/ui/MetricChart", () => ({
  MetricChart: ({ ariaLabel }: { ariaLabel: string }) => <div role="img" aria-label={ariaLabel} />,
}));

const emptyPage = { items: [], totalCount: 0, page: 1, pageSize: 20 };

const invitation: InvitationItem = {
  id: "inv-1",
  email: "ana@exemplo.com",
  status: "Pending",
  expiresAt: "2026-12-31T12:00:00Z",
  createdAt: "2026-10-01T12:00:00Z",
  createdBy: "admin@exemplo.com",
  roles: ["Viewer"],
  decidedAt: null,
};

function CurrentPath() {
  const location = useLocation();
  return <output>{`${location.pathname}${location.search}`}</output>;
}

function renderPage() {
  return render(
    <MemoryRouter>
      <DashboardPage />
      <CurrentPath />
    </MemoryRouter>,
  );
}

/**
 * F29-T004 — duas divergências de tom no dashboard.
 *
 * 1. Erro da auditoria num `<div>` simples: sem `role="alert"`, invisível para
 *    leitor de tela, e sem botão de nova tentativa — o usuário só via o texto
 *    vermelho e tinha que recarregar a página inteira.
 * 2. Aviso de convite num `<div>` verde paralelo ao `NoticeBanner`: mesmíssimo
 *    layout e cores, mas reimplementado (sem `role="status"`, com outro
 *    rótulo no botão de fechar). É a definição de divergência.
 */
describe("DashboardPage — tom de erro e aviso (F29-T004)", () => {
  beforeEach(() => {
    vi.mocked(listAlerts).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listClients).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listDevices).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listIncidents).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listTasks).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listAuditEntries).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(getMyInvitations).mockReset().mockResolvedValue([]);
    vi.mocked(acceptInvitation).mockReset();
    localStorage.clear();
  });

  it("should announce a failed audit load as an alert with a retry", async () => {
    vi.mocked(listAuditEntries).mockRejectedValue(new Error("Falha no servidor de auditoria."));

    renderPage();

    const alerts = await screen.findAllByRole("alert");
    expect(
      alerts.some((node) => node.textContent?.includes("Não foi possível carregar os eventos.")),
    ).toBe(true);
    expect(screen.queryByText("Acesso à auditoria restrito")).toBeNull();
  });

  it("should show the invitation outcome as a dismissible notice, not an ad-hoc green div", async () => {
    vi.mocked(getMyInvitations).mockResolvedValue([invitation]);
    vi.mocked(acceptInvitation).mockResolvedValue(undefined);

    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /aceitar/i }));

    const notice = await screen.findByText("Convite de ana@exemplo.com aceito.");
    // O componente de aviso positivo é quem emite `role="status"`.
    expect(notice.closest('[role="status"]')).not.toBeNull();

    await user.click(screen.getByRole("button", { name: "Dispensar aviso" }));
    expect(screen.queryByText("Convite de ana@exemplo.com aceito.")).toBeNull();
  });

  it("offers a quick 'Novo projeto' action that deep-links to the create dialog", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Novo projeto" }));
    expect(screen.getByText("/projects?novo=1")).toBeInTheDocument();
  });

  // M7 (caça-bugs): o "N em aberto" do KPI Incidentes vem do totalCount do
  // servidor com status=active (≠ Resolved) — não do filtro local sobre a
  // 1ª página de 5.
  it("shows the open-incident KPI from the server active total, not the local page slice", async () => {
    vi.mocked(listIncidents).mockImplementation(async (filters) => {
      if (filters?.status === "active") {
        return { items: [], totalCount: 12, page: 1, pageSize: 1 } as never;
      }
      return {
        items: [
          { id: "1", title: "já resolvido", status: "Resolved" },
          { id: "2", title: "aberto", status: "Open" },
        ],
        totalCount: 40,
        page: 1,
        pageSize: 5,
      } as never;
    });

    renderPage();

    expect(await screen.findByText("12 em aberto")).toBeInTheDocument();
    // O valor principal continua sendo o total geral (40).
    expect(screen.getByText("40")).toBeInTheDocument();
    // A chamada de contagem usa o alias do servidor.
    expect(vi.mocked(listIncidents)).toHaveBeenCalledWith(
      expect.objectContaining({ status: "active" }),
    );
  });
});
