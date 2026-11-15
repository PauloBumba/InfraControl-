import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { TopologyPage } from "./TopologyPage";
import {
  getDeviceSystemInfo,
  listDevices,
  listLocations,
  listSwitchPorts,
  type DeviceItem,
} from "@/services/api/inventory";
import { getTopology, listVlans } from "@/services/api/network";
import { getDeviceResults } from "@/services/api/monitoring";
import { listAlerts } from "@/services/api/alerts";
import { getZabbixHostItems, getZabbixHosts } from "@/services/api/zabbix";
import { getUniFiCorrelation } from "@/services/api/unifi";

vi.mock("@/services/api/inventory", () => ({
  listDevices: vi.fn(),
  listLocations: vi.fn(),
  getDeviceSystemInfo: vi.fn(),
  listSwitchPorts: vi.fn(),
}));

vi.mock("@/services/api/network", () => ({
  getTopology: vi.fn(),
  listVlans: vi.fn(),
}));

vi.mock("@/services/api/monitoring", () => ({
  getDeviceResults: vi.fn(),
}));

vi.mock("@/services/api/alerts", () => ({
  listAlerts: vi.fn(),
}));

vi.mock("@/services/api/zabbix", () => ({
  getZabbixHosts: vi.fn(),
  getZabbixHostItems: vi.fn(),
}));

vi.mock("@/services/api/unifi", () => ({
  getUniFiCorrelation: vi.fn(),
}));

const topologyMock = vi.mocked(getTopology);

/**
 * F53-T005 — defaults do bundle do painel de detalhes: seleção de device
 * dispara system-info + último poll + alertas (+ Zabbix/UniFi/portas quando
 * o device tem os dados). Sem isto, os testes antigos que selecionam um
 * device (T003/T004) explodiriam em mock sem retorno.
 */
function mockDetailDefaults() {
  vi.mocked(getDeviceSystemInfo).mockResolvedValue(null);
  vi.mocked(getDeviceResults).mockResolvedValue(null);
  vi.mocked(listAlerts).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 5 });
  vi.mocked(listSwitchPorts).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  vi.mocked(getZabbixHosts).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  vi.mocked(getZabbixHostItems).mockResolvedValue([]);
  vi.mocked(getUniFiCorrelation).mockResolvedValue({ matched: [], unlinked: [], conflicts: [] });
}

/**
 * F29-T004 — o erro INICIAL virava estado vazio. `if (error && !graph)`
 * devolvia um `uni-page-empty` com a mensagem dentro de um `<h2>` e um botão:
 * a tela dizia "não há dados aqui" quando na verdade a REQUISIÇÃO falhou. Os
 * dois estados pedem ações diferentes — um é "ajuste o filtro", o outro é
 * "tente de novo".
 *
 * O que se tranca: a falha aparece com semântica de alerta (`role="alert"`)
 * E a página mantém o cabeçalho (não vira estado vazio).
 */
describe("TopologyPage — tom do erro (F29-T004)", () => {
  beforeEach(() => {
    topologyMock.mockReset();
    vi.mocked(listDevices).mockReset();
    vi.mocked(listLocations).mockReset();
    vi.mocked(listVlans).mockReset();
    localStorage.clear();
  });

  it("should announce an initial load failure as an alert, not as an empty state", async () => {
    topologyMock.mockRejectedValue(new Error("Não foi possível falar com o servidor."));
    vi.mocked(listDevices).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    vi.mocked(listLocations).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    vi.mocked(listVlans).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });

    render(
      <MemoryRouter>
        <TopologyPage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Não foi possível falar com o servidor.");

    // Continua sendo a página de Topologia, com cabeçalho — não um vazio.
    expect(screen.getByText("Topologia de Rede")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /tentar de novo/i })).toBeInTheDocument();
  });
});

/**
 * F53-T003 — o mapa deixou a grade fixa de 7 colunas e passou a se
 * organizar em faixas por `layer` (BFS do backend, F53-T002): Camada 1 no
 * topo até "Sem hierarquia" embaixo; arrastar faz pan; "Centralizar"
 * volta ao estado inicial; cada faixa tem toggle recolher/expandir
 * (`aria-expanded`) que esconde devices, arestas e ghosts da camada.
 */
