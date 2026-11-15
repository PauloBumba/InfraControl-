import { useCallback, useEffect, useMemo, useState } from "react";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { Button } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/StatusChip";
import {
  getSchemaCatalog,
  type SchemaEntity,
} from "@/services/api/schema";
import { ErDiagram } from "@/components/schema/ErDiagram";
import { SelectButton } from "primereact/selectbutton";

/**
 * Dicionário de dados dinâmico: entidades, tabelas, colunas e
 * relacionamentos (FKs) lidos do modelo EF no backend — tabela nova
 * aparece aqui sozinha, sem código de frontend.
 */
export function SchemaPage() {
  const [entities, setEntities] = useState<SchemaEntity[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<SchemaEntity | null>(null);
  const [view, setView] = useState<"list" | "diagram">("list");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setEntities(await getSchemaCatalog());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao carregar o esquema.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return entities;
    return entities.filter(
      (e) =>
        e.entityName.toLowerCase().includes(q) ||
        e.tableName.toLowerCase().includes(q) ||
        e.properties.some((p) => p.name.toLowerCase().includes(q)),
    );
  }, [entities, search]);

  const fkCount = (e: SchemaEntity) =>
    e.properties.filter((p) => p.foreignKeyTarget != null).length;

  const diagramCenter =
    entities.find((e) => e.entityName === selected?.entityName) ??
    filtered[0] ??
    entities[0] ??
    null;

  return (
    <ListPageShell
      title="Esquema do banco"
      singular="entidade"
      plural="entidades"
      total={filtered.length}
      loading={loading}
      note="Dicionário de dados vivo: entidades, colunas e relacionamentos descobertos do modelo."
      error={error}
      onRetry={() => void load()}
      actions={
        <div className="flex align-items-center gap-2">
          <SelectButton
            className="uni-select-button"
            value={view}
            options={[
              { label: "Lista", value: "list" },
              { label: "Diagrama (DER)", value: "diagram" },
            ]}
            optionLabel="label"
            optionValue="value"
            allowEmpty={false}
            onChange={(e) => setView(e.value)}
          />
          <Button
            variant="ghost"
            icon="pi pi-refresh"
            title="Atualizar"
            aria-label="Atualizar"
            disabled={loading}
            onClick={() => void load()}
          />
        </div>
      }
    >
      {!loading && view === "diagram" && diagramCenter && (
        <div className="uni-panel" style={{ padding: 16 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 10, flexWrap: "wrap" }}>
            <span style={{ fontSize: 12, color: "#64748b" }}>
              Entidade central:
            </span>
            <select
              className="uni-login-input"
              style={{ maxWidth: 280 }}
              value={diagramCenter.entityName}
              onChange={(e) => {
                const found = entities.find((x) => x.entityName === e.target.value);
                if (found) setSelected(found);
              }}
              aria-label="Entidade central do diagrama"
            >
              {entities.map((e) => (
                <option key={e.entityName} value={e.entityName}>
                  {e.entityName} ({e.tableName})
                </option>
              ))}
            </select>
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              Clique numa entidade vizinha para recentralizar · PK = chave primária · ⧉ = FK
            </span>
          </div>
          <ErDiagram
            entities={entities}
            center={diagramCenter}
            onSelect={setSelected}
          />
        </div>
      )}
      {!loading && view === "list" && (
        <div style={{ display: "flex", gap: 16, alignItems: "flex-start" }}>
          <div className="uni-panel" style={{ padding: 16, flex: 1, minWidth: 0 }}>
            <div style={{ marginBottom: 12 }}>
              <input
                type="search"
                className="uni-login-input"
                style={{ maxWidth: 360 }}
                placeholder="Buscar entidade, tabela ou coluna..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                aria-label="Buscar no esquema"
              />
            </div>
            <div style={{ overflowX: "auto", maxHeight: 560, overflowY: "auto" }}>
              <table
                style={{
                  width: "100%",
                  fontSize: 13,
                  borderCollapse: "collapse",
                }}
              >
                <thead>
                  <tr
                    style={{
                      borderBottom: "1px solid #e5e7eb",
                      textAlign: "left",
                      position: "sticky",
                      top: 0,
                      background: "#fff",
                    }}
                  >
                    <th style={{ padding: "8px 12px 8px 0", fontWeight: 600 }}>
                      Entidade
                    </th>
                    <th style={{ padding: "8px 12px 8px 0", fontWeight: 600 }}>
                      Tabela
                    </th>
                    <th style={{ padding: "8px 12px 8px 0", fontWeight: 600 }}>
                      Colunas
                    </th>
                    <th style={{ padding: "8px 0 8px 8px", fontWeight: 600 }}>
                      FKs
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((e) => (
                    <tr
                      key={e.entityName}
                      onClick={() => setSelected(e)}
                      style={{
                        borderBottom: "1px solid #f3f4f6",
                        cursor: "pointer",
                        background:
                          selected?.entityName === e.entityName
                            ? "#eff6ff"
                            : undefined,
                      }}
                    >
                      <td style={{ padding: "8px 12px 8px 0", fontWeight: 500 }}>
                        {e.entityName}
                      </td>
                      <td style={{ padding: "8px 12px 8px 0", color: "#6b7280" }}>
                        {e.tableName}
                      </td>
                      <td style={{ padding: "8px 12px 8px 0" }}>{e.properties.length}</td>
                      <td style={{ padding: "8px 0 8px 8px" }}>
                        {fkCount(e) > 0 ? (
                          <StatusChip
                            label={String(fkCount(e))}
                            background="#fef3c7"
                            color="#b45309"
                          />
                        ) : (
                          <span style={{ color: "#9ca3af" }}>—</span>
                        )}
                      </td>
                    </tr>
                  ))}
                  {filtered.length === 0 && (
                    <tr>
                      <td
                        colSpan={4}
                        style={{ padding: 24, textAlign: "center", color: "#9ca3af" }}
                      >
                        Nenhuma entidade encontrada.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {selected && (
            <div
              className="uni-panel"
              style={{
                padding: 16,
                width: 420,
                flexShrink: 0,
                maxHeight: 640,
                overflowY: "auto",
              }}
            >
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  marginBottom: 8,
                }}
              >
                <div>
                  <strong style={{ fontSize: 15 }}>{selected.entityName}</strong>
                  <div style={{ fontSize: 12, color: "#6b7280" }}>
                    {selected.tableName}
                  </div>
                </div>
                <Button
                  variant="ghost"
                  icon="pi pi-times"
                  title="Fechar"
                  aria-label="Fechar detalhes"
                  onClick={() => setSelected(null)}
                />
              </div>
              <table
                style={{
                  width: "100%",
                  fontSize: 12,
                  borderCollapse: "collapse",
                }}
              >
                <thead>
                  <tr style={{ borderBottom: "1px solid #e5e7eb", textAlign: "left" }}>
                    <th style={{ padding: "6px 8px 6px 0", fontWeight: 600 }}>Coluna</th>
                    <th style={{ padding: "6px 8px 6px 0", fontWeight: 600 }}>Tipo</th>
                    <th style={{ padding: "6px 0 6px 8px", fontWeight: 600 }}>Relacionamento</th>
                  </tr>
                </thead>
                <tbody>
                  {selected.properties.map((p) => (
                    <tr key={p.name} style={{ borderBottom: "1px solid #f3f4f6" }}>
                      <td style={{ padding: "6px 8px 6px 0" }}>
                        {p.name}
                        {p.isPrimaryKey && (
                          <StatusChip
                            label="PK"
                            background="#dbeafe"
                            color="#1d4ed8"
                            style={{ marginLeft: 6 }}
                          />
                        )}
                        {p.isNullable && (
                          <span style={{ color: "#9ca3af", marginLeft: 6 }}>∅</span>
                        )}
                      </td>
                      <td style={{ padding: "6px 8px 6px 0", color: "#6b7280" }}>
                        {p.clrType}
                      </td>
                      <td style={{ padding: "6px 0 6px 8px" }}>
                        {p.foreignKeyTarget ? (
                          <StatusChip
                            label={`→ ${p.foreignKeyTarget}`}
                            background="#fef3c7"
                            color="#b45309"
                          />
                        ) : (
                          <span style={{ color: "#d1d5db" }}>—</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </ListPageShell>
  );
}
