import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { SettingsPage } from "./SettingsPage";
import {
  getAccountSettings,
  updateAccountSettings,
  type UserSettings,
} from "@/services/api/settings";

/**
 * Fase 06d — SettingsPage religada: os testes travam (1) o carregamento
 * real, (2) o upsert no save, (3) o estado de erro de load e (4) o link
 * para a página de Notificações.
 */
vi.mock("@/services/api/settings", () => ({
  getAccountSettings: vi.fn(),
  updateAccountSettings: vi.fn(),
}));

const getAccountSettingsMock = vi.mocked(getAccountSettings);
const updateAccountSettingsMock = vi.mocked(updateAccountSettings);

const DEFAULT_SETTINGS: UserSettings = {
  userId: "3f1a1111-2222-3333-4444-555566667777",
  timezone: "America/Sao_Paulo",
  darkMode: true,
  autoRefresh: true,
  telemetry: false,
};

beforeEach(() => {
  vi.clearAllMocks();
  getAccountSettingsMock.mockResolvedValue(DEFAULT_SETTINGS);
  updateAccountSettingsMock.mockImplementation(async (payload) => ({
    ...DEFAULT_SETTINGS,
    ...payload,
  }));
});

function renderPage() {
  return render(
    <MemoryRouter>
      <SettingsPage />
    </MemoryRouter>,
  );
}

describe("SettingsPage — preferências reais", () => {
  it("should load settings from the API on mount", async () => {
    renderPage();

    expect(await screen.findByText("Preferências")).toBeInTheDocument();
    expect(getAccountSettingsMock).toHaveBeenCalledTimes(1);
    expect(
      screen.getByRole("switch", { name: "Atualização Automática" }),
    ).toBeInTheDocument();
  });

  /**
   * ADR-061 §4 (M6): o toggle "Modo Escuro" era função morta — persistia
   * darkMode no backend mas nenhum consumidor aplica tema. Foi OCULTADO;
   * theming exige ADR próprio. O valor continua round-trip na API.
   */
  it("does not show the dead dark-mode toggle (M6)", async () => {
    renderPage();

    await screen.findByText("Preferências");
    expect(screen.queryByRole("switch", { name: "Modo Escuro" })).toBeNull();
  });

  it("should save toggled preferences via PUT", async () => {
    renderPage();
    const user = userEvent.setup();

    await screen.findByText("Preferências");
    await user.click(screen.getByRole("switch", { name: "Telemetria" }));
    await user.click(screen.getByRole("button", { name: /Salvar preferências/ }));

    await waitFor(() => {
      expect(updateAccountSettingsMock).toHaveBeenCalledWith({
        timezone: "America/Sao_Paulo",
        darkMode: true,
        autoRefresh: true,
        telemetry: true,
      });
    });
    expect(await screen.findByText("Preferências salvas.")).toBeInTheDocument();
  });

  it("should show the error banner when load fails", async () => {
    getAccountSettingsMock.mockRejectedValue(new Error("offline"));

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent("offline");
    expect(updateAccountSettingsMock).not.toHaveBeenCalled();
  });

  it("should show the save error and not the success notice", async () => {
    updateAccountSettingsMock.mockRejectedValue(new Error("salvamento falhou"));

    renderPage();
    const user = userEvent.setup();

    await screen.findByText("Preferências");
    await user.click(screen.getByRole("button", { name: /Salvar preferências/ }));

    expect(await screen.findByRole("alert")).toHaveTextContent("salvamento falhou");
    expect(screen.queryByText("Preferências salvas.")).not.toBeInTheDocument();
  });

  it("should link to the notifications page from its tab", async () => {
    renderPage();
    const user = userEvent.setup();

    await screen.findByText("Preferências");
    await user.click(screen.getByRole("button", { name: /Notificações/ }));

    expect(
      screen.getByRole("button", { name: /Abrir Notificações/ }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/preferências de entrega/, { selector: "p" }),
    ).toBeInTheDocument();
  });
});
