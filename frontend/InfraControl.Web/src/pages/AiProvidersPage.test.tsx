import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { AiProvidersPage } from "./AiProvidersPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import {
  getAiProviders,
  createAiProvider,
  listAiProviderModels,
  setAiProviderActive,
  testAiProviderConnection,
  type AiProviderList,
} from "@/services/api/aiProviders";

/**
 * FAI-T011b (ADR-057) — página Admin → Providers de IA.
 * Cobre: chips de origem (cadastro × config fallback), máscara de chave,
 * escrita atrás de level administer (Admin vê, Viewer não), teste de
 * conexão (§13) e criar provider. Mock do módulo de serviço (padrão
 * ZabbixHostsPage.test).
 */
vi.mock("@/services/api/aiProviders", () => ({
  getAiProviders: vi.fn(),
  createAiProvider: vi.fn(),
  updateAiProvider: vi.fn(),
  setAiProviderActive: vi.fn(),
  upsertAiProviderCapability: vi.fn(),
  deleteAiProviderCapability: vi.fn(),
  testAiProviderConnection: vi.fn(),
  listAiProviderModels: vi.fn(),
}));

const getAiProvidersMock = vi.mocked(getAiProviders);
const createAiProviderMock = vi.mocked(createAiProvider);
const setAiProviderActiveMock = vi.mocked(setAiProviderActive);
const testAiProviderConnectionMock = vi.mocked(testAiProviderConnection);

function makeList(overrides: Partial<AiProviderList> = {}): AiProviderList {
  return {
    providers: [
      {
        id: "11111111-1111-1111-1111-111111111111",
        name: "ollama",
        baseUrl: "http://ollama:11434",
        active: true,
        apiKeySet: true,
        apiKeyMasked: "••••cret",
        models: ["llama3.2", "qwen"],
      },
    ],
    capabilities: [
      {
        capability: "Copilot",
        providerId: "11111111-1111-1111-1111-111111111111",
        providerName: "ollama",
        providerActive: true,
        fallbackProviderId: null,
        fallbackProviderName: null,
        fallbackProviderActive: false,
        defaultModel: "llama3.2",
        fallbackModel: null,
      },
      {
        capability: "Reporting",
        providerId: null,
        providerName: null,
        providerActive: false,
        fallbackProviderId: null,
        fallbackProviderName: null,
        fallbackProviderActive: false,
        defaultModel: "",
        fallbackModel: null,
      },
    ],
    availableAdapterNames: ["ollama", "openai", "gemini"],
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <AiProvidersPage />
      </QueryClientProvider>
    </Provider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Admin"],
    }),
  );
  getAiProvidersMock.mockResolvedValue(makeList());
  createAiProviderMock.mockResolvedValue({
    id: "22222222-2222-2222-2222-222222222222",
    name: "openai",
    baseUrl: "https://api.openai.com",
    active: true,
    apiKeySet: false,
    apiKeyMasked: null,
    models: [],
  });
  setAiProviderActiveMock.mockResolvedValue({
    id: "11111111-1111-1111-1111-111111111111",
    name: "ollama",
    baseUrl: "http://ollama:11434",
    active: false,
    apiKeySet: true,
    apiKeyMasked: "••••cret",
    models: ["llama3.2", "qwen"],
  });
  testAiProviderConnectionMock.mockResolvedValue({ ok: true, error: null });
});