describe("TopologyPage — layout hierárquico (F53-T003)", () => {
  const sampleGraph = {
    nodes: [
      { id: "if-r1", deviceId: "dev-router", name: "ge-0/0/0", deviceName: "router-core", deviceType: "Router", ip: "192.0.2.1", status: "up" as const, isGhost: false, port: null, layer: 1 },
      { id: "if-f1", deviceId: "dev-fw", name: "eth0", deviceName: "fw-edge", deviceType: "Firewall", ip: "192.0.2.2", status: "up" as const, isGhost: false, port: null, layer: 1 },
      { id: "if-s1", deviceId: "dev-sw", name: "Gi0/1", deviceName: "sw-access", deviceType: "Switch", ip: "192.0.2.3", status: "up" as const, isGhost: false, port: null, layer: 2 },
      { id: "if-h1", deviceId: "dev-host", name: "eth0", deviceName: "host-solt", deviceType: "Server", ip: "192.0.2.4", status: null, isGhost: false, port: null, layer: null },
    ],
    edges: [
      { id: "edge-1", sourceInterfaceId: "if-r1", targetInterfaceId: "if-s1", isGhost: false },
    ],
  };

  beforeEach(() => {
    topologyMock.mockReset();
    vi.mocked(listDevices).mockReset();
    vi.mocked(listLocations).mockReset();
    vi.mocked(listVlans).mockReset();
    mockDetailDefaults();
    localStorage.clear();

    topologyMock.mockResolvedValue(sampleGraph);
    vi.mocked(listDevices).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    vi.mocked(listLocations).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    vi.mocked(listVlans).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  });

  it("should draw hierarchy bands from the API layer, ending with the out-of-hierarchy group", async () => {
    render(
      <MemoryRouter>
        <TopologyPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Camada 1 · 2 devices")).toBeInTheDocument();
    expect(screen.getByText("Camada 2 · 1 device")).toBeInTheDocument();
    expect(screen.getByText("Sem hierarquia · 1 device")).toBeInTheDocument();

    // A hierarquia guia a ordem vertical: Camada 1 acima da Camada 2.
    const layer1 = screen.getByText("Camada 1 · 2 devices");
    const layer2 = screen.getByText("Camada 2 · 1 device");
    expect(layer1.compareDocumentPosition(layer2) & Node.DOCUMENT_POSITION_FOLLOWING)
      .toBeTruthy();
  });

  it("should collapse and expand a layer group, hiding its devices and edges", async () => {
    const { container } = render(
      <MemoryRouter>
        <TopologyPage />
      </MemoryRouter>,
    );

    await screen.findByText("sw-access");
    const neutralEdges = () =>
      container.querySelectorAll('line[stroke="#94a3b8"]');
    expect(neutralEdges()).toHaveLength(1);

    const toggle = screen.getByRole("button", { name: "Camada 2" });
    expect(toggle).toHaveAttribute("aria-expanded", "true");

    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByText("sw-access")).toBeNull();
    expect(neutralEdges()).toHaveLength(0);
    // A faixa continua visível (só o corpo some) e as outras camadas ficam.
    expect(screen.getByText("Camada 2 · 1 device")).toBeInTheDocument();
    expect(screen.getByText("router-core")).toBeInTheDocument();

    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText("sw-access")).toBeInTheDocument();
    expect(neutralEdges()).toHaveLength(1);
  });

  it("should pan the map on drag and reset pan/zoom with Centralizar", async () => {
    render(
      <MemoryRouter>
        <TopologyPage />
      </MemoryRouter>,
    );

    const map = await screen.findByTestId("topology-map");
    const canvas = screen.getByTestId("topology-canvas");
    expect(canvas.style.transform).toContain("translate(0px, 0px)");

    fireEvent.mouseDown(map, { clientX: 100, clientY: 80 });
    fireEvent.mouseMove(window, { clientX: 150, clientY: 110 });
    fireEvent.mouseUp(window);
    expect(canvas.style.transform).toContain("translate(50px, 30px)");

    fireEvent.click(screen.getByRole("button", { name: "Aumentar zoom" }));
    expect(screen.getByText("110%")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /centralizar/i }));
    expect(canvas.style.transform).toContain("translate(0px, 0px)");
    expect(canvas.style.transform).toContain("scale(1)");
    expect(screen.getByText("100%")).toBeInTheDocument();
  });

  it("should not select a device after a drag that ends over it", async () => {
    render(
      <MemoryRouter>
        <TopologyPage />
      </MemoryRouter>,
    );

    const map = await screen.findByTestId("topology-map");
    await screen.findByText("router-core");

    // Arrasto que termina sobre o device: não é clique, é pan.
    fireEvent.mouseDown(map, { clientX: 60, clientY: 60 });
    fireEvent.mouseMove(window, { clientX: 160, clientY: 90 });
    fireEvent.mouseUp(window);
    fireEvent.click(screen.getByText("router-core"));

    expect(screen.queryByText("Detalhes")).toBeNull();

    // Um clique limpo ainda seleciona — mousedown+mouseup (o mousedown
    // zera a ref de arrasto, como num browser real) e depois o click.
    const routerLabel = screen.getByText("router-core");
    fireEvent.mouseDown(routerLabel, { clientX: 60, clientY: 60 });
    fireEvent.mouseUp(routerLabel);
    fireEvent.click(routerLabel);
    expect(screen.getByText("Detalhes")).toBeInTheDocument();
  });
});

