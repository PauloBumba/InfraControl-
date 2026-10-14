import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SwitchesPage } from "./SwitchesPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { listLocations } from "@/services/api/inventory";
import { listSwitches } from "@/services/api/network";

/**
 * F29-T001C-3 — SwitchesPage na casca padrão (`ListPageShell`).
 *
 * O que este teste tranca: o subtítulo é sempre "N switches" — sem ramo de
 * loading e sem regra de plural (a produção diz "1 switches", erro de
 * gramática pré-existente que o C-3 preserva de propósito: corrigir muda
 * texto visível e é decisão humana).
 */
vi.mock("@/services/api/network", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/network")>();
  return { ...actual, listSwitches: vi.fn() };
});

vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return { ...actual, listLocations: vi.fn() };
});

const listSwitchesMock = vi.mocked(listSwitches);
const listLocationsMock = vi.mocked(listLocations);

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
  listLocationsMock.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 100 });
  listSwitchesMock.mockResolvedValue({ items: [], totalCount: 3, page: 1, pageSize: 100 });
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <SwitchesPage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("SwitchesPage — casca da lista (C-3: ListPageShell)", () => {
  it("should count switches without a plural rule", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Switches & Portas" }),
    ).toBeInTheDocument();
    expect(await screen.findByText("3 switches")).toBeInTheDocument();
  });

  it("should keep the pre-existing singular wording byte-identical", async () => {
    listSwitchesMock.mockResolvedValue({ items: [], totalCount: 1, page: 1, pageSize: 100 });
    renderPage();

    // A produção diz "1 switches" (gramática errada, pré-existente). O C-3 só
    // troca a casca — corrigir para "1 switch" muda texto visível e fica para
    // decisão humana.
    expect(await screen.findByText("1 switches")).toBeInTheDocument();
    expect(screen.queryByText("1 switch")).toBeNull();
  });
});
