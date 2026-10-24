import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { WhatsAppSettingsPage } from "./WhatsAppSettingsPage";
import {
  createWhatsAppInstance,
  generateWhatsAppQr,
  getWhatsAppConnectionState,
  getWhatsAppSettings,
  listWhatsAppGroups,
  saveWhatsAppSettings,
  sendWhatsAppTest,
  type WhatsAppSettings,
} from "@/services/api/whatsapp";

vi.mock("@/services/api/whatsapp", () => ({
  getWhatsAppSettings: vi.fn(),
  saveWhatsAppSettings: vi.fn(),
  getWhatsAppConnectionState: vi.fn(),
  generateWhatsAppQr: vi.fn(),
  disconnectWhatsAppInstance: vi.fn(),
  createWhatsAppInstance: vi.fn(),
  listWhatsAppGroups: vi.fn(),
  sendWhatsAppTest: vi.fn(),
}));

const settingsMock = vi.mocked(getWhatsAppSettings);
const connectionMock = vi.mocked(getWhatsAppConnectionState);
const saveMock = vi.mocked(saveWhatsAppSettings);
const qrMock = vi.mocked(generateWhatsAppQr);
const createInstanceMock = vi.mocked(createWhatsAppInstance);
const groupsMock = vi.mocked(listWhatsAppGroups);
const testMock = vi.mocked(sendWhatsAppTest);

const baseSettings: WhatsAppSettings = {
  baseUrl: "http://evolution-api:8080",
  apiKeySet: true,
  instanceId: "Admin",
  groupId: "120363000000000000@g.us",
  phone: null,
  connectionState: "open",
  source: "database",
  lastTestedAtUtc: null,
  lastTestOk: false,
  lastTestError: null,
  updatedAtUtc: null,
  notificationsEnabled: false,
};

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <WhatsAppSettingsPage />
    </QueryClientProvider>,
  );
}

/**
 * ADR-062 — segurança da ApiKey e QR base64.
 *
 * 1) A chave é write-only (cifrada AES-GCM no servidor): a UI só pode exibir
 *    o indicador `apiKeySet` e nunca re-popular o campo com o valor gravado.
 * 2) O QR vem como `byte[]` em base64 no JSON — exibir direto como texto
 *    seria inútil; precisa virar data-URI `image/png`.
 */
