import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { IncidentsPage } from "./IncidentsPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { createIncident, investigateIncident, listIncidents, updateIncident } from "@/services/api/incidents";

/**
 * F29-T001B — piloto do `useFormErrors`.
 *
 * Esta página era a evidência de por que o bloco existe: ela pendurava o erro
 * do formulário INTEIRO em `<FormField label="Título">`, então a validação do
 * título era o único caso em que o recado saía no lugar certo — e o mesmo
 * texto era repetido no `FormError` do rodapé. Aqui se tranca que:
 *
 * 1. a falha de validação aparece **uma vez só**, e no campo que falhou;
 * 2. a recusa do servidor aparece no nível do formulário, **longe** dos campos
 *    (ninguém digitou errado, o backend é que recusou o conjunto).
 */
vi.mock("@/services/api/incidents", () => ({
  listIncidents: vi.fn(),
  createIncident: vi.fn(),
  changeIncidentStatus: vi.fn(),
  acknowledgeIncident: vi.fn(),
  updateIncident: vi.fn(),
  investigateIncident: vi.fn(),
}));

const listIncidentsMock = vi.mocked(listIncidents);
const createIncidentMock = vi.mocked(createIncident);
const updateIncidentMock = vi.mocked(updateIncident);
const investigateIncidentMock = vi.mocked(investigateIncident);

function seedIncident() {
  return {
    items: [
      {
        id: "11111111-2222-3333-4444-555566667777",
        title: "Chuva no datacenter",
        description: null,
        severity: "Critical",
        status: "Open",
        deviceId: null,
        openedAt: "2026-10-11T10:00:00Z",
        resolvedAt: null,
        alertCount: 0,
        alertIds: [],
        rootCause: null,
      },
    ],
    totalCount: 1,
    page: 1,
    pageSize: 20,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Operator"],
    }),
  );
  listIncidentsMock.mockResolvedValue({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
  });
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <IncidentsPage />
      </QueryClientProvider>
    </Provider>,
  );
}

describe("IncidentsPage — erro de formulário com campo declarado", () => {
  it("should attach the validation failure to the field that failed, once", async () => {
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Novo Incidente" }));
    await user.click(screen.getByRole("button", { name: "Criar incidente" }));

    expect(await screen.findByText("Informe o título do incidente.")).toBeInTheDocument();
    // Uma vez só. Antes a mesma frase saía embaixo do Título E no FormError
    // do rodapé — o usuário lia duas vezes a mesma acusação.
    expect(screen.getAllByText("Informe o título do incidente.")).toHaveLength(1);
    expect(createIncidentMock).not.toHaveBeenCalled();
  });

  it("should show a server refusal at form level, away from every field", async () => {
    createIncidentMock.mockRejectedValue(new Error("Já existe um incidente aberto com esse título."));
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Novo Incidente" }));
    await user.type(screen.getByPlaceholderText("Título *"), "Chuva no datacenter");
    await user.click(screen.getByRole("button", { name: "Criar incidente" }));

    expect(
      await screen.findByText("Já existe um incidente aberto com esse título."),
    ).toBeInTheDocument();
    // Nenhuma vez no campo: o título digitado estava válido.
    expect(screen.getAllByText("Já existe um incidente aberto com esse título.")).toHaveLength(1);
  });

  it("should keep the count subtitle on the shared shell", async () => {
    listIncidentsMock.mockResolvedValue(seedIncident());

    renderPage();

    expect(await screen.findByText("Chuva no datacenter")).toBeInTheDocument();
    expect(screen.getByText("1 incidente")).toBeInTheDocument();
  });
});

/**
 * CRUD-Incidentes (2026-10-12) — edição dos campos descritivos via
 * `PUT /incidents/{id}` (status continua no ciclo por estado; exclusão não
 * existe por ADR-030).
 */
describe("IncidentsPage — edição de campos", () => {
  it("should edit title/description/severity and call updateIncident", async () => {
    listIncidentsMock.mockResolvedValue(seedIncident());
    updateIncidentMock.mockResolvedValue(null);

    renderPage();
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole("button", { name: /editar campos do incidente/i }),
    );
    const titleInput = screen.getByDisplayValue("Chuva no datacenter");
    await user.clear(titleInput);
    await user.type(titleInput, "Chuva intensa");
    await user.click(screen.getByRole("button", { name: /salvar alterações/i }));

    await screen.findByText("Incidente atualizado.");
    expect(updateIncidentMock).toHaveBeenCalledWith(
      "11111111-2222-3333-4444-555566667777",
      expect.objectContaining({ title: "Chuva intensa" }),
    );
  });
});

/**
 * AI Investigation (ADR-053 · Fase C) — `POST /incidents/{id}/investigate`
 * (Operate). O painel abre com o estado pendente, exibe a análise + as
 * evidências + provider/modelo; falha (ex.: 503 sem provider) fica DENTRO
 * do painel, com o botão Fechar ainda disponível.
 */
describe("IncidentsPage — investigação com IA", () => {
  it("should investigate the incident and render analysis with evidences", async () => {
    listIncidentsMock.mockResolvedValue(seedIncident());
    investigateIncidentMock.mockResolvedValue({
      analysis: "Hipótese (Probable): saturação do enlace. [E1]",
      evidences: [
        {
          source: "knowledge",
          sourceType: "article",
          citation: "Artigo #7 — Link principal",
          confidence: "Possible",
          timestampUtc: "2026-10-01T12:00:00Z",
        },
      ],
      provider: "ollama",
      model: "qwen2.5-coder:1.5b",
      usedFallback: false,
    });

    renderPage();
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole("button", { name: /investigar incidente com ia/i }),
    );

    expect(investigateIncidentMock).toHaveBeenCalledWith(
      "11111111-2222-3333-4444-555566667777",
    );
    expect(
      await screen.findByText("Hipótese (Probable): saturação do enlace. [E1]"),
    ).toBeInTheDocument();
    expect(screen.getByText("Artigo #7 — Link principal")).toBeInTheDocument();
    expect(screen.getByText(/ollama · qwen2\.5-coder:1\.5b/)).toBeInTheDocument();
    expect(screen.getByText("LEVEL 0 · somente análise")).toBeInTheDocument();
  });

  it("should keep the panel open with the friendly failure when the provider is unavailable", async () => {
    listIncidentsMock.mockResolvedValue(seedIncident());
    investigateIncidentMock.mockRejectedValue(
      new Error("IA indisponível: nenhum provedor configurado no servidor."),
    );

    renderPage();
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole("button", { name: /investigar incidente com ia/i }),
    );

    expect(
      await screen.findByText("IA indisponível: nenhum provedor configurado no servidor."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /fechar/i })).toBeInTheDocument();
  });
});
