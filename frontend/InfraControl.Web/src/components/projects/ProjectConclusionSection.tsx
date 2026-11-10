import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/StatusChip";
import {
  approveProjectConclusion,
  listProjectConclusions,
  returnProjectConclusion,
  submitProjectConclusion,
  type ProjectConclusionItem,
} from "@/services/api/projects";

const STATUS_LABEL: Record<string, string> = {
  Submitted: "Aguardando análise",
  Approved: "Aprovada",
  Rejected: "Reprovada",
  ChangesRequested: "Ajustes solicitados",
};

const STATUS_COLOR: Record<string, { background: string; color: string }> = {
  Submitted: { background: "#fef3c7", color: "#b45309" },
  Approved: { background: "#dcfce7", color: "#15803d" },
  Rejected: { background: "#fee2e2", color: "#b91c1c" },
  ChangesRequested: { background: "#e0e7ff", color: "#4338ca" },
};

const INPUT_STYLE = {
  width: "100%",
  padding: "9px 10px",
  borderRadius: 8,
  border: "1px solid #cbd5e1",
  boxSizing: "border-box" as const,
};

function formatDateTime(value: string | null): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("pt-BR");
}

interface Props {
  projectId: string;
  projectStatus: string;
  canOperate: boolean;
  isAdmin: boolean;
  onChanged?: () => void;
}

/**
 * Conclusão formal do projeto (spec evolução §6): o responsável descreve o
 * trabalho realizado; o admin aprova, reprova ou pede ajustes. A aprovação
 * gera conhecimento na base (backend, idempotente).
 */
