import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { StatusChip } from "@/components/ui/StatusChip";
import { Can } from "@/components/auth/Can";
import { PageHeader } from "@/components/ui/PageHeader";
import { SearchInput } from "@/components/ui/SearchInput";
import { FormField, FormError } from "@/components/ui/FormField";
import { SelectField } from "@/components/ui/SelectField";
import { formatDateTime } from "@/utils/format";
import { ProgressBar } from "primereact/progressbar";
import {
  changeTaskState,
  createTask,
  listTasks,
  setTaskIdentification,
  type PagedTasks,
  type TaskItem,
} from "@/services/api/tasks";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
import {
  formatDateOnly,
  isOverdue,
  kindLabel,
  kindOptions,
  messageFor,
  priorityOptions,
  priorityVisual,
  stateColumns,
  stateLabel,
} from "@/components/tasks/taskCatalogs";

/**
 * F17-T001 — TasksPage conectada ao backend (substitui o mock de
 * `data/dashboardData`). O quadro espelha as enumerações REAIS: as colunas
 * são os 7 estados de `TaskState` e o chip de cada card é o `TaskKind`
 * (catálogo fechado de docs/04). Escrita fica atrás de `Can operate`
 * (exibição; o 403 volta no envelope se escapar). Pendências da F17-T001
 * fechadas: filtros `term/kind/state` na UI (params reais de `GET /tasks`)
 * e transição de estado via `PUT /tasks/{id}/state` (o mapa de transições
 * continua no domínio — fora dele o 409 pt-BR do envelope vira banner).
 *
 * F23-T001 — identificação básica: prioridade, prazo (dueDate) e
 * responsável entram na criação e num formulário de edição
 * (`PUT /tasks/{id}/identification`, comando de substituição). O card
 * mostra o chip de prioridade colorido, o prazo (vencido destacado) e o
 * responsável resolvido pelo backend.
 */

