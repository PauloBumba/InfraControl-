import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { CopilotPage } from "./CopilotPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { sendAiChat, type AiChatResponse } from "@/services/api/ai";
import {
  generateAutomationRecommendations,
  type AutomationRecommendations,
} from "@/services/api/automation";
import { ApiError } from "@/services/api/client";

/** Fase B (ADR-052) — CopilotPage consome POST /ai/chat grounded. */
vi.mock("@/services/api/ai", () => ({
  sendAiChat: vi.fn(),
}));

/** Fase F (ADR-055) — bloco LEVEL 1 consome POST /automation/recommendations. */
vi.mock("@/services/api/automation", () => ({
  generateAutomationRecommendations: vi.fn(),
}));

const sendMock = vi.mocked(sendAiChat);
const recommendationsMock = vi.mocked(generateAutomationRecommendations);

const grounded: AiChatResponse = {
  answer: "O INC-42 está em andamento [E1].",
  evidences: [
    {
      source: "Incident",
      sourceType: "incident",
      citation: "Incident #42 — link de falha APP-02",
      confidence: "Probable",
      timestampUtc: "2026-10-13T12:00:00Z",
    },
  ],
  provider: "ollama",
  model: "qwen2.5-coder:1.5b",
  usedFallback: false,
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
      <CopilotPage />
    </Provider>,
  );
}

describe("CopilotPage — chat grounded", () => {
  it("should send the question and render the grounded answer with evidence", async () => {
    sendMock.mockResolvedValue(grounded);
    renderPage();
    const user = userEvent.setup();

    await user.type(
      screen.getByLabelText(/mensagem para o copilot/i),
      "Qual o status do INC-42?",
    );
    await user.click(screen.getByRole("button", { name: /enviar/i }));

    expect(await screen.findByText(/INC-42 está em andamento/i)).toBeTruthy();
    expect(screen.getByText(/Incident #42 — link de falha APP-02/i)).toBeTruthy();
    expect(screen.getByText(/ollama · qwen2.5-coder:1.5b/i)).toBeTruthy();

    expect(sendMock).toHaveBeenCalledWith([
      { role: "user", content: "Qual o status do INC-42?" },
    ]);
  });

  it("should show the not-configured state on 503", async () => {
    sendMock.mockRejectedValue(
      new ApiError("O serviço de IA não está configurado neste ambiente.", 503),
    );
    renderPage();
    const user = userEvent.setup();

    await user.type(screen.getByLabelText(/mensagem para o copilot/i), "oi");
    await user.click(screen.getByRole("button", { name: /enviar/i }));

    // banner degradado + bubble do assistente mencionam o estado
    const matches = await screen.findAllByText(/serviço de IA não está configurado/i);
    expect(matches.length).toBeGreaterThan(0);
  });

  it("should keep the send button disabled while the input is empty", () => {
    renderPage();

    expect(screen.getByRole("button", { name: /enviar/i })).toHaveProperty(
      "disabled",
      true,
    );
  });
});

/**
 * Fase F (ADR-055) — Automation LEVEL 1: o bloco gera recomendações e
 * deixa claro que quem decide/executa é o humano; sem sinais o backend
 * devolve lista vazia sem chamar a IA.
 */
describe("CopilotPage — assistente de automação (LEVEL 1)", () => {
  const sample: AutomationRecommendations = {
    items: [
      {
        title: "Conferir enlace do APP-02",
        rationale:
          "Incidente Critical aberto desde 08:00. Verificar o status do link antes de agir.",
        priority: "alta",
      },
    ],
    provider: "ollama",
    model: "qwen2.5-coder:1.5b",
    usedFallback: false,
  };

  it("should render the recommendations with priority and the LEVEL 1 disclaimer", async () => {
    recommendationsMock.mockResolvedValue(sample);
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: /gerar recomendações/i }));

    expect(await screen.findByText("Conferir enlace do APP-02")).toBeTruthy();
    expect(screen.getByText("Alta")).toBeTruthy();
    expect(screen.getByText(/Verificar o status do link/)).toBeTruthy();
    expect(
      screen.getByText(/LEVEL 1: nenhuma ação é executada por esta IA/),
    ).toBeTruthy();
    expect(recommendationsMock).toHaveBeenCalledTimes(1);
  });

  it("should explain when there are no active signals", async () => {
    recommendationsMock.mockResolvedValue({
      items: [],
      provider: "ollama",
      model: "qwen2.5-coder:1.5b",
      usedFallback: false,
    });
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: /gerar recomendações/i }));

    expect(await screen.findByText(/Nenhum sinal ativo/)).toBeTruthy();
  });

  it("should show the degraded banner when no provider is configured", async () => {
    recommendationsMock.mockRejectedValue(
      new ApiError("O serviço de IA não está configurado neste ambiente.", 503),
    );
    renderPage();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: /gerar recomendações/i }));

    const matches = await screen.findAllByText(/serviço de IA não está configurado/i);
    expect(matches.length).toBeGreaterThan(0);
  });
});
