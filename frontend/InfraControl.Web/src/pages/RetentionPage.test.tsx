import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RetentionPage } from "./RetentionPage";
import {
  deleteRetentionPolicy,
  getRetentionPolicies,
  upsertRetentionPolicy,
  type RetentionPolicyItem,
} from "@/services/api/retention";

vi.mock("@/services/api/retention", () => ({
  getRetentionPolicies: vi.fn(),
  upsertRetentionPolicy: vi.fn(),
  deleteRetentionPolicy: vi.fn(),
}));

const listMock = vi.mocked(getRetentionPolicies);
const upsertMock = vi.mocked(upsertRetentionPolicy);
const deleteMock = vi.mocked(deleteRetentionPolicy);

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
});
