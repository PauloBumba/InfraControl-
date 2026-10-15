import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { CopilotPage } from "./CopilotPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import { sendAiChat, type AiChatResponse } from "@/services/api/ai";
import { ApiError } from "@/services/api/client";

/** Fase B (ADR-052) — CopilotPage consome POST /ai/chat grounded. */
vi.mock("@/services/api/ai", () => ({
  sendAiChat: vi.fn(),
}));

const sendMock = vi.mocked(sendAiChat);

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
