import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RetentionPage } from "./RetentionPage";
import {
  deleteRetentionPolicy,
  exportRetentionPolicies,
  getRetentionPolicies,
  importRetentionPolicies,
  upsertRetentionPolicy,
  type RetentionPolicyItem,
} from "@/services/api/retention";

vi.mock("@/services/api/retention", () => ({
  getRetentionPolicies: vi.fn(),
  upsertRetentionPolicy: vi.fn(),
  deleteRetentionPolicy: vi.fn(),
  exportRetentionPolicies: vi.fn(),
  importRetentionPolicies: vi.fn(),
}));

const listMock = vi.mocked(getRetentionPolicies);
const upsertMock = vi.mocked(upsertRetentionPolicy);
const deleteMock = vi.mocked(deleteRetentionPolicy);
const exportMock = vi.mocked(exportRetentionPolicies);
const importMock = vi.mocked(importRetentionPolicies);

const POLICIES: RetentionPolicyItem[] = [
  { dataType: "AuditEntries", days: 90, source: "Policy" },
  { dataType: "Notifications", days: 30, source: "Config" },
];

function renderPage() {
  return render(<RetentionPage />);
}

describe("RetentionPage — padrão da casa", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listMock.mockResolvedValue(POLICIES);
    upsertMock.mockResolvedValue(POLICIES[0]!);
    deleteMock.mockResolvedValue(undefined);
    exportMock.mockResolvedValue(new Blob(['{"policies":[]}']));
    importMock.mockResolvedValue(1);
  });

  it("should use ListPageShell title and list the policies", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Retenção de dados" }),
    ).toBeInTheDocument();
    expect(await screen.findByText("Registros de auditoria")).toBeInTheDocument();
    expect(screen.getByText("Política")).toBeInTheDocument();
    expect(screen.getByText("Padrão")).toBeInTheDocument();
  });

  it("should not use raw ProgressSpinner while loading", () => {
    listMock.mockReturnValue(new Promise(() => {}));
    const { container } = renderPage();

    expect(container.querySelector(".p-progress-spinner")).toBeNull();
    expect(screen.getByText("Carregando…")).toBeInTheDocument();
  });

  it("should save a policy via the API", async () => {
    renderPage();
    const user = userEvent.setup();

    await screen.findByRole("heading", { name: "Retenção de dados" });
    const row = screen.getByText("Registros de auditoria").closest("tr");
    expect(row).not.toBeNull();
    const input = row!.querySelector("input") as HTMLInputElement;
    // type="number" não recebe texto via user.type em jsdom — change direto
    await user.clear(input);
    fireEvent.change(input, { target: { value: "120" } });

    const saveBtn = row!.querySelector(
      "button[title='Salvar política']",
    ) as HTMLButtonElement;
    await user.click(saveBtn);

    await waitFor(() => expect(upsertMock).toHaveBeenCalledWith("AuditEntries", 120));
  });

  it("should label managed types and the BackupSettings source (F50)", async () => {
    listMock.mockResolvedValue([
      { dataType: "Grafana", days: 30, source: "Config" },
      { dataType: "Databases", days: 365, source: "Config" },
      { dataType: "Backups", days: 7, source: "BackupSettings" },
    ]);
    renderPage();

    expect(await screen.findByText("Métricas do Grafana")).toBeInTheDocument();
    expect(await screen.findByText("Dados dos bancos da stack")).toBeInTheDocument();
    expect(await screen.findByText("Arquivos de backup")).toBeInTheDocument();
    expect(screen.getAllByText("Gerenciada")).toHaveLength(2);
    expect(screen.getByText("Backup")).toBeInTheDocument();
    expect(screen.getAllByText("Padrão")).toHaveLength(2);
  });

  it("should save the Backups row into BackupSettings via the same endpoint", async () => {
    listMock.mockResolvedValue([
      { dataType: "Backups", days: 7, source: "BackupSettings" },
    ]);
    renderPage();
    const user = userEvent.setup();

    const row = (await screen.findByText("Arquivos de backup")).closest("tr");
    const input = row!.querySelector("input") as HTMLInputElement;
    await user.clear(input);
    fireEvent.change(input, { target: { value: "30" } });
    await user.click(row!.querySelector("button[title='Salvar política']")!);

    await waitFor(() => expect(upsertMock).toHaveBeenCalledWith("Backups", 30));
  });

  it("should export the effective policies as a JSON download (F50)", async () => {
    const createObjectURL = vi.fn(() => "blob:retention");
    const revokeObjectURL = vi.fn();
    URL.createObjectURL = createObjectURL as unknown as typeof URL.createObjectURL;
    URL.revokeObjectURL = revokeObjectURL;
    renderPage();
    const user = userEvent.setup();

    await screen.findByRole("heading", { name: "Retenção de dados" });
    await user.click(screen.getByTitle("Exportar políticas (JSON)"));

    await waitFor(() => expect(exportMock).toHaveBeenCalled());
    await waitFor(() => expect(createObjectURL).toHaveBeenCalled());
  });

  it("should import policies from a JSON file and show the count (F50)", async () => {
    const { container } = renderPage();
    await screen.findByRole("heading", { name: "Retenção de dados" });

    const file = new File(
      [JSON.stringify({ policies: [{ dataType: "Grafana", days: 45 }] })],
      "retention.json",
      { type: "application/json" },
    );
    const input = container.querySelector("input[type=file]") as HTMLInputElement;
    expect(input).not.toBeNull();
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() =>
      expect(importMock).toHaveBeenCalledWith([{ dataType: "Grafana", days: 45 }]),
    );
    expect(
      await screen.findByText(/2 políticas · 1 política importada\./),
    ).toBeInTheDocument();
  });
});
