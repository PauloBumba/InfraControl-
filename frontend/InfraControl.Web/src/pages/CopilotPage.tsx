import { useRef, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { ApiError } from "@/services/api/client";
import {
  sendAiChat,
  type AiChatEvidence,
  type ChatTurn,
} from "@/services/api/ai";

/** Mensagem da tela: turno + anexos de evidência quando é do assistente. */
interface ChatView {
  role: "user" | "assistant";
  content: string;
  evidences?: AiChatEvidence[];
  provider?: string;
  model?: string;
  usedFallback?: boolean;
}

const MAX_TURNS = 20;

/**
 * Copilot (ADR-052 · Fase B): chat grounded LEVEL 0 — a IA responde com
 * evidências citadas ([E1]…), nunca inventa e nunca executa ações. O
 * histórico vive na sessão da tela (últimos 20 turnos são o contrato do
 * backend). Sem provider configurado, o 503 vira aviso degradado.
 */
export function CopilotPage() {
  const [views, setViews] = useState<ChatView[]>([]);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notConfigured, setNotConfigured] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);

  async function handleSend() {
    const question = input.trim();
    if (!question || sending) return;

    const history: ChatTurn[] = [
      ...views.map((view) => ({ role: view.role, content: view.content })),
      { role: "user" as const, content: question },
    ].slice(-MAX_TURNS);

    setInput("");
    setError(null);
    setNotConfigured(false);
    setViews((previous) => [...previous, { role: "user", content: question }]);
    setSending(true);

    try {
      const response = await sendAiChat(history);
      setViews((previous) => [
        ...previous,
        {
          role: "assistant",
          content: response.answer,
          evidences: response.evidences,
          provider: response.provider,
          model: response.model,
          usedFallback: response.usedFallback,
        },
      ]);
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 503) {
        setNotConfigured(true);
      } else {
        setError(
          caught instanceof Error
            ? caught.message
            : "Não foi possível obter resposta do Copilot.",
        );
      }
      setViews((previous) => [
        ...previous,
        {
          role: "assistant",
          content:
            caught instanceof ApiError && caught.status === 503
              ? "O serviço de IA não está configurado neste ambiente."
              : "Desculpe, não consegui responder agora. Tente novamente.",
        },
      ]);
    } finally {
      setSending(false);
      bottomRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }

  return (
    <div className="uni-page">
      <PageHeader
        title="Copilot de IA"
        subtitle="Chat grounded em evidências da plataforma — nível LEVEL 0 (somente leitura, ADR-052)."
      />

      {notConfigured && (
        <ErrorBanner message="O serviço de IA não está configurado neste ambiente. Contate o administrador para habilitar o Ollama (profile ai) ou um provedor externo (ADR-050)." />
      )}
      {error && <ErrorBanner message={error} />}

      <div
        style={{
          background: "white",
          border: "1px solid #e5e7eb",
          borderRadius: 8,
          display: "flex",
          flexDirection: "column",
          minHeight: 420,
          maxHeight: 620,
        }}
      >
        <div
          style={{
            flex: 1,
            overflowY: "auto",
            padding: 16,
            display: "flex",
            flexDirection: "column",
            gap: 12,
          }}
        >
          {views.length === 0 && (
            <p style={{ color: "#94a3b8", fontSize: 13 }}>
              Pergunte sobre incidentes, conhecimento, tarefas ou
              procedimentos — o Copilot responde com base nas evidências
              recuperadas da plataforma e cita cada fonte.
            </p>
          )}

          {views.map((view, index) => (
            <div
              key={index}
              style={{
                alignSelf: view.role === "user" ? "flex-end" : "flex-start",
                maxWidth: "80%",
                background: view.role === "user" ? "#dbeafe" : "#f8fafc",
                border: "1px solid",
                borderColor: view.role === "user" ? "#93c5fd" : "#e5e7eb",
                borderRadius: 8,
                padding: "10px 12px",
              }}
            >
              <div style={{ fontSize: 11, color: "#64748b", marginBottom: 4 }}>
                {view.role === "user" ? "Você" : "Copilot"}
              </div>
              <div style={{ fontSize: 13, whiteSpace: "pre-wrap" }}>
                {view.content}
              </div>

              {view.evidences && view.evidences.length > 0 && (
                <div
                  style={{
                    marginTop: 8,
                    display: "flex",
                    flexDirection: "column",
                    gap: 4,
                  }}
                >
                  <div style={{ fontSize: 11, color: "#64748b" }}>
                    Evidências
                  </div>
                  {view.evidences.map((evidence, evidenceIndex) => (
                    <div
                      key={evidenceIndex}
                      style={{
                        fontSize: 11,
                        color: "#334155",
                        background: "white",
                        border: "1px solid #e5e7eb",
                        borderRadius: 6,
                        padding: "4px 8px",
                      }}
                    >
                      <b>[E{evidenceIndex + 1}]</b> {evidence.citation}{" "}
                      <span style={{ color: "#94a3b8" }}>
                        ({evidence.source} · {evidence.confidence})
                      </span>
                    </div>
                  ))}
                </div>
              )}

              {view.provider && (
                <div style={{ fontSize: 10, color: "#94a3b8", marginTop: 6 }}>
                  {view.provider} · {view.model}
                  {view.usedFallback ? " (fallback)" : ""}
                </div>
              )}
            </div>
          ))}

          {sending && (
            <div style={{ fontSize: 12, color: "#94a3b8" }}>
              Investigando…
            </div>
          )}
          <div ref={bottomRef} />
        </div>

        <div
          style={{
            display: "flex",
            gap: 8,
            padding: 12,
            borderTop: "1px solid #e5e7eb",
          }}
        >
          <input
            value={input}
            onChange={(event) => setInput(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                void handleSend();
              }
            }}
            placeholder="Pergunte ao Copilot…"
            aria-label="Mensagem para o Copilot"
            style={{
              flex: 1,
              padding: "8px 10px",
              fontSize: 13,
              border: "1px solid #d1d5db",
              borderRadius: 6,
            }}
          />
          <Button
            variant="primary"
            onClick={() => void handleSend()}
            disabled={sending || input.trim().length === 0}
            style={{ padding: "8px 16px" }}
          >
            {sending ? "Enviando…" : "Enviar"}
          </Button>
        </div>
      </div>
    </div>
  );
}
