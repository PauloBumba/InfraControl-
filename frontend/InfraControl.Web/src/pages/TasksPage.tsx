import { useCallback, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { Column } from "primereact/column";
import { Button } from "@/components/ui/Button";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { StatusChip } from "@/components/ui/StatusChip";
import { Can } from "@/components/auth/Can";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { SearchInput } from "@/components/ui/SearchInput";
import { FormField, FormError } from "@/components/ui/FormField";
import { SelectField } from "@/components/ui/SelectField";
import { ConfirmDialog } from "@/components/ui/Dialog";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { formatDateTime } from "@/utils/format";
import { ProgressBar } from "primereact/progressbar";
import {
  changeTaskState,
  createTask,
  deleteTask,
  listTasks,
  setTaskIdentification,
  type TaskItem,
} from "@/services/api/tasks";
import {
  useCreateMutation,
  useDeleteMutation,
  useListQuery,
  usePagedQuery,
  useUpdateMutation,
  errorMessage,
  mutationErrorMessage,
} from "@/hooks/queries";
import { useFormErrors } from "@/hooks/useFormErrors";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
import {
  formatDateOnly,
  isOverdue,
  kindLabel,
  kindOptions,
  priorityOptions,
  priorityVisual,
  stateColumns,
  stateLabel,
} from "@/components/tasks/taskCatalogs";

/**
 * F17-T001 + F23-T001 + ADR-030 Fase 5 — TasksPage com React Query
 * (`usePagedQuery` + mutations com invalidação por prefixo `["tasks"]`)
 * e visões tabela/grid/kanban (preferência persistida; default kanban).
 * O quadro espelha as enumerações REAIS: colunas são os 7 estados de
 * `TaskState` e o chip do card é o `TaskKind`. Escrita atrás de
 * `Can operate` (exibição; o 403 volta no envelope se escapar): transição
 * via `PUT /tasks/{id}/state` (mapa no domínio — 409 pt-BR vira banner),
 * identificação via `PUT /tasks/{id}/identification` e exclusão com
 * ConfirmDialog (409 quando há vínculos). Pendências da F17-T001 fechadas:
 * filtros `term/kind/state` (params reais de `GET /tasks`).
 */

export function TasksPage() {
  const [view, setView] = useEntityViewMode("tasks", "kanban");

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<TaskItem | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<TaskItem | null>(null);
  const [title, setTitle] = useState("");
  const [kind, setKind] = useState("Manual");
  const [description, setDescription] = useState("");
  const [priority, setPriority] = useState("Medium");
  const [dueDate, setDueDate] = useState("");
  const [responsibleId, setResponsibleId] = useState("");
  // F29-T001C-2 — a única validação de campo daqui é o título, e ela já saía
  // no `FormField` certo… mas com a mensagem **hardcodada sobre o estado
  // errado**: `error={formError ? "Informe o título da tarefa." : null}` fazia
  // QUALQUER erro (inclusive uma recusa do servidor) acender o Título com uma
  // frase mentirosa, e a mensagem real repetia no `<FormError>` do pé. Cada
  // validação agora declara o próprio campo; recusa do servidor vem sem
  // `field` e fica só no formulário.
  const {
    error: formError,
    field,
    reset: resetFormError,
    fail: failForm,
    errorFor,
  } = useFormErrors();
  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [moving, setMoving] = useState<string | null>(null);

  // Filtros: o input é aplicado no submit (mesmo padrão da ClientsPage) e os
  // valores aplicados é que disparam a recarga.
  const [termInput, setTermInput] = useState("");
  const [term, setTerm] = useState("");
  const [kindFilter, setKindFilter] = useState("");
  const [stateFilter, setStateFilter] = useState("");

  // pageSize no clamp máximo do handler (1..100) — o quadro não pagina.
  // rootOnly (F23-T004/P4a): subtarefas ficam dentro do detalhe do pai —
  // o quadro mostra só as tasks raiz.
  const tasks = usePagedQuery<TaskItem>(["tasks", term, kindFilter, stateFilter], () =>
    listTasks({
      pageSize: 100,
      rootOnly: true,
      term: term || undefined,
      kind: kindFilter || undefined,
      state: stateFilter || undefined,
    }),
  );

  const users = useListQuery<AdminUser>(["users"], () =>
    // 403 de Viewer em /admin/users silencia — select fica só com "Sem".
    listUsers().catch(() => [] as AdminUser[]),
  );

  const createMutation = useCreateMutation({
    invalidate: ["tasks"],
    mutationFn: (input: Parameters<typeof createTask>[0]) => createTask(input),
    onSuccess: () => {
      setNotice("Tarefa criada.");
      setActionError(null);
    },
    // Sem `onError` aqui: `submitForm` já captura a recusa e a manda para o
    // `FormError`. O banner de página mostrava a MESMA frase duas vezes na
    // mesma tela. `stateMutation`/`deleteMutation` mantêm o banner porque
    // não passam por um form.
  });
  const identificationMutation = useUpdateMutation({
    invalidate: ["tasks"],
    mutationFn: (input: { id: string } & Parameters<typeof setTaskIdentification>[1]) => {
      const { id, ...rest } = input;
      return setTaskIdentification(id, rest);
    },
    onSuccess: () => {
      setNotice("Identificação da tarefa atualizada.");
      setActionError(null);
    },
    // Idem createMutation — mesma recusa, um lugar só.
  });
  const stateMutation = useUpdateMutation({
    invalidate: ["tasks"],
    mutationFn: (input: { id: string; state: string }) => changeTaskState(input.id, input.state),
    onSuccess: () => setActionError(null),
    // 409/400 pt-BR do mapa de transições do domínio vira banner.
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });
  const deleteMutation = useDeleteMutation({
    invalidate: ["tasks"],
    mutationFn: (id: string) => deleteTask(id),
    onSuccess: () => {
      setNotice("Tarefa excluída.");
      setActionError(null);
    },
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });

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

  /** Transição de estado — 409/400 pt-BR do envelope vira o banner. */
  const move = useCallback(
    (task: TaskItem, target: string) => {
      setNotice(null);
      setMoving(task.id);
      stateMutation.mutate(
        { id: task.id, state: target },
        { onSettled: () => setMoving(null) },
      );
    },
    [stateMutation],
  );

  function resetForm() {
    setTitle("");
    setDescription("");
    setKind("Manual");
    setPriority("Medium");
    setDueDate("");
    setResponsibleId("");
    resetFormError();
  }

  /** Edição de identificação (F23-T001) — pré-preenche os 3 campos. */
  function startEdit(task: TaskItem) {
    setCreating(false);
    setPriority(task.priority || "Medium");
    setDueDate(task.dueDate ?? "");
    setResponsibleId(task.responsibleId ?? "");
    resetFormError();
    setEditing(task);
  }

  /**
   * Submit único: criação (title/kind/description + identificação) ou
   * substituição da identificação (`PUT /tasks/{id}/identification`).
   */
  async function submitForm(event: FormEvent) {
    event.preventDefault();
    resetFormError();

    if (editing) {
      try {
        await identificationMutation.mutateAsync({
          id: editing.id,
          priority,
          dueDate: dueDate || undefined,
          responsibleId: responsibleId || undefined,
        });
        setEditing(null);
      } catch (caught) {
        // Recusa do servidor não aponta campo: `errorMessage` aceita `unknown`
        // (o `catch` devolve isso) e sempre devolve texto — `fail` exige
        // `string`.
        failForm(errorMessage(caught));
      }
      return;
    }

    const trimmed = title.trim();
    if (!trimmed) {
      failForm("Informe o título da tarefa.", "title");
      return;
    }
    try {
      await createMutation.mutateAsync({
        title: trimmed,
        kind,
        description: description.trim() || undefined,
        priority,
        dueDate: dueDate || undefined,
        responsibleId: responsibleId || undefined,
      });
      setCreating(false);
      resetForm();
    } catch (caught) {
      // Idem: sem `field`, então vai para o nível do formulário.
      failForm(errorMessage(caught));
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    try {
      await deleteMutation.mutateAsync(deleteTarget.id);
      setDeleteTarget(null);
    } catch (caught) {
      setActionError(mutationErrorMessage(caught as Error));
    }
  }

  const items = tasks.items;
  const totalCount = tasks.totalCount;
  const truncated = totalCount > items.length;
  const hasFilters = term !== "" || kindFilter !== "" || stateFilter !== "";
  const completedCount = items.filter((task) => task.state === "Resolved" || task.state === "Closed").length;
  const completionPercent = items.length === 0 ? 0 : Math.round((completedCount / items.length) * 100);
  const listError = mutationErrorMessage(tasks.error as Error | null);

  // Opções do responsável: "Sem" + usuários; se o responsável da task em
  // edição não está na lista (usuário removido ou leitura negada), o id
  // aparece truncado para não virar select em branco.
  const responsibleOptions: { value: string; label: string }[] = [
    { value: "", label: "Sem responsável" },
    ...users.items.map((user) => ({
      value: user.userId,
      label: user.displayName || user.email,
    })),
  ];
  if (
    editing?.responsibleId &&
    !users.items.some((user) => user.userId === editing.responsibleId)
  ) {
    responsibleOptions.push({
      value: editing.responsibleId,
      label: `Usuário ${editing.responsibleId.slice(0, 8)}`,
    });
  }

  return (
    <ListPageShell
      title="Tarefas"
      singular="tarefa"
      plural="tarefas"
      total={totalCount}
      loading={tasks.isPending}
      note={
        [truncated ? `exibindo as ${items.length} primeiras` : "", hasFilters ? "filtros ativos" : ""]
          .filter(Boolean)
          .join(" · ") || undefined
      }
      notice={notice ? <NoticeBanner message={notice} onDismiss={() => setNotice(null)} /> : undefined}
      error={actionError ?? listError ?? null}
      onRetry={() => {
        setActionError(null);
        void tasks.refetch();
      }}
      actions={
        <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
          <ViewSwitcher mode={view} onChange={setView} />
          <Button
            onClick={() => void tasks.refetch()}
            disabled={tasks.isFetching}
            variant="ghost"
            style={{ padding: "6px 12px", cursor: tasks.isFetching ? "default" : "pointer" }}
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
    >

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

      {(creating || editing) && (
        <form onSubmit={submitForm} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>
            {editing ? `Editar identificação — ${editing.title}` : "Nova tarefa"}
          </div>
          {!editing && (
            <>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 220px", gap: 10 }}>
                <FormField label="Título" error={errorFor("title")}>
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
          {/* `field` setado = o recado já está no campo dele; o formulário
              fica calado para não repetir. Sem `field` (recusa do servidor) é
              aqui que ele aparece. */}
          <FormError message={field ? "" : (formError ?? "")} />
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
              disabled={createMutation.isPending || identificationMutation.isPending}
              style={{ padding: "6px 14px" }}
            >
              {createMutation.isPending || identificationMutation.isPending
                ? "Salvando…"
                : editing ? "Salvar identificação" : "Criar tarefa"}
            </Button>
          </div>
        </form>
      )}

      <EntityView<TaskItem>
        mode={view}
        items={items}
        getKey={(task) => task.id}
        emptyMessage={listError ? "—" : "Nenhuma tarefa encontrada"}
        kanban={{
          groupOf: (task) => task.state,
          columns: stateColumns.map((column) => ({ key: column.id, label: column.label })),
        }}
        table={
          <DataTablePanel
            value={items}
            dataKey="id"
            loading={tasks.isPending}
            emptyMessage={listError ? "—" : "Nenhuma tarefa encontrada"}
          >
            <Column
              header="Título"
              body={(task: TaskItem) => (
                <div>
                  <Link
                    to={`/tasks/${task.id}`}
                    title="Abrir detalhe da tarefa"
                    style={{ fontWeight: 500, fontSize: 13, color: "inherit", textDecoration: "none" }}
                  >
                    {task.title}
                  </Link>
                  {task.description && (
                    <div style={{ fontSize: 11, color: "#94a3b8" }}>{task.description}</div>
                  )}
                </div>
              )}
              style={{ minWidth: 240 }}
            />
            <Column header="Tipo" body={(task: TaskItem) => kindLabel(task.kind)} style={{ width: 140 }} />
            <Column
              header="Estado"
              body={(task: TaskItem) => (
                <StatusChip
                  label={stateLabel(task.state)}
                  background="#f1f5f9"
                  color="#475569"
                  fontWeight={600}
                  fontSize={10}
                  style={{ whiteSpace: "nowrap", padding: "2px 6px" }}
                />
              )}
              style={{ width: 130 }}
            />
            <Column
              header="Prioridade"
              body={(task: TaskItem) => {
                const visual = priorityVisual(task.priority);
                return (
                  <StatusChip
                    label={visual.label}
                    background={visual.background}
                    color={visual.color}
                    fontWeight={600}
                    fontSize={10}
                    style={{ whiteSpace: "nowrap", padding: "2px 6px" }}
                  />
                );
              }}
              style={{ width: 110 }}
            />
            <Column
              header="Prazo"
              body={(task: TaskItem) =>
                task.dueDate ? (
                  <span style={{ color: isOverdue(task.dueDate) ? "#dc2626" : "#64748b", fontWeight: isOverdue(task.dueDate) ? 600 : 400 }}>
                    {formatDateOnly(task.dueDate)}{isOverdue(task.dueDate) ? " · vencido" : ""}
                  </span>
                ) : "—"
              }
              style={{ width: 130 }}
            />
            <Column header="Responsável" body={(task: TaskItem) => task.responsibleName ?? "—"} style={{ width: 150 }} />
            <Column
              header="Progresso"
              body={(task: TaskItem) =>
                task.progress != null ? `${task.progress}% · ${task.subtaskCount} sub.` : "—"
              }
              style={{ width: 130 }}
            />
            <Column
              header=""
              style={{ width: 170 }}
              body={(task: TaskItem) => (
                <Can level="operate">
                  <div className="flex gap-1">
                    <Button variant="ghost" onClick={() => startEdit(task)}>
                      Editar
                    </Button>
                    <Button variant="ghost" onClick={() => setDeleteTarget(task)}>
                      Excluir
                    </Button>
                  </div>
                </Can>
              )}
            />
          </DataTablePanel>
        }
        renderCard={(task) => (
          <TaskCard
            task={task}
            moving={moving === task.id}
            onMove={move}
            onEdit={startEdit}
            onDelete={setDeleteTarget}
          />
        )}
      />

      <ConfirmDialog
        visible={deleteTarget !== null}
        onHide={() => setDeleteTarget(null)}
        onConfirm={() => void confirmDelete()}
        header="Excluir tarefa"
        message={
          deleteTarget
            ? `Excluir "${deleteTarget.title}"? Subtarefas e vínculos podem bloquear a exclusão com 409.`
            : ""
        }
        confirmLabel="Excluir"
      />
    </ListPageShell>
  );
}

function TaskCard({
  task,
  moving,
  onMove,
  onEdit,
  onDelete,
}: {
  task: TaskItem;
  moving: boolean;
  onMove: (task: TaskItem, target: string) => void;
  onEdit: (task: TaskItem) => void;
  onDelete: (task: TaskItem) => void;
}) {
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
        <div style={{ marginTop: 8, borderTop: "1px solid #f3f4f6", paddingTop: 8, display: "flex", gap: 8, flexWrap: "wrap" }}>
          {/* F23-T001 — abre o formulário de identificação no topo. */}
          <Button
            variant="ghost"
            onClick={() => onEdit(task)}
            title="Editar prioridade, prazo e responsável"
            style={{ padding: "4px 8px", fontSize: 11, whiteSpace: "nowrap" }}
          >
            <i className="pi pi-pencil" style={{ fontSize: 10 }} />
          </Button>
          <Button
            variant="ghost"
            onClick={() => onDelete(task)}
            title="Excluir tarefa"
            style={{ padding: "4px 8px", fontSize: 11, whiteSpace: "nowrap" }}
          >
            <i className="pi pi-trash" style={{ fontSize: 10 }} />
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
              onMove(task, target);
            }}
            style={{ width: "100%", minWidth: 0 }}
          />
        </div>
      </Can>
    </div>
  );
}
