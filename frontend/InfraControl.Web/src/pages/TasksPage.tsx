import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Can } from "@/components/auth/Can";
import { formatDateTime } from "@/utils/format";
import {
  changeTaskState,
  createTask,
  listTasks,
  type PagedTasks,
  type TaskItem,
} from "@/services/api/tasks";

/**
 * F17-T001 — TasksPage conectada ao backend (substitui o mock de
 * `data/dashboardData`). O quadro espelha as enumerações REAIS: as colunas
 * são os 7 estados de `TaskState` e o chip de cada card é o `TaskKind`
 * (catálogo fechado de docs/04). Campos do mock que não existem no contrato
 * (priority, dueDate, tags) saíram — o backend é a autoridade. Escrita fica
 * atrás de `Can operate` (exibição; o 403 volta no envelope se escapar).
 * Pendências da F17-T001 fechadas: filtros `term/kind/state` na UI (params
 * reais de `GET /tasks`) e transição de estado via `PUT /tasks/{id}/state`
 * (o mapa de transições continua no domínio — fora dele o 409 pt-BR do
 * envelope vira banner).
 */

const stateColumns = [
  { id: "Open", label: "Abertas", color: "#6b7280" },
  { id: "InProgress", label: "Em progresso", color: "#3b82f6" },
  { id: "Waiting", label: "Aguardando", color: "#f59e0b" },
  { id: "Blocked", label: "Bloqueadas", color: "#dc2626" },
  { id: "Resolved", label: "Resolvidas", color: "#22c55e" },
  { id: "Cancelled", label: "Canceladas", color: "#94a3b8" },
  { id: "Closed", label: "Fechadas", color: "#374151" },
];

const stateLabel = (state: string): string =>
  stateColumns.find((column) => column.id === state)?.label ?? state;

/** Rótulos pt-BR dos valores enviados no body (`CreateTaskCommand`). */
const kindOptions = [
  { value: "Manual", label: "Manual" },
  { value: "Incident", label: "Incidente" },
  { value: "Alert", label: "Alerta" },
  { value: "Monitoring", label: "Monitoramento" },
  { value: "Discovery", label: "Descoberta" },
  { value: "Maintenance", label: "Manutenção" },
  { value: "Change", label: "Mudança" },
  { value: "Preventive", label: "Preventiva" },
  { value: "Corrective", label: "Corretiva" },
  { value: "Project", label: "Projeto" },
  { value: "System", label: "Sistema" },
];

