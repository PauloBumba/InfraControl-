import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { WiFiPage } from "./WiFiPage";
import {
  getUnifiWifi,
  type UnifiAccessPointItem,
  type UnifiSsidItem,
  type UnifiWifi,
} from "@/services/api/integrations";
import { getUnifiLiveClients, type UniFiConnectedClient } from "@/services/api/unifi";
import { listDevices, type DeviceItem } from "@/services/api/inventory";

/**
 * Adoção do padrão de visões (backlog Operations UX, pedido 2026-10-11).
 *
 * A WiFiPage não é um índice: são 2 tabelas distintas (SSIDs e telemetria
 * de APs) mais um inventário que JÁ é grid de cards. Cada tabela ganha
 * seu PRÓPRIO seletor no cabeçalho da seção, com preferência própria no
 * localStorage — um seletor global não teria sobre o que agir.
 *
 * Agrupamentos reais: SSIDs → `enabled` (Ativa/Desativa, o mesmo da
 * coluna "Status"); APs → `connected` (Conectado/Desconectado, o mesmo
 * da coluna "Estado"). O inventário não ganha seletor: ele não é tabela.
 */
vi.mock("@/services/api/integrations", () => ({
  getUnifiWifi: vi.fn(),
  getUnifiInsights: vi.fn(),
}));

vi.mock("@/services/api/unifi", () => ({
  getUnifiLiveClients: vi.fn(),
}));

vi.mock("@/services/api/inventory", () => ({
  listDevices: vi.fn(),
}));

const getUnifiWifiMock = vi.mocked(getUnifiWifi);
const getUnifiLiveClientsMock = vi.mocked(getUnifiLiveClients);
const listDevicesMock = vi.mocked(listDevices);

const ssids: UnifiSsidItem[] = [
  { name: "Corp", security: "wpa2psk", enabled: true, vlan: 10, bands: "5 GHz", isGuest: false },
  // "Visitantes" e não "Convidados": esse nome colidiria com o header da
  // coluna "Convidados" da própria tabela.
  { name: "Visitantes", security: "none", enabled: false, vlan: 0, bands: "2.4 GHz", isGuest: true },
];

const accessPoints: UnifiAccessPointItem[] = [
  {
    mac: "f0:9f:c2:00:00:01",
    name: "AP-sala-reuniao",
    model: "U6-Lite",
    ip: "192.168.10.21",
    connected: true,
    uptimeSeconds: 90_000,
    cpuPercent: 12,
    memPercent: 40,
    rxBytes: 1_000,
    txBytes: 2_000,
    clients: 8,
    radios: [],
  },
  {
    mac: "f0:9f:c2:00:00:02",
    name: "AP-externo",
    model: "U6-Mesh",
    ip: "192.168.10.22",
    connected: false,
    uptimeSeconds: 0,
    cpuPercent: 0,
    memPercent: 0,
    rxBytes: 0,
    txBytes: 0,
    clients: 0,
    radios: [],
  },
];

const wifi: UnifiWifi = { ssids, accessPoints, capturedAtUtc: "2026-10-11T10:00:00Z" };

/**
 * F30-T004 S1 — clientes AO VIVO (`GET /integrations/unifi/clients`):
 * 5 clientes (1 com fio, 4 sem fio), Corp em 2, Visitantes em 1, um sem
 * essid (vira "—") e média de sinal só dos 2 que informaram (-55/-67 → -61).
 * S1b: rádios brutos — "ng" = 2.4 GHz × 2, "na" = 5 GHz × 1, um sem
 * rádio (agrupa sob "—" na banda); com-fio fica fora.
 */
const liveClients: UniFiConnectedClient[] = [
  { mac: "aa:bb:cc:dd:ee:01", ip: "10.0.0.1", hostname: "notebook-ana", apMac: "f0:9f:c2:00:00:01", switchMac: null, essid: "Corp", networkName: null, signal: -55, isWired: false, lastSeenUnix: 1, radio: "ng" },
  { mac: "aa:bb:cc:dd:ee:02", ip: "10.0.0.2", hostname: "celular-bruno", apMac: "f0:9f:c2:00:00:01", switchMac: null, essid: "Corp", networkName: null, signal: -67, isWired: false, lastSeenUnix: 1, radio: "na" },
  { mac: "aa:bb:cc:dd:ee:03", ip: "10.0.0.3", hostname: "visita-1", apMac: "f0:9f:c2:00:00:02", switchMac: null, essid: "Visitantes", networkName: null, signal: null, isWired: false, lastSeenUnix: 1, radio: "ng" },
  { mac: "aa:bb:cc:dd:ee:04", ip: "10.0.0.4", hostname: "iot-camera", apMac: "f0:9f:c2:00:00:01", switchMac: null, essid: null, networkName: null, signal: null, isWired: false, lastSeenUnix: 1, radio: null },
  { mac: "aa:bb:cc:dd:ee:05", ip: "10.0.0.5", hostname: "pc-mesa", apMac: null, switchMac: "f0:9f:c2:00:00:03", essid: null, networkName: null, signal: null, isWired: true, lastSeenUnix: 1, radio: null },
];