describe("WhatsAppSettingsPage (ADR-062)", () => {
  beforeEach(() => {
    settingsMock.mockReset();
    connectionMock.mockReset();
    saveMock.mockReset();
    qrMock.mockReset();
    createInstanceMock.mockReset();
    groupsMock.mockReset();
    testMock.mockReset();
    settingsMock.mockResolvedValue(baseSettings);
    connectionMock.mockResolvedValue({ state: "open" });
    createInstanceMock.mockResolvedValue({
      instanceId: "Admin",
      created: true,
      state: "close",
    });
  });

  it("should show apiKeySet as an indicator, never as a value", async () => {
    renderPage();

    // O indicador positivo…
    expect(await screen.findByText("ApiKey configurada")).toBeInTheDocument();
    expect(screen.getByText("Sim")).toBeInTheDocument();

    // …nunca um campo populado com a chave (write-only).
    const keyInput = screen.getByPlaceholderText("••••••••") as HTMLInputElement;
    expect(keyInput.value).toBe("");
    expect(screen.queryByDisplayValue("secret-key")).toBeNull();
  });

  it("should ask for the key only when none is configured", async () => {
    settingsMock.mockResolvedValue({ ...baseSettings, apiKeySet: false, groupId: null });
    renderPage();

    const field = await screen.findByText("ApiKey da Evolution");
    expect(field).toBeInTheDocument();
  });

  it("should render the QR png (base64) as an image data-URI", async () => {
    connectionMock.mockResolvedValue({ state: "close" });
    // 1x1 PNG em base64 — o DTO devolve `byte[]` serializado como base64.
    const pngBase64 =
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
    qrMock.mockResolvedValue({ png: pngBase64, pairingCode: "ABCD-EFGH", state: "connecting" });

    renderPage();
    await screen.findByRole("button", { name: /Gerar QR Code/ });

    fireEvent.click(screen.getByRole("button", { name: /Gerar QR Code/ }));

    const img = await screen.findByAltText(/QR Code de pareamento/);
    expect(img).toHaveAttribute("src", `data:image/png;base64,${pngBase64}`);
    expect(screen.getByText("ABCD-EFGH")).toBeInTheDocument();

    // Pode coexistir com o NoticeBanner (também role=status) — o bloco do QR
    // é quem carrega a instrução de escaneamento.
    const statuses = await screen.findAllByRole("status");
    expect(
      statuses.some((node) => /escaneie/i.test(node.textContent ?? "")),
    ).toBe(true);
  });

  it("should identify the created instance and its actual state", async () => {
    settingsMock.mockResolvedValue({ ...baseSettings, instanceId: "EquipeInfra" });
    createInstanceMock.mockResolvedValue({
      instanceId: "EquipeInfra",
      created: true,
      state: "close",
    });
    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: /Criar instância/ }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      /Instância\s*EquipeInfra\s*criada na Evolution\.\s*Estado:\s*close\./,
    );
  });

  it("should keep instance operations disabled until the configuration is persisted", async () => {
    settingsMock.mockResolvedValue({
      ...baseSettings,
      baseUrl: "",
      instanceId: "",
      apiKeySet: false,
      groupId: null,
      source: "environment",
    });
    renderPage();

    expect(await screen.findByRole("button", { name: /Criar instância/ })).toBeDisabled();
    expect(screen.getByRole("button", { name: /Gerar QR Code/ })).toBeDisabled();
    expect(screen.getByRole("button", { name: /Carregar grupos/ })).toBeDisabled();
  });

  it("should keep the selected group and send it on save", async () => {
    settingsMock.mockResolvedValue({ ...baseSettings, phone: "5511999990000" });
    saveMock.mockResolvedValue({ ...baseSettings, groupId: "novo-gupo@g.us", phone: "5511999990000" });
    renderPage();

    await screen.findByLabelText(/URL da Evolution API/);

    expect(screen.getByLabelText(/Telefone individual/)).toHaveValue("5511999990000");

    fireEvent.change(screen.getByLabelText(/Nome da instância/), {
      target: { value: "Admin" },
    });

    fireEvent.click(screen.getByRole("button", { name: /^Salvar$/ }));
    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(1));

    expect(saveMock).toHaveBeenCalledWith({
      baseUrl: "http://evolution-api:8080",
      instanceId: "Admin",
      // chave em branco → null = mantém a gravada no servidor.
      apiKey: null,
      groupId: "120363000000000000@g.us",
      phone: "5511999990000",
      notificationsEnabled: false,
    });
  });

  it("should ask before replacing the configured number and keep edits on cancel", async () => {
    settingsMock.mockResolvedValue({
      ...baseSettings,
      phone: "5511999990000",
      notificationsEnabled: true,
    });
    renderPage();

    const instanceInput = await screen.findByLabelText(/Nome da instância/);
    fireEvent.change(instanceInput, { target: { value: "NovoNumero" } });

    expect(screen.getByLabelText(/Telefone individual/)).toHaveValue("");
    expect(
      screen.getByRole("checkbox", {
        name: /Ativar envio automático de notificações/,
      }),
    ).toBeDisabled();
    expect(screen.getByRole("button", { name: /Criar instância/ })).toBeDisabled();

    fireEvent.click(screen.getByRole("button", { name: /^Salvar$/ }));
    expect(
      await screen.findByRole("dialog", { name: /Substituir número do WhatsApp/ }),
    ).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Continuar editando/ }));
    expect(saveMock).not.toHaveBeenCalled();
    expect(instanceInput).toHaveValue("NovoNumero");
  });

  it("should replace the number without carrying over the old destination or automation", async () => {
    settingsMock.mockResolvedValue({
      ...baseSettings,
      phone: "5511999990000",
      notificationsEnabled: true,
    });
    saveMock.mockResolvedValue({
      ...baseSettings,
      instanceId: "NovoNumero",
      groupId: null,
      phone: null,
      notificationsEnabled: false,
      connectionState: "close",
    });
    renderPage();

    fireEvent.change(await screen.findByLabelText(/Nome da instância/), {
      target: { value: "NovoNumero" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^Salvar$/ }));
    fireEvent.click(
      await screen.findByRole("button", { name: /Substituir configuração/ }),
    );

    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(1));
    expect(saveMock).toHaveBeenCalledWith({
      baseUrl: "http://evolution-api:8080",
      instanceId: "NovoNumero",
      apiKey: null,
      groupId: null,
      phone: null,
      notificationsEnabled: false,
    });
  });

  it("should persist the WhatsApp notification activation choice", async () => {
    saveMock.mockResolvedValue({ ...baseSettings, notificationsEnabled: true });
    renderPage();
    const enabled = await screen.findByRole("checkbox", {
      name: /Ativar envio automático de notificações/,
    });
    fireEvent.click(enabled);
    fireEvent.click(screen.getByRole("button", { name: /^Salvar$/ }));

    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(1));
    expect(saveMock).toHaveBeenCalledWith(
      expect.objectContaining({ notificationsEnabled: true }),
    );
  });

  it("should disable save while baseUrl is missing", async () => {
    settingsMock.mockResolvedValue({ ...baseSettings, baseUrl: "" });
    renderPage();

    const save = await screen.findByRole("button", { name: /^Salvar$/ });
    expect(save).toBeDisabled();
  });

  it("should load groups and reflect them as selectable destinations", async () => {
    groupsMock.mockResolvedValue([
      {
        id: "120363000000000000@g.us",
        subject: "Equipe atual",
        size: 8,
        owner: null,
        desc: null,
        restrict: false,
        announce: false,
      },
      {
        id: "120363000000000001@g.us",
        subject: "Estudo Arquitetura",
        size: 12,
        owner: "5511999990000",
        desc: null,
        restrict: false,
        announce: false,
      },
      {
        id: "120363000000000002@g.us",
        subject: "Operações",
        size: 5,
        owner: null,
        desc: null,
        restrict: true,
        announce: false,
      },
    ]);

    renderPage();
    await screen.findByRole("button", { name: /Carregar grupos/ });
    fireEvent.click(screen.getByRole("button", { name: /Carregar grupos/ }));

    expect(await screen.findByText("Estudo Arquitetura")).toBeInTheDocument();
    expect(screen.getByText("Operações")).toBeInTheDocument();

    // Grupo atual vindo do banco fica selecionado.
    const current = screen.getByRole("radio", { checked: true });
    expect(current).toBeInTheDocument();
  });

  it("should show an explicit failure when the test send fails", async () => {
    testMock.mockResolvedValue({ ok: false, error: "Grupo inexistente." });
    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: /mensagem de teste/ }));

    const status = await screen.findByRole("status");
    await waitFor(() => expect(status).toHaveTextContent(/Grupo inexistente\./));
  });
});