const kindLabel = (kind: string): string =>
  kindOptions.find((option) => option.value === kind)?.label ?? kind;

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function TasksPage() {
  const [page, setPage] = useState<PagedTasks | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [creating, setCreating] = useState(false);
  const [title, setTitle] = useState("");
  const [kind, setKind] = useState("Manual");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Filtros: o input é aplicado no submit (mesmo padrão da ClientsPage) e os
  // valores aplicados é que disparam a recarga.
  const [termInput, setTermInput] = useState("");
  const [term, setTerm] = useState("");
  const [kindFilter, setKindFilter] = useState("");
  const [stateFilter, setStateFilter] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      // pageSize no clamp máximo do handler (1..100) — o quadro não pagina.
      setPage(await listTasks({
        pageSize: 100,
        term: term || undefined,
        kind: kindFilter || undefined,
        state: stateFilter || undefined,
      }));
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [term, kindFilter, stateFilter]);

  useEffect(() => {
    void load();
  }, [load]);

  function applyFilters(event: FormEvent) {
    event.preventDefault();
    setTerm(termInput.trim());
  }

  function clearFilters(event: FormEvent) {
    event.preventDefault();
    setTermInput("");
    setTerm("");
    setKindFilter("");
    setStateFilter("");
  }

  /** Transição de estado — 409/400 pt-BR do envelope vira o banner de erro. */
  const move = useCallback(async (task: TaskItem, target: string) => {
    setNotice(null);
    setError(null);
    try {
      await changeTaskState(task.id, target);
      setNotice(`"${task.title}" movida para ${stateLabel(target)}.`);
      await load();
    } catch (caught) {
      setError(messageFor(caught));
    }
  }, [load]);

  async function submitCreate(event: FormEvent) {
    event.preventDefault();
    const trimmed = title.trim();
    if (!trimmed) {
      setFormError("Informe o título da tarefa.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      await createTask({
        title: trimmed,
        kind,
        description: description.trim() || undefined,
      });
      setCreating(false);
      setTitle("");
      setDescription("");
      setKind("Manual");
      setNotice("Tarefa criada.");
      await load();
    } catch (caught) {
      setFormError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  }

  const items = page?.items ?? [];
  const totalCount = page?.totalCount ?? 0;
  const truncated = totalCount > items.length;
  const hasFilters = term !== "" || kindFilter !== "" || stateFilter !== "";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Tarefas</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>
            {loading ? "Carregando…" : `${totalCount} ${totalCount === 1 ? "tarefa" : "tarefas"}`}
            {truncated && ` — exibindo as ${items.length} primeiras`}
            {!loading && hasFilters && " · filtros ativos"}
          </p>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <button
            onClick={() => void load()}
            disabled={loading}
            style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 12px", borderRadius: 6, background: "white", border: "1px solid #e5e7eb", fontSize: 12, color: "#374151", cursor: loading ? "default" : "pointer" }}
          >
            <i className="pi pi-refresh" style={{ fontSize: 11 }} /> Atualizar
          </button>
          <Can level="operate">
            <button
              onClick={() => { setCreating((value) => !value); setFormError(null); }}
              style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}
            >
              <i className="pi pi-plus" style={{ fontSize: 11 }} /> Nova Tarefa
            </button>
          </Can>
        </div>
      </div>

      <form onSubmit={applyFilters} style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        <input
          value={termInput}
          onChange={(event) => setTermInput(event.target.value)}
          placeholder="Buscar por título…"
          style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, minWidth: 220 }}
        />
        <select
          value={kindFilter}
          onChange={(event) => setKindFilter(event.target.value)}
          style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, background: "white" }}
        >
          <option value="">Todos os tipos</option>
          {kindOptions.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
        <select
          value={stateFilter}
          onChange={(event) => setStateFilter(event.target.value)}
          style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, background: "white" }}
        >
          <option value="">Todos os estados</option>
          {stateColumns.map((column) => (
            <option key={column.id} value={column.id}>{column.label}</option>
          ))}
        </select>
        <button
          type="submit"
          style={{ padding: "7px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}
        >
          Filtrar
        </button>
        {hasFilters && (
          <button
            type="button"
            onClick={clearFilters}
            style={{ padding: "7px 14px", borderRadius: 6, background: "white", border: "1px solid #e5e7eb", fontSize: 12, color: "#374151" }}
          >
            Limpar
          </button>
        )}
      </form>

      {notice && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#f0fdf4", color: "#15803d" }}>
          <i className="pi pi-check-circle" />
          <span>{notice}</span>
          <button onClick={() => setNotice(null)} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer" }}>
            <i className="pi pi-times" />
          </button>
        </div>
      )}

      {error && (
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", borderRadius: 6, fontSize: 13, background: "#fef2f2", color: "#b91c1c" }}>
          <i className="pi pi-exclamation-circle" />
          <span>{error}</span>
          <button onClick={() => void load()} style={{ marginLeft: "auto", background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: 600 }}>
            Tentar de novo
          </button>
        </div>
      )}

      {creating && (
        <form onSubmit={submitCreate} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Nova tarefa</div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 220px", gap: 10 }}>
            <input
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              placeholder="Título *"
              autoFocus
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
            <select
              value={kind}
              onChange={(event) => setKind(event.target.value)}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, background: "white" }}
            >
              {kindOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Descrição (opcional)"
            rows={3}
            style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit" }}
          />
          {formError && (
            <div style={{ fontSize: 12, color: "#b91c1c" }}>{formError}</div>
          )}
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <button
              type="button"
              onClick={() => { setCreating(false); setFormError(null); }}
              style={{ padding: "6px 14px", borderRadius: 6, background: "white", border: "1px solid #e5e7eb", fontSize: 12, color: "#374151" }}
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={saving}
              style={{ padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500, opacity: saving ? 0.6 : 1 }}
            >
              {saving ? "Salvando…" : "Criar tarefa"}
            </button>
          </div>
        </form>
      )}

      <div style={{ display: "flex", gap: 12, overflowX: "auto", alignItems: "flex-start", paddingBottom: 4 }}>
        {stateColumns.map((col) => {
          const colTasks = items.filter((task) => task.state === col.id);
          return (
            <div key={col.id} style={{ minWidth: 230, flex: "0 0 auto" }}>
              <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 10, padding: "8px 12px", background: "white", borderRadius: 6, border: "1px solid #e5e7eb" }}>
                <div style={{ width: 8, height: 8, borderRadius: "50%", background: col.color }} />
                <span style={{ fontSize: 12, fontWeight: 600 }}>{col.label}</span>
                <span style={{ marginLeft: "auto", fontSize: 10, fontWeight: 600, padding: "1px 6px", borderRadius: 10, background: "#f3f4f6", color: "#6b7280" }}>{colTasks.length}</span>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {loading && !page && (
                  <div style={{ fontSize: 11, color: "#9ca3af", padding: "6px 2px" }}>…</div>
                )}
                {!loading && colTasks.length === 0 && (
                  <div style={{ fontSize: 11, color: "#9ca3af", padding: "6px 2px" }}>Sem tarefas</div>
                )}
                {colTasks.map((task) => (
                  <TaskCard key={task.id} task={task} onMove={move} />
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function TaskCard({
  task,
  onMove,
}: {
  task: TaskItem;
  onMove: (task: TaskItem, target: string) => Promise<void>;
}) {
  const [moving, setMoving] = useState(false);
  return (
    <div style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 12, transition: "all 0.12s" }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6, gap: 8 }}>
        <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: "#f1f5f9", color: "#475569", whiteSpace: "nowrap" }}>
          {kindLabel(task.kind)}
        </span>
        <span style={{ fontSize: 10, color: "#9ca3af" }}>
          {formatDateTime(new Date(task.openedAt))}
        </span>
      </div>
      <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 6 }}>{task.title}</div>
      {task.description && (
        <div style={{
          fontSize: 10, color: "#6b7280", marginBottom: 6,
          display: "-webkit-box", WebkitLineClamp: 2, WebkitBoxOrient: "vertical", overflow: "hidden",
        }}>
          {task.description}
        </div>
      )}
      {(task.assigneeCount > 0 || task.deviceCount > 0 || task.incidentCount > 0) && (
        <div style={{ display: "flex", gap: 10, fontSize: 10, color: "#64748b" }}>
          {task.assigneeCount > 0 && (
            <span title={`${task.assigneeCount} responsável(is)`}>
              <i className="pi pi-user" style={{ fontSize: 9, marginRight: 3 }} />{task.assigneeCount}
            </span>
          )}
          {task.deviceCount > 0 && (
            <span title={`${task.deviceCount} dispositivo(s)`}>
              <i className="pi pi-server" style={{ fontSize: 9, marginRight: 3 }} />{task.deviceCount}
            </span>
          )}
          {task.incidentCount > 0 && (
            <span title={`${task.incidentCount} incidente(s)`}>
              <i className="pi pi-exclamation-circle" style={{ fontSize: 9, marginRight: 3 }} />{task.incidentCount}
            </span>
          )}
        </div>
      )}
      <Can level="operate">
        <div style={{ marginTop: 8, borderTop: "1px solid #f3f4f6", paddingTop: 8 }}>
          {/* Select controlado por task.state: se a transição falhar (409 do
              mapa do domínio), o re-render restaura o valor anterior — a UI
              nunca mostra um estado que o backend não gravou. */}
          <select
            value={task.state}
            disabled={moving}
            aria-label={`Mover "${task.title}" para outro estado`}
            onChange={(event) => {
              const target = event.target.value;
              if (target === task.state) return;
              setMoving(true);
              void onMove(task, target).finally(() => setMoving(false));
            }}
            style={{ width: "100%", padding: "5px 8px", fontSize: 11, border: "1px solid #e5e7eb", borderRadius: 6, background: "white", color: "#374151", cursor: "pointer" }}
          >
            {stateColumns.map((column) => (
              <option key={column.id} value={column.id}>{column.label}</option>
            ))}
          </select>
        </div>
      </Can>
    </div>
  );
}