const inventoryAp: DeviceItem = {
  id: "dev-1",
  locationId: "loc-1",
  rackId: null,
  type: "AccessPoint",
  name: "AP-sala-reuniao",
  ip: "192.168.10.21",
  mac: "f0:9f:c2:00:00:01",
  serial: "SN123",
  model: "U6-Lite",
  vendor: "Ubiquiti",
  vlanId: null,
  description: null,
  monitoringChecks: [],
  profileIsCustom: false,
};

/** Cabeçalho da seção — é onde o seletor daquela seção mora. */
function sectionHeader(title: string): HTMLElement {
  const header = screen.getByText(title).closest(".uni-panel-header");
  if (!header) throw new Error(`cabeçalho da seção "${title}" não encontrado`);
  return header as HTMLElement;
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <WiFiPage />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  // a preferência de visão vive no localStorage — zera p/ default determinístico
  localStorage.clear();
  getUnifiWifiMock.mockResolvedValue(wifi);
  getUnifiLiveClientsMock.mockResolvedValue(liveClients);
  listDevicesMock.mockResolvedValue({ items: [inventoryAp], totalCount: 1, page: 1, pageSize: 20 });
});

describe("WiFiPage — seletor por seção (tabela/grid/quadro)", () => {
  it("should keep both sections as tables by default", async () => {
    const { container } = renderPage();

    expect(await screen.findByText("Visitantes")).toBeInTheDocument();
    expect(container.querySelectorAll("table")).toHaveLength(2);
  });

  it("should group the SSID section by status in kanban", async () => {
    const user = userEvent.setup();
    const { container } = renderPage();
    await screen.findByText("Visitantes");

    await user.click(within(sectionHeader("Redes (SSID)")).getByRole("tab", { name: "Kanban" }));

    // as 2 colunas de status; o mesmo rótulo está na coluna E no card → All.
    expect(screen.getAllByText("Ativa").length).toBeGreaterThan(1);
    expect(screen.getAllByText("Desativa").length).toBeGreaterThan(1);
    expect(container.querySelectorAll("table")).toHaveLength(1);
  });

  it("should switch only the AP telemetry section to a grid, leaving SSIDs as a table", async () => {
    const user = userEvent.setup();
    const { container } = renderPage();
    await screen.findByText("Visitantes");

    await user.click(within(sectionHeader("Access Points — telemetria")).getByRole("tab", { name: "Grid" }));

    expect(container.querySelectorAll("table")).toHaveLength(1);
    // "Clientes: N" só existe nos cards de TELETMETRIA — o inventário de APs
    // também exibe o modelo, então `getByText("U6-Lite")` casaria 2×.
    expect(screen.getByText("Clientes: 8")).toBeInTheDocument();
    expect(screen.getByText("Clientes: 0")).toBeInTheDocument();
  });

  it("should group the AP telemetry section by connection state in kanban", async () => {
    const user = userEvent.setup();
    const { container } = renderPage();
    await screen.findByText("Visitantes");

    await user.click(
      within(sectionHeader("Access Points — telemetria")).getByRole("tab", { name: "Kanban" }),
    );

    // "Desconectado" não tem card no fixture além da coluna; os 2 APs
    // ficam um em cada coluna.
    expect(screen.getAllByText("Desconectado").length).toBeGreaterThan(1);
    expect(screen.getAllByText("Conectado").length).toBeGreaterThan(1);
    expect(container.querySelectorAll("table")).toHaveLength(1);
  });

  it("should keep the inventory section always as cards, without a switcher", async () => {
    const { container } = renderPage();

    expect(await screen.findByText("Inventário de APs")).toBeInTheDocument();
    expect(screen.getByText("SN123")).toBeInTheDocument();
    // o inventário nunca foi tabela — continua card e sem seletor.
    expect(within(screen.getByText("Inventário de APs").closest("div") as HTMLElement).queryByRole("tab")).toBeNull();
    expect(container.querySelectorAll("table")).toHaveLength(2);
  });
});

/**
 * C-3 — casca do `ListPageShell`.
 *
 * Esta página é a exceção da exceção: o subtítulo não é "N itens", é o
 * resultado da SONDAGEM AO VIVO ("2 redes · 2 APs · sondagem em …"). A casca
 * monta `contagem · nota`, então a nota carrega o resto do texto byte a byte.
 *
 * E o mesmo problema da AuditPage: DOIS `<ErrorBanner>` adjacentes, um da
 * sondagem e um do inventário. Com os dois erros ao mesmo tempo a tela mostrava
 * duas caixas vermelhas lado a lado — a casca tem um slot.
 */