describe("AiProvidersPage", () => {
  it("lista providers com chave mascarada e chips de origem por capacidade", async () => {
    renderPage();

    expect((await screen.findAllByText("ollama")).length).toBeGreaterThan(0);
    expect(screen.getByText("••••cret")).toBeInTheDocument();
    expect(screen.getByText("cadastro")).toBeInTheDocument();
    expect(screen.getByText("config fallback")).toBeInTheDocument();
    expect(screen.getByText("Reporting")).toBeInTheDocument();
  });

  it("oferece o form de cadastro com os adaptadores registrados", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Cadastrar provider" }));

    const select = await screen.findByRole("combobox", { name: /Adaptador/ });
    expect(within(select).getByRole("option", { name: "ollama" })).toBeInTheDocument();
    expect(within(select).getByRole("option", { name: "openai" })).toBeInTheDocument();
  });

  it("cria um provider pelo form (apiKey opcional)", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Cadastrar provider" }));
    await user.selectOptions(
      await screen.findByRole("combobox", { name: /Adaptador/ }),
      "openai",
    );
    await user.type(
      screen.getByPlaceholderText("http://ollama:11434"),
      "https://api.openai.com",
    );
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(await screen.findByText(/"openai" cadastrado/)).toBeInTheDocument();
    expect(createAiProviderMock.mock.calls[0]?.[0]).toEqual({
      name: "openai",
      baseUrl: "https://api.openai.com",
      apiKey: null,
      models: null,
    });
  });

  it("desativa um provider e avisa sobre a mudança de resolve", async () => {
    const user = userEvent.setup();
    renderPage();

    const button = await screen.findByRole("button", { name: "Desativar" });
    await user.click(button);

    expect(await screen.findByText(/desativado/)).toBeInTheDocument();
    expect(setAiProviderActiveMock).toHaveBeenCalledWith(
      "11111111-1111-1111-1111-111111111111",
      false,
    );
  });

  it("testa a conexão (§13) e mostra o resultado", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Testar conexão" }));

    expect(await screen.findByText("Conexão OK (§13).")).toBeInTheDocument();
    expect(testAiProviderConnectionMock).toHaveBeenCalledWith(
      "11111111-1111-1111-1111-111111111111",
    );
  });

  it("esconde a escrita para quem não tem level administer", async () => {
    store.dispatch(
      sessionChecked({
        id: "3f1a1111-2222-3333-4444-555566667777",
        email: "bia@exemplo.com",
        displayName: "Bia",
        roles: ["Viewer"],
      }),
    );
    renderPage();

    expect((await screen.findAllByText("ollama")).length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "Cadastrar provider" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Desativar" })).toBeNull();
  });
});

/**
 * ADR-059 §3 (M3): "Listar modelos" só preenche o form do provider que
 * foi clicado E que é o editado — antes escrevia newModels+editModels
 * sempre, e o clique no B substituía o models já digitado no form de A.
 */
describe("AiProvidersPage — Listar modelos não polui o form de outro (M3)", () => {
  it("keeps A's edited models when listing models of B", async () => {
    getAiProvidersMock.mockResolvedValue(
      makeList({
        providers: [
          {
            id: "11111111-1111-1111-1111-111111111111",
            name: "ollama",
            baseUrl: "http://ollama:11434",
            active: true,
            apiKeySet: true,
            apiKeyMasked: "••••cret",
            models: ["llama3.2"],
          },
          {
            id: "99999999-9999-9999-9999-999999999999",
            name: "openai",
            baseUrl: "https://api.openai.com",
            active: true,
            apiKeySet: false,
            apiKeyMasked: null,
            models: ["gpt-4o"],
          },
        ],
      }),
    );
    vi.mocked(listAiProviderModels).mockImplementation(async (id: string) => ({
      models: id.startsWith("9999") ? ["gpt-4o-mini"] : ["qwen3"],
      error: null,
    }));

    const user = userEvent.setup();
    renderPage();

    // Abre o form de edição do provider A e digita modelos próprios.
    const editar = await screen.findAllByRole("button", { name: "Editar" });
    await user.click(editar[0]!);
    const input = await screen.findByLabelText("Modelos (separados por vírgula)");
    await user.clear(input);
    await user.type(input, "modelos-digitados-em-a");

    // "Listar modelos" do provider B NÃO pode sobrescrever o form de A.
    const listar = screen.getAllByRole("button", { name: "Listar modelos" });
    await user.click(listar[1]!);
    await waitFor(() =>
      expect(vi.mocked(listAiProviderModels)).toHaveBeenCalledWith(
        "99999999-9999-9999-9999-999999999999",
      ),
    );
    expect(screen.getByLabelText("Modelos (separados por vírgula)")).toHaveValue(
      "modelos-digitados-em-a",
    );

    // No provider A (editado), o click preenche o form.
    await user.click(listar[0]!);
    await waitFor(() =>
      expect(screen.getByLabelText("Modelos (separados por vírgula)")).toHaveValue("qwen3"),
    );
  });
});
