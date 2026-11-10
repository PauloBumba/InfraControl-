import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { BackupSettingsPage } from "./BackupSettingsPage";
import {
  getBackupSettings,
  updateBackupSettings,
  type BackupSettings,
} from "@/services/api/accessPolicies";

vi.mock("@/services/api/accessPolicies", () => ({
  getBackupSettings: vi.fn(),
  updateBackupSettings: vi.fn(),
}));

const getMock = vi.mocked(getBackupSettings);
const putMock = vi.mocked(updateBackupSettings);

const SETTINGS: BackupSettings = {
  enabled: true,
  intervalHours: 24,
  retentionDays: 7,
  path: "/var/backups",
  fileNamePrefix: "infracontrol",
  timeoutSeconds: 3600,
};

function renderPage() {
  return render(<BackupSettingsPage />);
}

describe("BackupSettingsPage — padrão da casa", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getMock.mockResolvedValue(SETTINGS);
    putMock.mockResolvedValue(SETTINGS);
  });

  it("should render PageHeader with the house title", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Backup do banco de dados" }),
    ).toBeInTheDocument();
    expect(getMock).toHaveBeenCalledTimes(1);
  });

  it("should not use raw ProgressSpinner while loading", () => {
    getMock.mockReturnValue(new Promise(() => {}));
    const { container } = renderPage();

    expect(container.querySelector(".p-progress-spinner")).toBeNull();
    expect(screen.getByText("Carregando…")).toBeInTheDocument();
  });

  it("should save via the API with the house Button", async () => {
    renderPage();
    const user = userEvent.setup();

    await screen.findByRole("heading", { name: "Backup do banco de dados" });
    const path = screen.getByLabelText(/Diretório de destino/);
    await user.clear(path);
    await user.type(path, "/tmp/backup");

    await user.click(screen.getByRole("button", { name: /Salvar/ }));

    await waitFor(() => expect(putMock).toHaveBeenCalledTimes(1));
    expect(putMock.mock.calls[0]![0]).toMatchObject({ path: "/tmp/backup" });
    expect(await screen.findByText("Configurações salvas.")).toBeInTheDocument();
  });
});