export function TasksPage() {
  const [page, setPage] = useState<PagedTasks | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<TaskItem | null>(null);
  const [title, setTitle] = useState("");
  const [kind, setKind] = useState("Manual");
  const [description, setDescription] = useState("");
  const [priority, setPriority] = useState("Medium");
  const [dueDate, setDueDate] = useState("");
  const [responsibleId, setResponsibleId] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [users, setUsers] = useState<AdminUser[]>([]);

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
      // rootOnly (F23-T004/P4a): subtarefas ficam dentro do detalhe do
      // pai — o quadro mostra só as tasks raiz.
      setPage(await listTasks({
        pageSize: 100,
        rootOnly: true,
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

  // Usuários do seletor de responsável — a listagem exige role Admin/User;
  // Viewer recebe 403 e a falha silencia (select fica só com "Sem").
  useEffect(() => {
    void listUsers()
      .then(setUsers)
      .catch(() => setUsers([]));
  }, []);

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

  function resetForm() {
    setTitle("");
    setDescription("");
    setKind("Manual");
    setPriority("Medium");
    setDueDate("");
    setResponsibleId("");
    setFormError(null);
  }

  /** Edição de identificação (F23-T001) — pré-preenche os 3 campos. */
  function startEdit(task: TaskItem) {
    setCreating(false);
    setPriority(task.priority || "Medium");
    setDueDate(task.dueDate ?? "");
    setResponsibleId(task.responsibleId ?? "");
    setFormError(null);
    setEditing(task);
  }

  /**
   * Submit único: criação (title/kind/description + identificação) ou
   * substituição da identificação (`PUT /tasks/{id}/identification`).
   */
  async function submitForm(event: FormEvent) {
    event.preventDefault();
    setFormError(null);

    if (editing) {
      setSaving(true);
      try {
        await setTaskIdentification(editing.id, {
          priority,
          dueDate: dueDate || undefined,
          responsibleId: responsibleId || undefined,
        });
        const savedTitle = editing.title;
        setEditing(null);
        setNotice(`Identificação de "${savedTitle}" atualizada.`);
        await load();
      } catch (caught) {
        setFormError(messageFor(caught));
      } finally {
        setSaving(false);
      }
      return;
    }

    const trimmed = title.trim();
    if (!trimmed) {
      setFormError("Informe o título da tarefa.");
      return;
    }
    setSaving(true);
    try {
      await createTask({
        title: trimmed,
        kind,
        description: description.trim() || undefined,
        priority,
        dueDate: dueDate || undefined,
        responsibleId: responsibleId || undefined,
      });
      setCreating(false);
      resetForm();
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
  const completedCount = items.filter((task) => task.state === "Resolved" || task.state === "Closed").length;
  const completionPercent = items.length === 0 ? 0 : Math.round((completedCount / items.length) * 100);

  // Opções do responsável: "Sem" + usuários; se o responsável da task em
  // edição não está na lista (usuário removido ou leitura negada), o id
  // aparece truncado para não virar select em branco.
  const responsibleOptions: { value: string; label: string }[] = [
    { value: "", label: "Sem responsável" },
    ...users.map((user) => ({
      value: user.userId,
      label: user.displayName || user.email,
    })),
  ];
  if (
    editing?.responsibleId &&
    !users.some((user) => user.userId === editing.responsibleId)
  ) {
    responsibleOptions.push({
      value: editing.responsibleId,
      label: `Usuário ${editing.responsibleId.slice(0, 8)}`,
    });
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Tarefas"
        subtitle={
          <>
            {loading ? "Carregando…" : `${totalCount} ${totalCount === 1 ? "tarefa" : "tarefas"}`}
            {truncated && ` — exibindo as ${items.length} primeiras`}
            {!loading && hasFilters && " · filtros ativos"}
          </>
        }
        actions={
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <Button
              onClick={() => void load()}
              disabled={loading}
              variant="ghost"
              style={{ padding: "6px 12px", cursor: loading ? "default" : "pointer" }}
            >
              <i className="pi pi-refresh" style={{ fontSize: 11 }} /> Atualizar
            </Button>
            <Can level="operate">
              <Button
                variant="primary"
                onClick={() => {
                  setEditing(null);
                  if (!creating) resetForm();
                  setCreating((value) => !value);
                }}
                style={{ padding: "6px 14px" }}
              >
                <i className="pi pi-plus" style={{ fontSize: 11 }} /> Nova Tarefa
              </Button>
            </Can>
          </div>
        }
      />

      <div className="uni-panel uni-task-progress">
        <div><strong>Conclusão do quadro</strong><span>{completedCount} de {items.length} tarefas resolvidas ou fechadas</span></div>
        <ProgressBar value={completionPercent} aria-label={`Conclusão do quadro: ${completionPercent}%`} />
      </div>

      <form onSubmit={applyFilters} style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        <SearchInput
          value={termInput}
          onChange={setTermInput}
          placeholder="Buscar por título…"
          variant="outlined"
          width={220}
          onEnter={() => { setTerm(termInput.trim()); }}
        />
        <SelectField
          value={kindFilter}
          onChange={setKindFilter}
          placeholder="Todos os tipos"
          options={[
            { value: "", label: "Todos os tipos" },
            ...kindOptions.map((option) => ({ value: option.value, label: option.label })),
          ]}
          style={{ minWidth: 150 }}
        />
        <SelectField
          value={stateFilter}
          onChange={setStateFilter}
          placeholder="Todos os estados"
          options={[
            { value: "", label: "Todos os estados" },
            ...stateColumns.map((column) => ({ value: column.id, label: column.label })),
          ]}
          style={{ minWidth: 150 }}
        />
        <Button type="submit" variant="primary">
          Filtrar
        </Button>
        {hasFilters && (
          <Button type="button" variant="ghost" onClick={clearFilters}>
            Limpar
          </Button>
        )}
      </form>

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      {(creating || editing) && (
        <form onSubmit={submitForm} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>
            {editing ? `Editar identificação — ${editing.title}` : "Nova tarefa"}
          </div>
          {!editing && (
            <>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 220px", gap: 10 }}>
                <FormField label="Título" error={formError ? "Informe o título da tarefa." : null}>
                  <input
                    value={title}
                    onChange={(event) => setTitle(event.target.value)}
                    placeholder="Título *"
                    autoFocus
                    style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
                  />
                </FormField>
                <FormField label="Tipo">
                  <SelectField
                    value={kind}
                    onChange={setKind}
                    options={kindOptions.map((option) => ({ value: option.value, label: option.label }))}
                    style={{ width: "100%" }}
                  />
                </FormField>
              </div>
              <FormField label="Descrição (opcional)">
                <textarea
                  value={description}
                  onChange={(event) => setDescription(event.target.value)}
                  placeholder="Descrição (opcional)"
                  rows={3}
                  style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit" }}
                />
              </FormField>
            </>
          )}
          {/* F23-T001 — identificação básica: prioridade, prazo e
              responsável nos dois modos (no editing são os 3 campos que o
              PUT substitui). */}
          <div style={{ display: "grid", gridTemplateColumns: "180px 180px 1fr", gap: 10 }}>
            <FormField label="Prioridade">
              <SelectField
                value={priority}
                onChange={setPriority}
                options={priorityOptions.map((option) => ({ value: option.value, label: option.label }))}
                style={{ width: "100%" }}
              />
            </FormField>
            <FormField label="Prazo (opcional)">
              <input
                type="date"
                value={dueDate}
                onChange={(event) => setDueDate(event.target.value)}
                style={{ padding: "6px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: "100%" }}
              />
            </FormField>
            <FormField label="Responsável (opcional)">
              <SelectField
                value={responsibleId}
                onChange={setResponsibleId}
                options={responsibleOptions}
                style={{ width: "100%" }}
              />
            </FormField>
          </div>
          {formError && <FormError message={formError} />}
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => { setCreating(false); setEditing(null); resetForm(); }}
              style={{ padding: "6px 14px" }}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={saving}
              style={{ padding: "6px 14px" }}
            >
              {saving ? "Salvando…" : editing ? "Salvar identificação" : "Criar tarefa"}
            </Button>
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
                  <TaskCard key={task.id} task={task} onMove={move} onEdit={startEdit} />
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
  onEdit,
}: {
  task: TaskItem;
  onMove: (task: TaskItem, target: string) => Promise<void>;
  onEdit: (task: TaskItem) => void;
}) {
  const [moving, setMoving] = useState(false);
  const priority = priorityVisual(task.priority);
  const overdue = task.dueDate != null && isOverdue(task.dueDate);
  return (
    <div style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 12, transition: "all 0.12s" }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6, gap: 8 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 4, flexWrap: "wrap" }}>
          <StatusChip
            label={kindLabel(task.kind)}
            background="#f1f5f9"
            color="#475569"
            fontWeight={600}
            fontSize={10}
            style={{ whiteSpace: "nowrap", padding: "2px 6px" }}
          />
          {/* F23-T001 — prioridade com cor por nível. */}
          <StatusChip
            label={priority.label}
            background={priority.background}
            color={priority.color}
            fontWeight={600}
            fontSize={10}
            style={{ whiteSpace: "nowrap", padding: "2px 6px" }}
          />
        </div>
        <span style={{ fontSize: 10, color: "#9ca3af" }}>
          {formatDateTime(new Date(task.openedAt))}
        </span>
      </div>
      {/* F23-T002 — o título abre a tela de detalhe `/tasks/:id`. */}
      <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 6 }}>
        <Link
          to={`/tasks/${task.id}`}
          title="Abrir detalhe da tarefa"
          style={{ color: "inherit", textDecoration: "none" }}
        >
          {task.title}
        </Link>
      </div>
      {task.description && (
        <div style={{
          fontSize: 10, color: "#6b7280", marginBottom: 6,
          display: "-webkit-box", WebkitLineClamp: 2, WebkitBoxOrient: "vertical", overflow: "hidden",
        }}>
          {task.description}
        </div>
      )}
      {/* F23-T004/P4a — progresso derivado das subtarefas (null = sem
          subtarefas ativas → barra some). */}
      {task.progress != null && (
        <div style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 10, marginBottom: 6 }}>
          <div style={{ flex: 1, height: 4, background: "#e5e7eb", borderRadius: 2, overflow: "hidden" }}>
            <div style={{ width: `${task.progress}%`, height: "100%", background: "#0ea5e9" }} />
          </div>
          <span style={{ color: "#64748b", whiteSpace: "nowrap" }}>
            {task.progress}% · {task.subtaskCount}{" "}
            {task.subtaskCount === 1 ? "subtarefa" : "subtarefas"}
          </span>
        </div>
      )}
      {/* F23-T001 — prazo (vencido destacado) e responsável resolvido. */}
      {(task.dueDate || task.responsibleName) && (
        <div style={{ display: "flex", flexWrap: "wrap", gap: 10, fontSize: 10, marginBottom: 6 }}>
          {task.dueDate && (
            <span
              style={{ color: overdue ? "#dc2626" : "#64748b", fontWeight: overdue ? 600 : 400 }}
              title={overdue ? "Prazo vencido" : "Prazo previsto"}
            >
              <i className="pi pi-calendar" style={{ fontSize: 9, marginRight: 3 }} />
              {formatDateOnly(task.dueDate)}{overdue ? " · vencido" : ""}
            </span>
          )}
          {task.responsibleName && (
            <span style={{ color: "#64748b" }} title={`Responsável: ${task.responsibleName}`}>
              <i className="pi pi-user" style={{ fontSize: 9, marginRight: 3 }} />
              {task.responsibleName}
            </span>
          )}
        </div>
      )}
      {(task.assigneeCount > 0 || task.deviceCount > 0 || task.incidentCount > 0) && (
        <div style={{ display: "flex", gap: 10, fontSize: 10, color: "#64748b" }}>
          {task.assigneeCount > 0 && (
            <span title={`${task.assigneeCount} responsável(is)`}>
              <i className="pi pi-users" style={{ fontSize: 9, marginRight: 3 }} />{task.assigneeCount}
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
        <div style={{ marginTop: 8, borderTop: "1px solid #f3f4f6", paddingTop: 8, display: "flex", gap: 8 }}>
          {/* F23-T001 — abre o formulário de identificação no topo. */}
          <Button
            variant="ghost"
            onClick={() => onEdit(task)}
            title="Editar prioridade, prazo e responsável"
            style={{ padding: "4px 8px", fontSize: 11, whiteSpace: "nowrap" }}
          >
            <i className="pi pi-pencil" style={{ fontSize: 10 }} />
          </Button>
          {/* Select controlado por task.state: se a transição falhar (409 do
              mapa do domínio), o re-render restaura o valor anterior — a UI
              nunca mostra um estado que o backend não gravou. */}
          <SelectField
            value={task.state}
            disabled={moving}
            aria-label={`Mover "${task.title}" para outro estado`}
            options={stateColumns.map((column) => ({ value: column.id, label: column.label }))}
            onChange={(target) => {
              if (target === task.state) return;
              setMoving(true);
              void onMove(task, target).finally(() => setMoving(false));
            }}
            style={{ width: "100%", minWidth: 0 }}
          />
        </div>
      </Can>
    </div>
  );
}
