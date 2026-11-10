import { useCallback, useEffect, useMemo, useState } from "react";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { Button } from "@/components/ui/Button";
import { SelectField } from "@/components/ui/SelectField";
import {
  getAccessPolicyMatrix,
  updateAccessPolicyGrants,
  type AccessPolicyGrantInput,
  type AccessPolicyMatrix,
} from "@/services/api/accessPolicies";

/**
 * ADR-066 — matriz role×módulo editável pelo admin. Cada célula escolhe o
 * nível efetivo da role no módulo; "Padrão" remove o override e volta ao
 * default do ADR-016 (Viewer→Ler, Operator→Operar, Admin→Administrar).
 *
 * Layout no padrão da casa: PageHeader + uni-panel + banners/LoadingBlock
 * (F50 — páginas F50 antigas usavam Card/Message/ProgressSpinner soltos).
 */

/** SelectField da casa só aceita string — "" = sem override (null na API). */
const LEVEL_OPTIONS = [
  { label: "Padrão (ADR-016)", value: "" },
  { label: "Revogar", value: "0" },
  { label: "Ler", value: "1" },
  { label: "Operar", value: "2" },
  { label: "Administrar", value: "3" },
];

const LEVEL_BY_VALUE: Record<string, string> = {
  "0": "Revogar",
  "1": "Ler",
  "2": "Operar",
  "3": "Administrar",
};

const ROLE_ORDER = ["Viewer", "Operator", "Admin"];
const ROLE_LABEL: Record<string, string> = {
  Viewer: "Leitor",
  Operator: "Operador",
  Admin: "Administrador",
};

type CellValue = string; // "" = segue o default

export function AccessPoliciesPage() {
  const [matrix, setMatrix] = useState<AccessPolicyMatrix | null>(null);
  const [cells, setCells] = useState<Record<string, CellValue>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAccessPolicyMatrix();
      setMatrix(data);
      const next: Record<string, CellValue> = {};
      for (const mod of data.modules) {
        for (const grant of mod.roles) {
          next[`${mod.key}:${grant.role}`] = grant.isOverride ? String(grant.level) : "";
        }
      }
      setCells(next);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao carregar políticas.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const roles = useMemo(() => {
    const first = matrix?.modules[0]?.roles ?? [];
    const customLabels = new Map(
      (matrix?.customRoles ?? []).map((r) => [r.key.toLowerCase(), r.label]),
    );
    return [...first]
      .sort((a, b) => {
        const ai = ROLE_ORDER.indexOf(a.role);
        const bi = ROLE_ORDER.indexOf(b.role);
        return (ai < 0 ? 99 : ai) - (bi < 0 ? 99 : bi);
      })
      .map((r) => ({
        ...r,
        label: ROLE_LABEL[r.role] ?? customLabels.get(r.role.toLowerCase()) ?? r.role,
      }));
  }, [matrix]);

  const setCell = (moduleKey: string, role: string, value: CellValue) => {
    setCells((prev) => ({ ...prev, [`${moduleKey}:${role}`]: value }));
    setSaved(false);
  };

  const dirty = useMemo(() => {
    if (!matrix) return false;
    for (const mod of matrix.modules) {
      for (const grant of mod.roles) {
        const baseline = grant.isOverride ? String(grant.level) : "";
        if (cells[`${mod.key}:${grant.role}`] !== baseline) return true;
      }
    }
    return false;
  }, [matrix, cells]);

  const save = async () => {
    if (!matrix) return;
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      // PUT = replace: envia TODAS as células; "" = remove override.
      const grants: AccessPolicyGrantInput[] = matrix.modules.flatMap((mod) =>
        mod.roles.map((grant) => {
          const raw = cells[`${mod.key}:${grant.role}`] ?? "";
          return {
            role: grant.role,
            moduleKey: mod.key,
            level: raw === "" ? null : Number(raw),
          };
        }),
      );
      const updated = await updateAccessPolicyGrants(grants);
      setMatrix(updated);
      const next: Record<string, CellValue> = {};
      for (const mod of updated.modules) {
        for (const grant of mod.roles) {
          next[`${mod.key}:${grant.role}`] = grant.isOverride ? String(grant.level) : "";
        }
      }
      setCells(next);
      setSaved(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao salvar políticas.");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <PageHeader title="Políticas de acesso" />
        <LoadingBlock />
      </div>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Políticas de acesso"
        subtitle="Defina o nível de cada perfil por módulo. Padrao segue a regra original (Leitor le, Operador opera, Admin administra)."
        actions={
          <Button
            icon="pi pi-save"
            disabled={!dirty || saving}
            onClick={() => void save()}
          >
            {saving ? "Salvando…" : "Salvar"}
          </Button>
        }
      />

      {saved && (
        <NoticeBanner message="Políticas salvas." onDismiss={() => setSaved(false)} />
      )}
      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div className="uni-panel" style={{ padding: 16 }}>
        <div style={{ overflowX: "auto" }}>
          <table
            style={{
              width: "100%",
              fontSize: 13,
              borderCollapse: "collapse",
            }}
          >
            <thead>
              <tr style={{ borderBottom: "1px solid #e5e7eb", textAlign: "left" }}>
                <th style={{ padding: "8px 16px 8px 0", fontWeight: 600 }}>Módulo</th>
                {roles.map((role) => (
                  <th
                    key={role.role}
                    style={{
                      padding: "8px 8px",
                      fontWeight: 600,
                      textAlign: "center",
                    }}
                  >
                    {role.label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {(matrix?.modules ?? []).map((mod) => (
                <tr key={mod.key} style={{ borderBottom: "1px solid #f3f4f6" }}>
                  <td style={{ padding: "8px 16px 8px 0" }}>
                    <div style={{ fontWeight: 600 }}>{mod.label}</div>
                    <div style={{ fontSize: 11, color: "#9ca3af" }}>{mod.key}</div>
                  </td>
                  {roles.map((role) => {
                    const value = cells[`${mod.key}:${role.role}`] ?? "";
                    const grant = mod.roles.find((r) => r.role === role.role);
                    return (
                      <td key={role.role} style={{ padding: "8px 8px", textAlign: "center" }}>
                        <SelectField
                          value={value}
                          options={LEVEL_OPTIONS}
                          onChange={(v) => setCell(mod.key, role.role, v)}
                          aria-label={`${mod.label} — ${role.label}`}
                          style={{ minWidth: 176 }}
                        />
                        {grant && !grant.isOverride && value === "" && (
                          <div style={{ fontSize: 10, color: "#9ca3af", marginTop: 4 }}>
                            padrão: {LEVEL_BY_VALUE[String(grant.level)] ?? grant.level}
                          </div>
                        )}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="uni-panel" style={{ padding: 16 }}>
        <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Como funciona</div>
        <ul
          style={{
            fontSize: 13,
            color: "#6b7280",
            margin: 0,
            paddingLeft: 18,
            display: "grid",
            gap: 4,
            listStyle: "disc",
          }}
        >
          <li>As alterações valem para as próximas requisições (cache de ~10 min pode atrasar a propagação).</li>
          <li>Revogar (nível 0) bloqueia até a leitura do módulo.</li>
          <li>O Administrador nunca pode ser rebaixado abaixo de "Administrar" no módulo Administração.</li>
          <li>Módulos não listados aqui continuam usando a regra fixa de papéis.</li>
        </ul>
      </div>
    </div>
  );
}
