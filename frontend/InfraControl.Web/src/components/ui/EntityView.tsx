import { useCallback, useEffect, useState, type ReactNode } from "react";

/**
 * ADR-030 Fase 5 — seletor de visão de lista reutilizável: tabela
 * (DataTable), grid de cards ou kanban (colunas por estado). A preferência
 * é POR ENTIDADE e fica no localStorage (`entityView:<entityKey>`), para o
 * usuário escolher uma vez e a página lembrar.
 *
 * Uso típico na página índice:
 *
 * ```tsx
 * const [mode, setMode] = useEntityViewMode("tasks", "table");
 * ...
 * <ViewSwitcher mode={mode} onChange={setMode} />
 * <EntityView
 *   mode={mode}
 *   items={tasks}
 *   getKey={(t) => t.id}
 *   table={<DataTablePanel ...>...</DataTablePanel>}
 *   renderCard={(t) => <TaskCard ... />}
 *   kanban={{ groupOf: (t) => t.state, columns: TASK_STATE_COLUMNS }}
 * />
 * ```
 */
export type EntityViewMode = "table" | "grid" | "kanban";

const STORAGE_PREFIX = "entityView:";
const MODES: readonly EntityViewMode[] = ["table", "grid", "kanban"];

/** Lê/grava a preferência de visão de uma entidade (localStorage defensivo). */
export function useEntityViewMode(
  entityKey: string,
  fallback: EntityViewMode = "table"
): [EntityViewMode, (mode: EntityViewMode) => void] {
  const storageKey = STORAGE_PREFIX + entityKey;

  const [mode, setModeState] = useState<EntityViewMode>(() => {
    try {
      const stored = localStorage.getItem(storageKey);
      return MODES.includes(stored as EntityViewMode)
        ? (stored as EntityViewMode)
        : fallback;
    } catch {
      return fallback;
    }
  });

  const setMode = useCallback(
    (next: EntityViewMode) => {
      setModeState(next);
      try {
        localStorage.setItem(storageKey, next);
      } catch {
        // modo privado/quota — segue com a preferência só em memória.
      }
    },
    [storageKey]
  );

  // Sincroniza entre abas/janelas do mesmo origin.
  useEffect(() => {
    const onStorage = (event: StorageEvent) => {
      if (event.key !== storageKey) return;
      if (MODES.includes(event.newValue as EntityViewMode)) {
        setModeState(event.newValue as EntityViewMode);
      }
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, [storageKey]);

  return [mode, setMode];
}

const SWITCHER_ITEMS: ReadonlyArray<{
  id: EntityViewMode;
  icon: string;
  label: string;
}> = [
  { id: "table", icon: "pi pi-table", label: "Tabela" },
  { id: "grid", icon: "pi pi-th-large", label: "Grid" },
  { id: "kanban", icon: "pi pi-bars", label: "Kanban" },
];

/** Segmentado de 3 ícones (tabela/grid/kanban). */
export function ViewSwitcher({
  mode,
  onChange,
  labels,
}: {
  mode: EntityViewMode;
  onChange: (mode: EntityViewMode) => void;
  /**
   * Sobrescreve os rótulos padrão. Existe porque o slot `table` aceita
   * qualquer ReactNode — a NotificationsPage coloca um `Timeline` ali e
   * o 1º modo passa a se chamar "Linha do tempo". Prop opcional: as
   * páginas que já usam o componente continuam iguais.
   */
  labels?: Partial<Record<EntityViewMode, string>>;
}) {
  return (
    <div
      role="tablist"
      aria-label="Modo de visualização"
      className="flex gap-1"
      style={{
        border: "1px solid #e2e8f0",
        borderRadius: 8,
        padding: 2,
        background: "#f8fafc",
      }}
    >
      {SWITCHER_ITEMS.map((item) => {
        const active = mode === item.id;
        const label = labels?.[item.id] ?? item.label;
        return (
          <button
            key={item.id}
            type="button"
            role="tab"
            aria-selected={active}
            title={label}
            onClick={() => onChange(item.id)}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 6,
              padding: "5px 10px",
              fontSize: 12,
              fontWeight: 500,
              color: active ? "#1d4ed8" : "#64748b",
              background: active ? "#ffffff" : "transparent",
              border: "none",
              borderRadius: 6,
              boxShadow: active ? "0 1px 2px rgba(15,23,42,.08)" : "none",
              cursor: "pointer",
            }}
          >
            <i className={item.icon} />
            <span>{label}</span>
          </button>
        );
      })}
    </div>
  );
}