describe("WiFiPage — casca da lista (C-3: ListPageShell)", () => {
  it("should keep the live-probe subtitle byte-identical", async () => {
    renderPage();

    expect(
      await screen.findByText(/^2 redes · 2 APs · sondagem ao vivo em /),
    ).toBeInTheDocument();
  });

  it("should fall back to the inventory count when the probe fails", async () => {
    getUnifiWifiMock.mockRejectedValue(new Error("UniFi fora do ar"));
    renderPage();

    expect(
      await screen.findByText("1 access point · inventário (DeviceType.AccessPoint)"),
    ).toBeInTheDocument();
  });

  it("should show ONE banner when the probe and the inventory both fail", async () => {
    getUnifiWifiMock.mockRejectedValue(new Error("UniFi fora do ar"));
    listDevicesMock.mockRejectedValue(new Error("API fora do ar"));
    renderPage();

    // Os dois erros já aconteceram quando a casca monta o subtítulo de
    // fallback do inventário.
    await screen.findByText(/· inventário \(DeviceType\.AccessPoint\)/);

    expect(screen.getAllByRole("alert")).toHaveLength(1);
    // A casca tem UM slot. Quem fica é a fonte da sondagem (a mesma ordem do
    // `formError ?? error` da AuditPage); o do inventário é descartado, não
    // duplicado. Se um dia precisar dos dois, é a casca que muda — não aqui.
    expect(screen.getByText("UniFi fora do ar")).toBeInTheDocument();
    expect(screen.queryByText("API fora do ar")).toBeNull();
  });
});

/**
 * F30-T004 S1 (docs/52 §S1) — painel "Clientes ao vivo": agregados do
 * `GET /integrations/unifi/clients` que JÁ existe (ficha 45 áreas C/E:
 * clientes por SSID e RSSI médio). Label e valor no MESMO text node —
 * `getByText` não junta irmãos (lição do S5c do docs/51).
 */
describe("WiFiPage — clientes ao vivo (F30-T004 S1)", () => {
  it("should show total/wired/wireless and average RSSI derived from live clients", async () => {
    renderPage();

    expect(await screen.findByText("Total: 5")).toBeInTheDocument();
    expect(screen.getByText("Com fio: 1")).toBeInTheDocument();
    expect(screen.getByText("Sem fio: 4")).toBeInTheDocument();
    expect(screen.getByText("RSSI médio: -61 dBm")).toBeInTheDocument();
  });

  it("should list clients per SSID ordered by count, rendering a missing essid as —", async () => {
    renderPage();

    expect(await screen.findByText("Corp: 2")).toBeInTheDocument();
    expect(screen.getByText("Visitantes: 1")).toBeInTheDocument();
    expect(screen.getByText("—: 1")).toBeInTheDocument();
  });

  it("should show an empty clients panel with — when the controller reports none", async () => {
    getUnifiLiveClientsMock.mockResolvedValue([]);
    renderPage();

    expect(await screen.findByText("Total: 0")).toBeInTheDocument();
    expect(screen.getByText("RSSI médio: —")).toBeInTheDocument();
    expect(screen.getByText("Por banda: —")).toBeInTheDocument();
    expect(screen.getByText("Nenhum cliente sem-fio no Controller")).toBeInTheDocument();
  });

  it("should show — on the clients panel when the probe fails, keeping the rest of the page", async () => {
    getUnifiLiveClientsMock.mockRejectedValue(new Error("Clients fora do ar"));
    renderPage();

    expect(await screen.findByText("Total: —")).toBeInTheDocument();
    expect(screen.getByText("RSSI médio: —")).toBeInTheDocument();
    expect(screen.getByText("Por banda: —")).toBeInTheDocument();
    // as demais seções seguem vivas com os dados da sondagem de WiFi
    expect(screen.getByText("Visitantes")).toBeInTheDocument();
    expect(screen.queryByText("Clients fora do ar")).toBeNull();
  });

  it("should show clients per band derived from the raw radio (S1b)", async () => {
    // banda vem do RÁDIO BRUTO mapeado no utilitário ("ng"→2.4, "na"→5);
    // sem rádio conhecido agrupa sob "—" — nunca banda inventada. Tudo em
    // UM text node (getByText não junta irmãos) para não colidir com as
    // linhas de SSID.
    renderPage();

    expect(
      await screen.findByText("Por banda: 2.4 GHz 2 · 5 GHz 1 · — 1")
    ).toBeInTheDocument();
  });
});
