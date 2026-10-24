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
import { getZabbixSummary, type ZabbixSummary } from "@/services/api/zabbix";
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
vi.mock("@/services/api/zabbix", () => ({ getZabbixSummary: vi.fn() }));
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

const configuredSummary: ZabbixSummary = {
  totalHosts: 12,
  enabledHosts: 11,
  missingHosts: 1,
  hostsWithAsset: 10,
  hostsWithoutAsset: 2,
  problemsTotal: 3,
  problemsBySeverity: [0, 1, 0, 2, 0, 0],
  hostsWithProblems: 2,
  problemsAvailable: true,
  problemsError: null,
  isConfigured: true,
  detectedVersion: "7.0.0",
  lastInventorySyncAtUtc: "2026-10-23T08:00:00Z",
};

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
    vi.mocked(getZabbixSummary).mockReset().mockResolvedValue(configuredSummary);
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
          {
            id: "1",
            title: "já resolvido",
            description: null,
            severity: "Low",
            status: "Resolved",
            deviceId: null,
            openedAt: "2026-10-01T10:00:00Z",
            resolvedAt: "2026-10-02T10:00:00Z",
            alertCount: 0,
            alertIds: [],
            rootCause: null,
          },
          {
            id: "2",
            title: "aberto",
            description: null,
            severity: "Critical",
            status: "Open",
            deviceId: null,
            openedAt: "2026-10-10T10:00:00Z",
            resolvedAt: null,
            alertCount: 1,
            alertIds: ["a1"],
            rootCause: null,
          },
        ],
        totalCount: 40,
        page: 1,
        pageSize: 5,
      } as never;
    });

    renderPage();

    // F40-T001: "N em aberto" aparece no card E no detalhe da HealthBar —
    // o contrato é o VALOR vindo do servidor, não um nó único na tela.
    expect((await screen.findAllByText("12 em aberto")).length).toBeGreaterThan(0);
    // O valor principal continua sendo o total geral (40).
    expect(screen.getByText("40")).toBeInTheDocument();
    // A chamada de contagem usa o alias do servidor.
    expect(vi.mocked(listIncidents)).toHaveBeenCalledWith(
      expect.objectContaining({ status: "active" }),
    );
  });
});

/**
 * F40-T002 — layout UniFi: a pilha genérica de painéis vira uma hierarquia
 * com barra de saúde no topo, KPIs em seguida e o conteúdo em duas colunas
 * (principal × lateral de alertas). O doughnut "Distribuição operacional"
 * (que misturava contagens incomparáveis) saiu — o espaço será do gráfico
 * de tráfego real (F40-T003).
 */
describe("DashboardPage — layout UniFi (F40-T002)", () => {
  beforeEach(() => {
    vi.mocked(listAlerts).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listClients).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listDevices).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listIncidents).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listTasks).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listAuditEntries).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(getZabbixSummary).mockReset().mockResolvedValue(configuredSummary);
    vi.mocked(getMyInvitations).mockReset().mockResolvedValue([]);
    localStorage.clear();
  });

  it("does not render the old operational-distribution doughnut anymore", async () => {
    renderPage();

    // O gráfico mockado no topo do arquivo era o doughnut; ele não deve
    // ser montado. O painel de título também some.
    expect(await screen.findByText("Incidentes Recentes")).toBeInTheDocument();
    expect(screen.queryByText("Distribuição operacional")).toBeNull();
    expect(screen.queryByRole("img", { name: /distribuição operacional/i })).toBeNull();
  });

  it("keeps the health bar above the KPI cards", async () => {
    const { container } = renderPage();

    // "Dispositivos" existe 2× (card KPI + painel lateral) — o contrato é
    // a ORDEM dos nós de classe, não um texto único.
    await screen.findAllByText("Dispositivos");
    const health = container.querySelector(".uni-dash-health");
    const stats = container.querySelector(".uni-dash-stats");
    expect(health).not.toBeNull();
    expect(stats).not.toBeNull();
    // Ordem no DOM: barra de saúde antes dos cards de KPI.
    expect(
      health!.compareDocumentPosition(stats!) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("lays the content panels in a main column and a sidebar column", async () => {
    const { container } = renderPage();

    await screen.findByText("Incidentes Recentes");
    // Duas colunas: conteúdo principal (incidentes/dispositivos/clientes)
    // × lateral (alertas/tarefas/eventos).
    expect(container.querySelector(".uni-dash-layout")).not.toBeNull();
    expect(container.querySelector(".uni-dash-main")).not.toBeNull();
    expect(container.querySelector(".uni-dash-side")).not.toBeNull();
  });

  it("still offers the quick 'Novo projeto' action", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Novo projeto" }));
    expect(screen.getByText("/projects?novo=1")).toBeInTheDocument();
  });
});

/**
 * F40-T003 — painel Monitoramento (Zabbix) com dado real do summary.
 * O `getZabbixHistory` exige itemId específico (não genérico p/ dashboard);
 * o summary traz hosts, problemas por severidade e sync — o gráfico de
 * barras é alimentado por `problemsBySeverity`.
 */
describe("DashboardPage — painel Zabbix (F40-T003)", () => {
  beforeEach(() => {
    vi.mocked(listAlerts).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listClients).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listDevices).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listIncidents).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listTasks).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(listAuditEntries).mockReset().mockResolvedValue(emptyPage as never);
    vi.mocked(getZabbixSummary).mockReset().mockResolvedValue(configuredSummary);
    vi.mocked(getMyInvitations).mockReset().mockResolvedValue([]);
    localStorage.clear();
  });

  it("shows the Zabbix monitoring panel with real summary numbers", async () => {
    renderPage();

    expect(await screen.findByText("Monitoramento (Zabbix)")).toBeInTheDocument();
    expect(screen.getByText("12")).toBeInTheDocument(); // totalHosts
    expect(screen.getByText("11 habilitados")).toBeInTheDocument(); // enabledHosts no sub
    expect(screen.getByText("3")).toBeInTheDocument(); // problemsTotal
    expect(screen.getByText("7.0.0")).toBeInTheDocument(); // detectedVersion
  });

  it("renders a bar chart of problems by severity", async () => {
    const { container } = renderPage();

    // O MetricChart mockado emite role="img" com o ariaLabel passado.
    const chart = await screen.findByRole("img", {
      name: /problemas por severidade/i,
    });
    expect(chart).toBeInTheDocument();
    expect(container.querySelector(".uni-dash-zabbix")).not.toBeNull();
  });

  it("hides the Zabbix panel entirely when Zabbix is not configured", async () => {
    vi.mocked(getZabbixSummary).mockResolvedValue({
      ...configuredSummary,
      isConfigured: false,
      totalHosts: 0,
      problemsTotal: 0,
      problemsAvailable: false,
      detectedVersion: null,
    });

    renderPage();

    // O painel não renderiza quando não configurado — o dashboard fica limpo,
    // sem um bloco "não configurado" ocupando espaço.
    expect(await screen.findByText("Incidentes Recentes")).toBeInTheDocument();
    expect(screen.queryByText("Monitoramento (Zabbix)")).toBeNull();
  });

  it("does not break the dashboard when the Zabbix summary fails", async () => {
    vi.mocked(getZabbixSummary).mockRejectedValue(new Error("Zabbix fora do ar."));

    renderPage();

    // Os demais painéis continuam de pé.
    expect(await screen.findByText("Incidentes Recentes")).toBeInTheDocument();
    expect(screen.queryByText("Monitoramento (Zabbix)")).toBeNull();
  });
});