export function ProjectConclusionSection({
  projectId,
  projectStatus,
  canOperate,
  isAdmin,
  onChanged,
}: Props) {
  const [conclusions, setConclusions] = useState<ProjectConclusionItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [decision, setDecision] = useState<{
    mode: "approve" | "reject" | "requestchanges";
    notes: string;
  } | null>(null);
  const [form, setForm] = useState({
    description: "",
    activitiesPerformed: "",
    resultsAchieved: "",
    problemsEncountered: "",
    solutionsImplemented: "",
    evidenceSummary: "",
    completedAt: new Date().toISOString().slice(0, 10),
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setConclusions(await listProjectConclusions(projectId));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar as conclusões.");
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  useEffect(() => {
    void load();
  }, [load]);

  const latest = conclusions[0] ?? null;
  const canSubmit =
    canOperate &&
    projectStatus === "InProgress" &&
    (!latest || latest.status !== "Submitted");
  const pendingReview = latest?.status === "Submitted";

  async function handleSubmit() {
    if (!form.description.trim() || !form.completedAt) {
      setError("Descrição e data de conclusão são obrigatórias.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await submitProjectConclusion(projectId, {
        description: form.description.trim(),
        activitiesPerformed: form.activitiesPerformed.trim() || null,
        resultsAchieved: form.resultsAchieved.trim() || null,
        problemsEncountered: form.problemsEncountered.trim() || null,
        solutionsImplemented: form.solutionsImplemented.trim() || null,
        evidenceSummary: form.evidenceSummary.trim() || null,
        completedAt: new Date(`${form.completedAt}T12:00:00`).toISOString(),
      });
      setFormOpen(false);
      setNotice("Solicitação de conclusão enviada para análise.");
      await load();
      onChanged?.();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível enviar a conclusão.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDecision() {
    if (!decision) return;
    if (decision.mode !== "approve" && !decision.notes.trim()) {
      setError("A justificativa é obrigatória para reprovar ou solicitar ajustes.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      if (decision.mode === "approve") {
        await approveProjectConclusion(projectId, decision.notes.trim() || null);
        setNotice("Conclusão aprovada — conhecimento registrado na base.");
      } else {
        await returnProjectConclusion(
          projectId,
          decision.mode === "reject" ? "reject" : "requestchanges",
          decision.notes.trim(),
        );
        setNotice(
          decision.mode === "reject"
            ? "Conclusão reprovada."
            : "Ajustes solicitados ao responsável.",
        );
      }
      setDecision(null);
      await load();
      onChanged?.();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível registrar a decisão.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20, display: "grid", gap: 12 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
        <div style={{ fontWeight: 700, fontSize: 18 }}>Conclusão do projeto</div>
        {canSubmit && !formOpen && (
          <Button variant="primary" icon="pi pi-send" onClick={() => setFormOpen(true)}>
            Solicitar conclusão
          </Button>
        )}
      </div>

      {notice && (
        <div role="status" style={{ padding: 10, borderRadius: 8, background: "#f0fdf4", color: "#15803d", fontSize: 12 }}>
          {notice}
        </div>
      )}
      {error && (
        <div role="alert" style={{ padding: 10, borderRadius: 8, background: "#fef2f2", color: "#991b1b", fontSize: 12 }}>
          {error}
        </div>
      )}

      {loading ? (
        <div style={{ color: "#64748b" }}>Carregando…</div>
      ) : conclusions.length === 0 ? (
        <div style={{ color: "#64748b" }}>Nenhuma solicitação de conclusão registrada.</div>
      ) : (
        <div style={{ display: "grid", gap: 12 }}>
          {conclusions.map((item) => {
            const color = STATUS_COLOR[item.status] ?? { background: "#f1f5f9", color: "#475569" };
            return (
              <div key={item.id} style={{ border: "1px solid #e2e8f0", borderRadius: 10, padding: 14, display: "grid", gap: 8 }}>
                <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
                  <StatusChip label={STATUS_LABEL[item.status] ?? item.status} background={color.background} color={color.color} />
                  <span style={{ fontSize: 12, color: "#64748b" }}>
                    Concluído em {formatDateTime(item.completedAt)} · enviado {formatDateTime(item.createdAt)}
                  </span>
                </div>
                <div style={{ fontSize: 13, whiteSpace: "pre-wrap" }}>{item.description}</div>
                {[
                  ["Atividades", item.activitiesPerformed],
                  ["Resultados", item.resultsAchieved],
                  ["Problemas", item.problemsEncountered],
                  ["Soluções", item.solutionsImplemented],
                  ["Evidências", item.evidenceSummary],
                ].map(([label, value]) =>
                  value ? (
                    <div key={label} style={{ fontSize: 12, color: "#475569" }}>
                      <strong>{label}:</strong> {value}
                    </div>
                  ) : null,
                )}
                {item.decisionNotes && (
                  <div style={{ fontSize: 12, color: "#991b1b" }}>
                    <strong>Decisão:</strong> {item.decisionNotes}
                    {item.decidedAt ? ` · ${formatDateTime(item.decidedAt)}` : ""}
                  </div>
                )}
                {item.knowledgeArticleId && (
                  <div style={{ fontSize: 12, color: "#15803d" }}>Conhecimento registrado na base.</div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {formOpen && (
        <div style={{ display: "grid", gap: 10, borderTop: "1px solid #e2e8f0", paddingTop: 12 }}>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Descrição do trabalho realizado *</span>
            <textarea rows={3} style={INPUT_STYLE} value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </label>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 10 }}>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Atividades executadas</span>
              <textarea rows={2} style={INPUT_STYLE} value={form.activitiesPerformed}
                onChange={(e) => setForm({ ...form, activitiesPerformed: e.target.value })} />
            </label>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Resultados obtidos</span>
              <textarea rows={2} style={INPUT_STYLE} value={form.resultsAchieved}
                onChange={(e) => setForm({ ...form, resultsAchieved: e.target.value })} />
            </label>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Problemas encontrados</span>
              <textarea rows={2} style={INPUT_STYLE} value={form.problemsEncountered}
                onChange={(e) => setForm({ ...form, problemsEncountered: e.target.value })} />
            </label>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Soluções implementadas</span>
              <textarea rows={2} style={INPUT_STYLE} value={form.solutionsImplemented}
                onChange={(e) => setForm({ ...form, solutionsImplemented: e.target.value })} />
            </label>
          </div>
          <div style={{ display: "flex", gap: 10, flexWrap: "wrap", alignItems: "end" }}>
            <label style={{ display: "grid", gap: 4, flex: "1 1 220px" }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Evidências e documentos</span>
              <input style={INPUT_STYLE} value={form.evidenceSummary}
                onChange={(e) => setForm({ ...form, evidenceSummary: e.target.value })} />
            </label>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Data de conclusão *</span>
              <input type="date" style={INPUT_STYLE} value={form.completedAt}
                onChange={(e) => setForm({ ...form, completedAt: e.target.value })} />
            </label>
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <Button variant="primary" onClick={() => void handleSubmit()} disabled={saving}>
              {saving ? "Enviando…" : "Enviar solicitação"}
            </Button>
            <Button variant="ghost" onClick={() => setFormOpen(false)}>Cancelar</Button>
          </div>
        </div>
      )}

      {isAdmin && pendingReview && !decision && (
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", borderTop: "1px solid #e2e8f0", paddingTop: 12 }}>
          <Button variant="primary" icon="pi pi-check" onClick={() => setDecision({ mode: "approve", notes: "" })}>
            Aprovar
          </Button>
          <Button variant="ghost" icon="pi pi-exclamation-triangle" onClick={() => setDecision({ mode: "requestchanges", notes: "" })}>
            Solicitar ajustes
          </Button>
          <Button variant="danger" icon="pi pi-times" onClick={() => setDecision({ mode: "reject", notes: "" })}>
            Reprovar
          </Button>
        </div>
      )}

      {decision && (
        <div style={{ display: "grid", gap: 8, borderTop: "1px solid #e2e8f0", paddingTop: 12 }}>
          <span style={{ fontSize: 12, color: "#475569" }}>
            {decision.mode === "approve"
              ? "Aprovar conclusão (opcional: parecer)"
              : "Justificativa (obrigatória)"}
          </span>
          <textarea
            rows={3}
            style={INPUT_STYLE}
            value={decision.notes}
            onChange={(e) => setDecision({ ...decision, notes: e.target.value })}
            placeholder={decision.mode === "approve" ? "Parecer da aprovação…" : "Descreva o motivo…"}
          />
          <div style={{ display: "flex", gap: 8 }}>
            <Button variant="primary" onClick={() => void handleDecision()} disabled={saving}>
              {saving ? "Registrando…" : "Confirmar"}
            </Button>
            <Button variant="ghost" onClick={() => setDecision(null)}>Cancelar</Button>
          </div>
        </div>
      )}
    </div>
  );
}
