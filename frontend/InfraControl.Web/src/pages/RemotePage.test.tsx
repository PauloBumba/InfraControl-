import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RemotePage } from "./RemotePage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listDevices, type DeviceItem } from "@/services/api/inventory";

/**
 * F29-T001C-3 — RemotePage na casca padrão (`ListPageShell`).
 *
 * O que este teste tranca: o subtítulo sai de
 * `countLabel(total, "dispositivo", "dispositivos") · "selecione um …"` — a
 * nota é literal e entra pelo `note` do shell com o mesmo "·" de produção.
 */
vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return { ...actual, listDevices: vi.fn() };
});

const listDevicesMock = vi.mocked(listDevices);

const umDevice: DeviceItem = {
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

const NOTE = "selecione um para ver e abrir sessões";

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Operator"],
    }),
  );
  listDevicesMock.mockResolvedValue({ items: [], totalCount: 3, page: 1, pageSize: 100 });
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <RemotePage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("RemotePage — casca da lista (C-3: ListPageShell)", () => {
  it("should compose the count with the literal note", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Acesso Remoto" }),
    ).toBeInTheDocument();
    expect(await screen.findByText(`3 dispositivos · ${NOTE}`)).toBeInTheDocument();
  });

  it("should pluralize the head count without touching the note", async () => {
    listDevicesMock.mockResolvedValue({
      items: [umDevice],
      totalCount: 1,
      page: 1,
      pageSize: 100,
    });
    renderPage();

    expect(await screen.findByText(`1 dispositivo · ${NOTE}`)).toBeInTheDocument();
    expect(screen.queryByText(`1 dispositivos · ${NOTE}`)).toBeNull();
  });
});