export interface KanbanColumn {
  /** Valor do `groupOf` que cai nesta coluna. */
  key: string;
  label: string;
}

export interface EntityViewProps<T> {
  mode: EntityViewMode;
  items: readonly T[];
  getKey: (item: T) => string;
  /** Conteúdo da visão tabela (normalmente um `<DataTablePanel>` pronto). */
  table: ReactNode;
  /** Card de item — usado pelas visões grid E kanban. */
  renderCard: (item: T) => ReactNode;
  /** Configuração da visão kanban. Sem ela, kanban cai para grid. */
  kanban?: {
    groupOf: (item: T) => string;
    columns: readonly KanbanColumn[];
    /** Itens com grupo fora de `columns` caem aqui (padrão: última coluna). */
    overflowColumnKey?: string;
  };
  emptyMessage?: string;
}

/** Renderiza a visão ativa (tabela / grid de cards / kanban). */
export function EntityView<T>({
  mode,
  items,
  getKey,
  table,
  renderCard,
  kanban,
  emptyMessage = "Nenhum registro encontrado",
}: EntityViewProps<T>) {
  if (mode === "table") {
    return <>{table}</>;
  }

  if (items.length === 0) {
    return (
      <div
        className="uni-panel"
        style={{ padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}
      >
        {emptyMessage}
      </div>
    );
  }

  if (mode === "grid" || !kanban) {
    return (
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))",
          gap: 16,
        }}
      >
        {items.map((item) => (
          <div key={getKey(item)}>{renderCard(item)}</div>
        ))}
      </div>
    );
  }

  const overflowKey =
    kanban.overflowColumnKey ?? kanban.columns[kanban.columns.length - 1]?.key;
  const buckets = new Map<string, T[]>();
  for (const column of kanban.columns) buckets.set(column.key, []);
  const extra: T[] = [];

  for (const item of items) {
    const group = kanban.groupOf(item);
    const bucket = buckets.get(group);
    if (bucket) bucket.push(item);
    else if (group === overflowKey) extra.push(item);
    else if (overflowKey) buckets.get(overflowKey)?.push(item);
    else extra.push(item);
  }
  if (overflowKey && extra.length > 0) buckets.get(overflowKey)?.push(...extra);

  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: `repeat(${kanban.columns.length}, minmax(220px, 1fr))`,
        gap: 12,
        alignItems: "start",
      }}
    >
      {kanban.columns.map((column) => {
        const bucket = buckets.get(column.key) ?? [];
        return (
          <div key={column.key} className="uni-panel" style={{ padding: 10 }}>
            <div
              className="flex align-items-center justify-content-between"
              style={{ marginBottom: 8, padding: "0 2px" }}
            >
              <span style={{ fontSize: 12, fontWeight: 600, color: "#334155" }}>
                {column.label}
              </span>
              <span
                style={{
                  fontSize: 11,
                  fontWeight: 600,
                  color: "#64748b",
                  background: "#f1f5f9",
                  borderRadius: 999,
                  padding: "1px 8px",
                }}
              >
                {bucket.length}
              </span>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {bucket.map((item) => (
                <div key={getKey(item)}>{renderCard(item)}</div>
              ))}
              {bucket.length === 0 && (
                <div
                  style={{
                    fontSize: 11,
                    color: "#94a3b8",
                    textAlign: "center",
                    padding: "10px 0",
                  }}
                >
                  Vazio
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