/**
 * F53-T004 — busca (nome/IP) + filtros combináveis (categoria ∧ local ∧
 * VLAN ∧ só problemas, tudo AND) + destaque do selecionado: ao clicar num
 * switch, as conexões dele ganham traço forte, os vizinhos ganham anel e
 * o resto esmaece. VLAN do device = união das VLANs nas pontas das
 * Connections dele (dado real da aresta — sem Connection, sem VLAN).
 */
describe("TopologyPage — busca, filtros combináveis e destaque (F53-T004)", () => {
  const filterGraph = {
    nodes: [
      { id: "if-r1", deviceId: "dev-router", name: "ge-0/0/0", deviceName: "router-core", deviceType: "Router", ip: "192.0.2.1", status: "up" as const, isGhost: false, port: null, layer: 1 },
      { id: "if-f1", deviceId: "dev-fw", name: "eth0", deviceName: "fw-edge", deviceType: "Firewall", ip: "192.0.2.2", status: "up" as const, isGhost: false, port: null, layer: 1 },
      { id: "if-s1", deviceId: "dev-sw", name: "Gi0/1", deviceName: "sw-access", deviceType: "Switch", ip: "192.0.2.3", status: "down" as const, isGhost: false, port: null, layer: 2 },
      { id: "if-h1", deviceId: "dev-host", name: "eth0", deviceName: "host-solt", deviceType: "Server", ip: "192.0.2.4", status: null, isGhost: false, port: null, layer: null },
    ],
    edges: [
      { id: "e1", sourceInterfaceId: "if-r1", targetInterfaceId: "if-s1", isGhost: false, sourceVlans: [{ number: 10, name: "Dados" }], targetVlans: [{ number: 10, name: "Dados" }] },
      { id: "e2", sourceInterfaceId: "if-f1", targetInterfaceId: "if-s1", isGhost: false, sourceVlans: [{ number: 20, name: "Voz" }], targetVlans: [{ number: 20, name: "Voz" }] },
      { id: "e3", sourceInterfaceId: "if-r1", targetInterfaceId: "if-f1", isGhost: false },
    ],
  };

  const mkDevice = (over: Partial<DeviceItem> & { id: string }): DeviceItem => ({
    locationId: "loc-1",
    rackId: null,
    type: "Other",
    name: over.id,
    ip: null,
    mac: null,
    serial: null,
    model: null,
    vendor: null,
    vlanId: null,
    description: null,
    monitoringChecks: [],
    profileIsCustom: false,
    ...over,
  });

  const deviceItems: DeviceItem[] = [
    mkDevice({ id: "dev-router", name: "router-core", type: "Router", ip: "192.0.2.1", locationId: "loc-1" }),
    mkDevice({ id: "dev-fw", name: "fw-edge", type: "Firewall", ip: "192.0.2.2", locationId: "loc-1" }),
    mkDevice({ id: "dev-sw", name: "sw-access", type: "Switch", ip: "192.0.2.3", locationId: "loc-2" }),
    mkDevice({ id: "dev-host", name: "host-solt", type: "Server", ip: "192.0.2.4", locationId: "loc-2" }),
  ];

  beforeEach(() => {
    topologyMock.mockReset();
    vi.mocked(listDevices).mockReset();
    vi.mocked(listLocations).mockReset();
    vi.mocked(listVlans).mockReset();
    mockDetailDefaults();
    localStorage.clear();

    topologyMock.mockResolvedValue(filterGraph);
    vi.mocked(listDevices).mockResolvedValue({ items: deviceItems, totalCount: 4, page: 1, pageSize: 20 });
    vi.mocked(listLocations).mockResolvedValue({
      items: [
        { id: "loc-1", name: "Datacenter", description: null },
        { id: "loc-2", name: "Escritório", description: null },
      ],
      totalCount: 2, page: 1, pageSize: 20,
    });
    vi.mocked(listVlans).mockResolvedValue({
      items: [
        { id: "v10", name: "Dados", vlanNumber: 10, description: null },
        { id: "v20", name: "Voz", vlanNumber: 20, description: null },
      ],
      totalCount: 2, page: 1, pageSize: 20,
    });
  });

  const renderPage = () =>
    render(
      <MemoryRouter>
        <TopologyPage />
      </MemoryRouter>,
    );

  it("should search devices by name and by IP", async () => {
    renderPage();
    await screen.findByText("sw-access");

    const search = screen.getByLabelText("Buscar device por nome ou IP");
    fireEvent.change(search, { target: { value: "acce" } });
    expect(screen.getByText("sw-access")).toBeInTheDocument();
    expect(screen.queryByText("router-core")).toBeNull();
    expect(screen.queryByText("host-solt")).toBeNull();

    fireEvent.change(search, { target: { value: "192.0.2.4" } });
    expect(screen.getByText("host-solt")).toBeInTheDocument();
    expect(screen.queryByText("sw-access")).toBeNull();

    fireEvent.change(search, { target: { value: "" } });
    expect(screen.getByText("router-core")).toBeInTheDocument();
    expect(screen.getByText("sw-access")).toBeInTheDocument();
  });

  it("should combine location, VLAN and problems filters with AND semantics", async () => {
    renderPage();
    await screen.findByText("sw-access");

    fireEvent.change(screen.getByLabelText("Filtrar por localização"), { target: { value: "loc-1" } });
    expect(screen.getByText("router-core")).toBeInTheDocument();
    expect(screen.getByText("fw-edge")).toBeInTheDocument();
    expect(screen.queryByText("sw-access")).toBeNull();

    // loc-1 ∧ VLAN 20 → só fw-edge (router só tem a VLAN 10 na ponta da e1).
    fireEvent.change(screen.getByLabelText("Filtrar por VLAN"), { target: { value: "20" } });
    expect(screen.getByText("fw-edge")).toBeInTheDocument();
    expect(screen.queryByText("router-core")).toBeNull();

    // loc-1 ∧ VLAN 20 ∧ só problemas → vazio (ambos loc-1 estão up).
    fireEvent.click(screen.getByRole("button", { name: "Só problemas" }));
    expect(screen.getByText(/nenhum device corresponde aos filtros/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Limpar filtros" }));
    expect(screen.getByText("router-core")).toBeInTheDocument();
    expect(screen.getByText("fw-edge")).toBeInTheDocument();
    expect(screen.getByText("sw-access")).toBeInTheDocument();
    expect(screen.getByText("host-solt")).toBeInTheDocument();
    expect(screen.getByLabelText("Filtrar por localização")).toHaveValue("all");
    expect(screen.getByLabelText("Filtrar por VLAN")).toHaveValue("all");
  });

  it("should limit the map to down devices with Só problemas", async () => {
    renderPage();
    await screen.findByText("sw-access");

    const toggle = screen.getByRole("button", { name: "Só problemas" });
    expect(toggle).toHaveAttribute("aria-pressed", "false");
    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByText("sw-access")).toBeInTheDocument();
    expect(screen.queryByText("router-core")).toBeNull();
    expect(screen.queryByText("host-solt")).toBeNull();

    fireEvent.click(toggle);
    expect(screen.getByText("router-core")).toBeInTheDocument();
  });

  it("should highlight the selected switch's connections and neighbors", async () => {
    const { container } = renderPage();
    await screen.findByText("sw-access");

    fireEvent.click(screen.getByText("sw-access"));

    const neutral = [...container.querySelectorAll('line[stroke="#94a3b8"]')];
    // e1 (r1→sw) e e2 (fw→sw) tocam o selecionado → traço forte.
    expect(neutral.filter((line) => line.getAttribute("stroke-width") === "2.2")).toHaveLength(2);
    // e3 (r1→fw) não toca → esmaecida.
    expect(neutral.filter((line) =>
      line.getAttribute("stroke-width") === "1.2" && line.getAttribute("opacity") === "0.12",
    )).toHaveLength(1);

    // Vizinhos associados (router + fw) ganham anel de destaque.
    expect(container.querySelectorAll('circle[stroke="#93c5fd"]')).toHaveLength(2);

    // Não-associado esmaecido; vizinho inteiro.
    expect(screen.getByText("host-solt").closest("g")?.getAttribute("opacity")).toBe("0.32");
    expect(screen.getByText("router-core").closest("g")?.getAttribute("opacity")).toBe("1");
  });
});

/**
 * F53-T005 — painel de detalhes: ficha do equipamento (11.5) com dado real
 * (inventário + system-info + último poll + Zabbix + UniFi + alertas; switch
 * com portas e o que está conectado em cada uma) e painel da conexão (11.3)
 * com portas, velocidade/duplex, VLANs, estado da porta (linkUp), origem e
 * confiança. Onde o contrato não tem dado (tipo da conexão) a tela mostra
 * "—" — nunca dado inventado.
 */
describe("TopologyPage — painel de detalhes (F53-T005)", () => {
  const detailGraph = {
    nodes: [
      { id: "if-r1", deviceId: "dev-router", name: "ge-0/0/0", deviceName: "router-core", deviceType: "Router", ip: "192.0.2.1", status: "up" as const, isGhost: false, port: null, layer: 1 },
      { id: "if-sw1", deviceId: "dev-sw", name: "Gi0/1", deviceName: "sw-access", deviceType: "Switch", ip: "192.0.2.3", status: "up" as const, isGhost: false, port: null, layer: 2 },
      { id: "if-sw2", deviceId: "dev-sw", name: "Gi0/2", deviceName: "sw-access", deviceType: "Switch", ip: "192.0.2.3", status: "up" as const, isGhost: false, port: null, layer: 2 },
      { id: "if-h1", deviceId: "dev-host", name: "eth0", deviceName: "host-solt", deviceType: "Server", ip: "192.0.2.4", status: null, isGhost: false, port: null, layer: null },
      { id: "if-g1", deviceId: "dev-sw", name: "ap-unmanaged", deviceName: "sw-access", deviceType: "Switch", ip: null, status: null, isGhost: true, port: "Gi1/0/48", layer: 2 },
    ],
    edges: [
      {
        id: "e1", sourceInterfaceId: "if-r1", targetInterfaceId: "if-sw1", isGhost: false,
        sourceInterfaceName: "ge-0/0/0", targetInterfaceName: "Gi0/1",
        sourceSpeed: "Speed1G", targetSpeed: "Speed1G",
        sourceDuplex: "Full", targetDuplex: "Full",
        sourceVlans: [{ number: 10, name: "Dados" }], targetVlans: [{ number: 10, name: "Dados" }],
        source: "lldp", confidence: "confirmed",
      },
      {
        id: "e2", sourceInterfaceId: "if-sw2", targetInterfaceId: "if-h1", isGhost: false,
        sourceInterfaceName: "Gi0/2", targetInterfaceName: "eth0",
        sourceSpeed: null, targetSpeed: null,
        sourceDuplex: null, targetDuplex: null,
        sourceVlans: null, targetVlans: null,
        source: "manual", confidence: "estimated",
      },
      {
        id: "e3", sourceInterfaceId: "if-sw1", targetInterfaceId: "if-g1", isGhost: true,
        sourceInterfaceName: "Gi0/1", targetInterfaceName: "ap-unmanaged",
        sourceSpeed: null, targetSpeed: null,
        sourceDuplex: null, targetDuplex: null,
        sourceVlans: null, targetVlans: null,
        source: "lldp", confidence: "estimated",
      },
    ],
  };

  const mkDevice = (over: Partial<DeviceItem> & { id: string }): DeviceItem => ({
    locationId: "loc-1",
    rackId: null,
    type: "Other",
    name: over.id,
    ip: null,
    mac: null,
    serial: null,
    model: null,
    vendor: null,
    vlanId: null,
    description: null,
    monitoringChecks: [],
    profileIsCustom: false,
    ...over,
  });

  const deviceItems: DeviceItem[] = [
    mkDevice({ id: "dev-router", name: "router-core", type: "Router", ip: "192.0.2.1" }),
    mkDevice({
      id: "dev-sw", name: "sw-access", type: "Switch", ip: "192.0.2.3",
      mac: "aa:bb:cc:dd:ee:ff", serial: "SN123", model: "C9200-48P",
      vendor: "Cisco", vlanId: "v10",
    }),
    mkDevice({ id: "dev-host", name: "host-solt", type: "Server", ip: "192.0.2.4", locationId: "loc-2", mac: "11:22:33:44:55:66" }),
  ];

  const swPorts = {
    items: [
      { id: "p1", deviceId: "dev-sw", name: "Gi0/1", description: null, linkUp: true, speed: "Speed1G", duplex: "Full" },
      { id: "p2", deviceId: "dev-sw", name: "Gi0/2", description: null, linkUp: false, speed: null, duplex: null },
    ],
    totalCount: 2, page: 1, pageSize: 20,
  };

  beforeEach(() => {
    topologyMock.mockReset();
    vi.mocked(listDevices).mockReset();
    vi.mocked(listLocations).mockReset();
    vi.mocked(listVlans).mockReset();
    mockDetailDefaults();
    localStorage.clear();

    topologyMock.mockResolvedValue(detailGraph);
    vi.mocked(listDevices).mockResolvedValue({ items: deviceItems, totalCount: 3, page: 1, pageSize: 20 });
    vi.mocked(listLocations).mockResolvedValue({
      items: [{ id: "loc-1", name: "Datacenter", description: null }],
      totalCount: 1, page: 1, pageSize: 20,
    });
    vi.mocked(listVlans).mockResolvedValue({
      items: [{ id: "v10", name: "Dados", vlanNumber: 10, description: null }],
      totalCount: 1, page: 1, pageSize: 20,
    });
  });

  const renderPage = () =>
    render(
      <MemoryRouter>
        <TopologyPage />
      </MemoryRouter>,
    );

  const detailsPanel = () =>
    screen.getByText("Detalhes").closest(".uni-panel") as HTMLElement;

  it("should show the device sheet: inventory, uptime, last poll, Zabbix, UniFi, events and switch ports", async () => {
    vi.mocked(getDeviceSystemInfo).mockResolvedValue({
      uptimeSeconds: 90061, sysDescr: "Cisco IOS XE", capturedAtUtc: "2026-11-14T09:00:00.000Z",
    });
    vi.mocked(getDeviceResults).mockResolvedValue({
      deviceId: "dev-sw", deviceName: "sw-access",
      results: [{
        id: "r1", check: "Ping", succeeded: true, durationMs: 4,
        error: null, values: null, checkedAt: "2026-11-14T10:30:00.000Z",
      }],
    });
    vi.mocked(listAlerts).mockResolvedValue({
      items: [{
        id: "a1", deviceId: "dev-sw", deviceName: "sw-access", check: "Ping",
        source: "Native", severity: "High", status: "open",
        message: "Sem resposta há 5 min", openedAt: "2026-11-13T08:00:00.000Z",
        resolvedAt: null, operatorNote: null,
      }],
      totalCount: 7, page: 1, pageSize: 5,
    });
    vi.mocked(getZabbixHosts).mockResolvedValue({
      items: [{
        hostId: "z1", host: "sw-access", name: "sw-access", ip: "192.0.2.3",
        port: 10051, enabled: true, discovered: false, missing: false,
        missingSinceUtc: null, groups: ["Switches"], templates: ["Cisco IOS"],
        hasAsset: true, lastSyncedAtUtc: "2026-11-14T09:00:00.000Z",
      }],
      totalCount: 1, page: 1, pageSize: 20,
    });
    vi.mocked(getZabbixHostItems).mockResolvedValue([
      {
        itemId: "i1", name: "CPU utilization", key: "system.cpu.util", unit: "%",
        type: 0, enabled: true, unsupported: false,
        lastValue: "12.5", lastClockUtc: "2026-11-14T10:00:00.000Z",
      },
      {
        itemId: "i2", name: "Memória", key: "vm.memory.size", unit: "B",
        type: 0, enabled: false, unsupported: false, lastValue: "999", lastClockUtc: null,
      },
    ]);
    vi.mocked(getUniFiCorrelation).mockResolvedValue({
      matched: [{ mac: "aa:bb:cc:dd:ee:ff", deviceId: "dev-sw", deviceName: "sw-access", source: "sync", controllerPresent: true }],
      unlinked: [], conflicts: [],
    });
    vi.mocked(listSwitchPorts).mockResolvedValue(swPorts);

    renderPage();
    await screen.findByText("sw-access");
    fireEvent.click(screen.getByText("sw-access"));

    const panel = detailsPanel();
    // Ficha do inventário (DeviceItem) — sem dado inventado.
    expect(within(panel).getByText("aa:bb:cc:dd:ee:ff")).toBeInTheDocument();
    expect(within(panel).getByText("SN123")).toBeInTheDocument();
    expect(within(panel).getByText("C9200-48P")).toBeInTheDocument();
    expect(within(panel).getByText("Cisco")).toBeInTheDocument();
    expect(within(panel).getByText("VLAN 10 — Dados")).toBeInTheDocument();
    await within(panel).findByText("1d 1h");
    expect(within(panel).getByText(/Última comunicação/)).toBeInTheDocument();

    // Zabbix: host vinculado + métricas (item desativado fica de fora).
    await within(panel).findByText("CPU utilization: 12.5 %");
    expect(within(panel).queryByText(/Memória/)).toBeNull();
    expect(vi.mocked(getZabbixHostItems)).toHaveBeenCalledWith("z1");

    // UniFi: vínculo por MAC com estado do controller.
    expect(within(panel).getByText(/Vinculado \(sync\)/)).toBeInTheDocument();

    // Eventos: alertas do device com o total da página.
    await within(panel).findByText("Eventos recentes (7)");
    expect(within(panel).getByText(/Sem resposta há 5 min/)).toBeInTheDocument();

    // Portas do switch + o que está conectado (match nas duas pontas).
    await within(panel).findByText("Gi0/1 · Up · 1G · router-core · ge-0/0/0");
    expect(within(panel).getByText("Gi0/2 · Down · — · host-solt · eth0")).toBeInTheDocument();

    expect(within(panel).getByRole("button", { name: /ver device/i })).toBeInTheDocument();
  });

  it("should show connection details: ports, speed, VLANs, state, source and confidence", async () => {
    vi.mocked(listSwitchPorts).mockResolvedValue(swPorts);

    const { container } = renderPage();
    await screen.findByText("sw-access");

    // Hit-area invisível da e1 (router → switch).
    const hits = container.querySelectorAll('line[stroke="transparent"]');
    fireEvent.click(hits[0]!);

    const panel = detailsPanel();
    expect(within(panel).getByText("router-core")).toBeInTheDocument();
    expect(within(panel).getByText("ge-0/0/0")).toBeInTheDocument();
    expect(within(panel).getByText(/Destino/)).toBeInTheDocument();
    expect(within(panel).getByText("sw-access")).toBeInTheDocument();
    expect(within(panel).getByText("Gi0/1")).toBeInTheDocument();

    expect(within(panel).getByText("1G / 1G")).toBeInTheDocument();
    expect(within(panel).getByText("Full / Full")).toBeInTheDocument();
    expect(within(panel).getByText(/Tipo:/)).toBeInTheDocument();
    expect(within(panel).getByText("10 Dados")).toBeInTheDocument();

    // Estado da porta = linkUp real (aqui só a ponta destino tem SwitchPort).
    await within(panel).findByText("Up (ponta destino)");

    expect(within(panel).getByText("LLDP")).toBeInTheDocument();
    expect(within(panel).getByText("Confirmada")).toBeInTheDocument();
  });

  it("should stay honest without data: no Zabbix host, no UniFi link, no events, no type", async () => {
    const { container } = renderPage();
    await screen.findByText("host-solt");
    fireEvent.click(screen.getByText("host-solt"));

    const panel = detailsPanel();
    await within(panel).findByText("Sem host Zabbix vinculado");
    expect(within(panel).getByText("Sem vínculo UniFi")).toBeInTheDocument();
    expect(within(panel).getByText("Nenhum evento registrado")).toBeInTheDocument();
    expect(within(panel).getByText(/Uptime:/)).toBeInTheDocument();

    // e2 (switch → host): sem metadado de velocidade, origem manual.
    vi.mocked(listSwitchPorts).mockResolvedValue(swPorts);
    const hits = container.querySelectorAll('line[stroke="transparent"]');
    fireEvent.click(hits[1]!);

    const edgePanel = detailsPanel();
    // Velocidade e Duplex sem observação → ambos "— / —" (nada inventado).
    expect(within(edgePanel).getAllByText("— / —")).toHaveLength(2);
    expect(within(edgePanel).getByText("Manual")).toBeInTheDocument();
    expect(within(edgePanel).getByText("Estimada")).toBeInTheDocument();
    await within(edgePanel).findByText("Down (ponta origem)");

    // e3 (ghost): destino não é device gerenciado.
    fireEvent.click(hits[2]!);
    const ghostPanel = detailsPanel();
    expect(within(ghostPanel).getByText("Vizinho não gerenciado")).toBeInTheDocument();
    expect(within(ghostPanel).getByText("Gi1/0/48")).toBeInTheDocument();
  });
});

/**
 * F53-T006 — visão física ↔ lógica: alternância sem perder a organização
 * (mesmo mapa, filtros, seleção e pan/zoom). Lógico = faixas por VLAN
 * (união das pontas das Connections) + "Sem VLAN"; device em várias
 * VLANs aparece em cada faixa; aresta só entra nas faixas das VLANs
 * dela — sem dado de VLAN não dá pra posicionar (fica na física).
 */
describe("TopologyPage — visão física ↔ lógica (F53-T006)", () => {
  const logicalGraph = {
    nodes: [
      { id: "if-r1", deviceId: "dev-router", name: "ge-0/0/0", deviceName: "router-core", deviceType: "Router", ip: "192.0.2.1", status: "up" as const, isGhost: false, port: null, layer: 1 },
      { id: "if-f1", deviceId: "dev-fw", name: "eth0", deviceName: "fw-edge", deviceType: "Firewall", ip: "192.0.2.2", status: "up" as const, isGhost: false, port: null, layer: 1 },
      { id: "if-s1", deviceId: "dev-sw", name: "Gi0/1", deviceName: "sw-access", deviceType: "Switch", ip: "192.0.2.3", status: "up" as const, isGhost: false, port: null, layer: 2 },
      { id: "if-h1", deviceId: "dev-host", name: "eth0", deviceName: "host-solt", deviceType: "Server", ip: "192.0.2.4", status: null, isGhost: false, port: null, layer: null },
    ],
    edges: [
      { id: "e1", sourceInterfaceId: "if-r1", targetInterfaceId: "if-s1", isGhost: false, sourceVlans: [{ number: 10, name: "Dados" }], targetVlans: [{ number: 10, name: "Dados" }] },
      { id: "e2", sourceInterfaceId: "if-f1", targetInterfaceId: "if-s1", isGhost: false, sourceVlans: [{ number: 20, name: "Voz" }], targetVlans: [{ number: 20, name: "Voz" }] },
      { id: "e3", sourceInterfaceId: "if-r1", targetInterfaceId: "if-f1", isGhost: false },
    ],
  };

  const mkDevice = (over: Partial<DeviceItem> & { id: string }): DeviceItem => ({
    locationId: "loc-1",
    rackId: null,
    type: "Other",
    name: over.id,
    ip: null,
    mac: null,
    serial: null,
    model: null,
    vendor: null,
    vlanId: null,
    description: null,
    monitoringChecks: [],
    profileIsCustom: false,
    ...over,
  });

  beforeEach(() => {
    topologyMock.mockReset();
    vi.mocked(listDevices).mockReset();
    vi.mocked(listLocations).mockReset();
    vi.mocked(listVlans).mockReset();
    mockDetailDefaults();
    localStorage.clear();

    topologyMock.mockResolvedValue(logicalGraph);
    vi.mocked(listDevices).mockResolvedValue({
      items: [
        mkDevice({ id: "dev-router", name: "router-core", type: "Router", ip: "192.0.2.1" }),
        mkDevice({ id: "dev-fw", name: "fw-edge", type: "Firewall", ip: "192.0.2.2" }),
        mkDevice({ id: "dev-sw", name: "sw-access", type: "Switch", ip: "192.0.2.3" }),
        mkDevice({ id: "dev-host", name: "host-solt", type: "Server", ip: "192.0.2.4" }),
      ],
      totalCount: 4, page: 1, pageSize: 20,
    });
    vi.mocked(listLocations).mockResolvedValue({
      items: [{ id: "loc-1", name: "Datacenter", description: null }],
      totalCount: 1, page: 1, pageSize: 20,
    });
    vi.mocked(listVlans).mockResolvedValue({
      items: [
        { id: "v10", name: "Dados", vlanNumber: 10, description: null },
        { id: "v20", name: "Voz", vlanNumber: 20, description: null },
      ],
      totalCount: 2, page: 1, pageSize: 20,
    });
  });

  const renderPage = () =>
    render(
      <MemoryRouter>
        <TopologyPage />
      </MemoryRouter>,
    );

  it("should switch between physical layer bands and VLAN bands and back", async () => {
    renderPage();
    const mapa = await screen.findByTestId("topology-map");

    // Física (default): faixas por camada, nenhuma faixa de VLAN.
    expect(within(mapa).getByText("Camada 1 · 2 devices")).toBeInTheDocument();
    expect(within(mapa).getByText("Sem hierarquia · 1 device")).toBeInTheDocument();
    expect(within(mapa).queryByText("VLAN 10 — Dados · 2 devices")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Lógica" }));
    expect(await within(mapa).findByText("VLAN 10 — Dados · 2 devices")).toBeInTheDocument();
    expect(within(mapa).getByText("VLAN 20 — Voz · 2 devices")).toBeInTheDocument();
    expect(within(mapa).getByText("Sem VLAN · 1 device")).toBeInTheDocument();
    expect(within(mapa).queryByText(/Camada 1/)).toBeNull();
    expect(screen.getByRole("button", { name: "Lógica" })).toHaveAttribute("aria-pressed", "true");

    // Volta para a física com a organização original intacta.
    fireEvent.click(screen.getByRole("button", { name: "Física" }));
    expect(await within(mapa).findByText("Camada 1 · 2 devices")).toBeInTheDocument();
    expect(within(mapa).queryByText("VLAN 10 — Dados · 2 devices")).toBeNull();
    expect(screen.getByRole("button", { name: "Física" })).toHaveAttribute("aria-pressed", "true");
  });

  it("should repeat a multi-VLAN switch in each band and draw only placed links", async () => {
    const { container } = renderPage();
    const mapa = await screen.findByTestId("topology-map");
    await screen.findByText("sw-access");

    // Física: as 3 Connections aparecem.
    expect(container.querySelectorAll('line[stroke="#94a3b8"]')).toHaveLength(3);

    fireEvent.click(screen.getByRole("button", { name: "Lógica" }));
    await within(mapa).findByText("VLAN 10 — Dados · 2 devices");

    // Switch nas duas faixas (VLAN 10 e VLAN 20); host só em "Sem VLAN".
    expect(within(mapa).getAllByText("sw-access")).toHaveLength(2);
    expect(within(mapa).getAllByText("host-solt")).toHaveLength(1);

    // e1 no VLAN 10, e2 no VLAN 20; e3 sem VLAN com pontas em faixas
    // diferentes (r1{10} × fw{20}) não tem onde ficar — só na física.
    expect(container.querySelectorAll('line[stroke="#94a3b8"]')).toHaveLength(2);
  });

  it("should collapse one VLAN band without hiding the switch's copy elsewhere, keeping the selection", async () => {
    renderPage();
    const mapa = await screen.findByTestId("topology-map");
    await screen.findByText("sw-access");

    // Seleção feita na física sobrevive à troca de visão.
    fireEvent.click(screen.getByText("sw-access"));
    expect(screen.getByText("Detalhes")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Lógica" }));
    await within(mapa).findByText("VLAN 10 — Dados · 2 devices");
    expect(screen.getByText("Detalhes")).toBeInTheDocument();
    expect(within(mapa).getAllByText("sw-access")).toHaveLength(2);

    // Colapsa o VLAN 10: cópia do switch no VLAN 20 continua visível.
    const toggle = screen.getByRole("button", { name: "VLAN 10 — Dados" });
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(within(mapa).getAllByText("sw-access")).toHaveLength(1);
    expect(within(mapa).queryByText("router-core")).toBeNull();
    expect(within(mapa).getByText("fw-edge")).toBeInTheDocument();

    fireEvent.click(toggle);
    expect(within(mapa).getAllByText("sw-access")).toHaveLength(2);
    expect(within(mapa).getByText("router-core")).toBeInTheDocument();
  });
});
