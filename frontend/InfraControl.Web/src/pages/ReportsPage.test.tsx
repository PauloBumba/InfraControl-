import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { ReportsPage } from "./ReportsPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import {
  exportTransversalReport,
  getTransversalReport,
  type TransversalReport,
} from "@/services/api/reports";

/** ADR-049 · R-d — ReportsPage consome o read-model transversal. */
vi.mock("@/services/api/reports", () => ({
  getTransversalReport: vi.fn(),
  exportTransversalReport: vi.fn(),
}));

const getReportMock = vi.mocked(getTransversalReport);
const exportMock = vi.mocked(exportTransversalReport);

const sample: TransversalReport = {
  from: null,
  to: null,
  pdi: { total: 12, byStatus: { Completed: 5, Active: 7 }, completed: 5 },
  tasks: { total: 40, byState: { Closed: 20, Open: 20 }, completed: 20, overdue: 3 },
  knowledge: { total: 10, published: 8, draft: 2 },
  projects: { total: 4, byStatus: { InProgress: 2, Completed: 2 } },
  incidents: { total: 9, resolved: 6, averageResolutionHours: 2.5 },
};

beforeEach(() => {
  vi.clearAllMocks();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Operator"],
    }),
  );
});

function renderPage() {
  return render(
    <Provider store={store}>
      <ReportsPage />
    </Provider>,
  );
}

describe("ReportsPage — relatório transversal", () => {
  it("should load and render the aggregated counts", async () => {
    getReportMock.mockResolvedValue(sample);
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: /gerar relatório/i }));

    expect(await screen.findByText("Produtividade (PDI)")).toBeInTheDocument();
    expect(screen.getByText("40")).toBeInTheDocument(); // tasks total
    expect(screen.getByText("2.5 h")).toBeInTheDocument(); // média de resolução
    expect(getReportMock).toHaveBeenCalledWith(null, null);
  });

  it("should pass the period filters to the query", async () => {
    getReportMock.mockResolvedValue(sample);
    renderPage();
    const user = userEvent.setup();

    const from = screen.getByLabelText("De");
    const to = screen.getByLabelText("Até");
    await user.type(from, "2026-01-01");
    await user.type(to, "2026-06-30");
    await user.click(screen.getByRole("button", { name: /gerar relatório/i }));

    await screen.findByText("Produtividade (PDI)");
    expect(getReportMock).toHaveBeenCalledWith("2026-01-01", "2026-06-30");
  });

  it("should export via the service", async () => {
    exportMock.mockResolvedValue(undefined);
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: /xlsx/i }));
    expect(exportMock).toHaveBeenCalledWith(null, null, "xlsx");
  });
});
