import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { EmailSettingsPage } from "./EmailSettingsPage";
import {
  getEmailConfig,
  saveEmailConfig,
  testEmail,
  type EmailSettings,
} from "@/services/api/email";

vi.mock("@/services/api/email", () => ({
  getEmailConfig: vi.fn(),
  saveEmailConfig: vi.fn(),
  testEmail: vi.fn(),
}));

const getConfigMock = vi.mocked(getEmailConfig);
const saveMock = vi.mocked(saveEmailConfig);
const testMock = vi.mocked(testEmail);

const configuredSettings: EmailSettings = {
  host: "smtp.example.test",
  port: 587,
  username: "smtp-user",
  passwordSet: true,
  from: "noreply@example.test",
  fromName: "InfraControl",
  useSsl: true,
  notificationsEnabled: null,
  source: "database",
  lastTestedAtUtc: "2026-10-23T12:00:00Z",
  lastTestOk: true,
  lastTestError: null,
  updatedAtUtc: "2026-10-23T12:00:00Z",
};

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <EmailSettingsPage />
    </QueryClientProvider>,
  );
}

/**
 * F41-T003 — segurança da senha SMTP (write-only, AES-GCM no servidor).
 * A UI só pode exibir o indicador `passwordSet` e nunca re-popular o
 * campo com o valor gravado — mesmo contrato do Zabbix/WhatsApp.
 */
describe("EmailSettingsPage (F41-T003)", () => {
  beforeEach(() => {
    getConfigMock.mockReset();
    saveMock.mockReset();
    testMock.mockReset();
    getConfigMock.mockResolvedValue(configuredSettings);
  });

  it("should show passwordSet as an indicator, never as a value", async () => {
    renderPage();

    expect(
      await screen.findByText("Senha (deixe em branco para manter a atual)")
    ).toBeInTheDocument();
    expect(screen.getByText("Sim")).toBeInTheDocument();

    const passwordInput = screen.getByPlaceholderText("••••••••") as HTMLInputElement;
    expect(passwordInput.value).toBe("");
    expect(screen.queryByDisplayValue("plain-secret")).toBeNull();
  });

  it("should ask for the password only when none is configured", async () => {
    getConfigMock.mockResolvedValue({ ...configuredSettings, passwordSet: false });

    renderPage();

    expect(await screen.findByText("Senha SMTP")).toBeInTheDocument();
    expect(screen.queryByPlaceholderText("••••••••")).toBeNull();
  });

  it("should omit empty password on save (keep current)", async () => {
    saveMock.mockResolvedValue(configuredSettings);

    renderPage();

    fireEvent.change(await screen.findByPlaceholderText("smtp.example.com"), {
      target: { value: "smtp2.example.test" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Salvar" }));

    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(1));
    expect(saveMock).toHaveBeenCalledWith(
      expect.objectContaining({
        host: "smtp2.example.test",
        password: null,
        from: "noreply@example.test",
        useSsl: true,
      }),
    );
  });

  it("should display test failure without breaking the form", async () => {
    testMock.mockResolvedValue({ ok: false, error: "Connection refused" });

    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Testar envio" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Falha: Connection refused",
    );
    expect(screen.getByRole("button", { name: "Salvar" })).toBeInTheDocument();
  });

  it("should display test success message", async () => {
    testMock.mockResolvedValue({ ok: true, error: null });

    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Testar envio" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "E-mail de teste enviado com sucesso",
    );
  });
});
