import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { VirtualizationPage } from "./VirtualizationPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { getVirtualizationOverview } from "@/services/api/virtualization";

/**
 * F31-T004 (docs/53 §4.4) — página Virtualização no padrão EntityView
 * (tabela/grid/kanban — decisão F26 "sempre os 3 modos") sobre o
 * read-model de `GET /inventory/virtualization` (F31-T003).
 *
 * O que estes testes trançam:
 *  - as contagens no topo (VMs / vCenters / Órfãs) nunca mentem;
 *  - hypervisor e vCenter aparecem com o NOME resolvido, não o guid;
 *  - a VM sem vínculo é sinalizada como Órfã;
 *  - base vazia mostra o vazio declarado, sem contagem inventada.
 */
vi.mock("@/services/api/virtualization", async (importOriginal) => {
  const actual =
    await importOriginal<typeof import("@/services/api/virtualization")>();
  return { ...actual, getVirtualizationOverview: vi.fn() };
});

const overviewMock = vi.mocked(getVirtualizationOverview);

/** Mesmo espelho do seed da T003: esx-01 ← vm-a/vm-b; vcenter-01 ← vm-a/vm-c; vm-o órfã. */
const overviewFixture = {
  totalVirtualMachines: 4,
  totalVCenters: 1,
  orphanVirtualMachines: 1,
  byHypervisor: [
    {
      deviceId: "7d000000-0000-0000-0000-00000000e001",
      name: "esx-01",
      virtualMachineCount: 2,
    },
  ],
  byVCenter: [
    {
      deviceId: "7d000000-0000-0000-0000-00000000f001",
      name: "vcenter-01",
      virtualMachineCount: 2,
    },
  ],
  virtualMachines: [
    {
      id: "7d000000-0000-0000-0000-000000000001",
      name: "vm-a",
      ip: "10.0.10.11",
      hypervisorId: "7d000000-0000-0000-0000-00000000e001",
      hypervisorName: "esx-01",
      vCenterId: "7d000000-0000-0000-0000-00000000f001",
      vCenterName: "vcenter-01",
      isOrphan: false,
    },
    {
      id: "7d000000-0000-0000-0000-000000000002",
      name: "vm-b",
      ip: null,
      hypervisorId: "7d000000-0000-0000-0000-00000000e001",
      hypervisorName: "esx-01",
      vCenterId: null,
      vCenterName: null,
      isOrphan: false,
    },
    {
      id: "7d000000-0000-0000-0000-000000000003",
      name: "vm-c",
      ip: null,
      hypervisorId: null,
      hypervisorName: null,
      vCenterId: "7d000000-0000-0000-0000-00000000f001",
      vCenterName: "vcenter-01",
      isOrphan: false,
    },
    {
      id: "7d000000-0000-0000-0000-000000000004",
      name: "vm-o",
      ip: null,
      hypervisorId: null,
      hypervisorName: null,
      vCenterId: null,
      vCenterName: null,
      isOrphan: true,
    },
  ],
};

const emptyOverview = {
  totalVirtualMachines: 0,
  totalVCenters: 0,
  orphanVirtualMachines: 0,
  byHypervisor: [],
  byVCenter: [],
  virtualMachines: [],
};

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "admin@exemplo.com",
      displayName: "Admin",
      roles: ["Admin"],
    }),
  );
  overviewMock.mockResolvedValue(overviewFixture);
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <VirtualizationPage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("Virtualização — contagens no topo (F31-T004)", () => {
  it("should count VMs, vCenters and orphans from the overview", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Virtualização" }),
    ).toBeInTheDocument();
    // Subtítulo da casca (countLabel): "4 VMs".
    expect(await screen.findByText("4 VMs")).toBeInTheDocument();
    // Faixa de contagens — label+valor no mesmo node (molde S5b).
    expect(await screen.findByText("VMs: 4")).toBeInTheDocument();
    expect(await screen.findByText("vCenters: 1")).toBeInTheDocument();
    expect(await screen.findByText("Órfãs: 1")).toBeInTheDocument();
    expect(overviewMock).toHaveBeenCalledTimes(1);
  });
});

describe("Virtualização — vínculos resolvidos por nome (F31-T004)", () => {
  it("should show hypervisor and vCenter names, never raw guids", async () => {
    renderPage();

    expect(
      await screen.findByRole("columnheader", { name: "Hypervisor" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("columnheader", { name: "vCenter" }),
    ).toBeInTheDocument();
    expect((await screen.findAllByText("esx-01")).length).toBeGreaterThan(0);
    expect((await screen.findAllByText("vcenter-01")).length).toBeGreaterThan(0);
    // Os guids do read-model nunca aparecem na tela.
    expect(screen.queryByText(/7d000000-0000-0000-0000-00000000/)).toBeNull();
  });

  it("should flag the VM without links as orpha", async () => {
    renderPage();

    // Só vm-o é órfã — exatamente um selo.
    const badges = await screen.findAllByText("Órfã");
    expect(badges).toHaveLength(1);
  });
});

describe("Virtualização — base vazia (F31-T004)", () => {
  it("should show the declared empty state without inventing counts", async () => {
    overviewMock.mockResolvedValue(emptyOverview);
    renderPage();

    expect(await screen.findByText("0 VMs")).toBeInTheDocument();
    expect(await screen.findByText("VMs: 0")).toBeInTheDocument();
    expect(await screen.findByText("Órfãs: 0")).toBeInTheDocument();
    expect(
      await screen.findByText("Nenhuma VM cadastrada"),
    ).toBeInTheDocument();
  });
});
