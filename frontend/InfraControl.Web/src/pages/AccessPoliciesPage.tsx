import { useCallback, useEffect, useMemo, useState } from "react";
import { Button } from "primereact/button";
import { Card } from "primereact/card";
import { Dropdown } from "primereact/dropdown";
import { Message } from "primereact/message";
import { ProgressSpinner } from "primereact/progressspinner";
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
 */

const LEVEL_OPTIONS = [
  { label: "Padrão (ADR-016)", value: null as number | null },
  { label: "Revogar", value: 0 },
  { label: "Ler", value: 1 },
  { label: "Operar", value: 2 },
  { label: "Administrar", value: 3 },
];

const ROLE_ORDER = ["Viewer", "Operator", "Admin"];
const ROLE_LABEL: Record<string, string> = {
  Viewer: "Leitor",
  Operator: "Operador",
  Admin: "Administrador",
};

type CellValue = number | null; // null = segue o default

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
          next[`${mod.key}:${grant.role}`] = grant.isOverride ? grant.level : null;
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
        const baseline = grant.isOverride ? grant.level : null;
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
      // PUT = replace: envia TODAS as células; null = remove override.
      const grants: AccessPolicyGrantInput[] = matrix.modules.flatMap((mod) =>
        mod.roles.map((grant) => ({
          role: grant.role,
          moduleKey: mod.key,
          level: cells[`${mod.key}:${grant.role}`] ?? null,
        })),
      );
      const updated = await updateAccessPolicyGrants(grants);
      setMatrix(updated);
      const next: Record<string, CellValue> = {};
      for (const mod of updated.modules) {
        for (const grant of mod.roles) {
          next[`${mod.key}:${grant.role}`] = grant.isOverride ? grant.level : null;
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
      <div className="flex justify-center py-16">
        <ProgressSpinner />
      </div>
    );
  }

  return (
    <div className="p-4 md:p-6 space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Políticas de acesso</h1>
          <p className="text-sm text-gray-500">
            Defina o nível de cada perfil por módulo. "Padrão" segue a regra
            original (Leitor lê, Operador opera, Admin administra).
          </p>
        </div>
        <div className="flex items-center gap-3">
          {saved && <Message severity="success" text="Políticas salvas." />}
          {error && <Message severity="error" text={error} />}
          <Button
            label="Salvar"
            icon="pi pi-save"
            loading={saving}
            disabled={!dirty}
            onClick={() => void save()}
          />
        </div>
      </div>

      <Card>
        <div className="overflow-x-auto">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="border-b text-left">
                <th className="py-2 pr-4 font-medium">Módulo</th>
                {roles.map((role) => (
                  <th key={role.role} className="py-2 px-2 font-medium text-center">
                    {role.label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {(matrix?.modules ?? []).map((mod) => (
                <tr key={mod.key} className="border-b last:border-0">
                  <td className="py-2 pr-4">
                    <div className="font-medium">{mod.label}</div>
                    <div className="text-xs text-gray-400">{mod.key}</div>
                  </td>
                  {roles.map((role) => {
                    const value = cells[`${mod.key}:${role.role}`] ?? null;
                    const grant = mod.roles.find((r) => r.role === role.role);
                    return (
                      <td key={role.role} className="py-2 px-2 text-center">
                        <Dropdown
                          value={value}
                          options={LEVEL_OPTIONS}
                          onChange={(e) =>
                            setCell(mod.key, role.role, e.value as CellValue)
                          }
                          className="w-44"
                          appendTo="self"
                        />
                        {grant && !grant.isOverride && value === null && (
                          <div className="text-[10px] text-gray-400 mt-1">
                            padrão: {LEVEL_OPTIONS.find((o) => o.value === grant.level)?.label ?? grant.level}
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
      </Card>

      <Card title="Como funciona">
        <ul className="text-sm text-gray-600 space-y-1 list-disc list-inside">
          <li>As alterações valem para as próximas requisições (cache de ~10 min pode atrasar a propagação).</li>
          <li>Revogar (nível 0) bloqueia até a leitura do módulo.</li>
          <li>O Administrador nunca pode ser rebaixado abaixo de "Administrar" no módulo Administração.</li>
          <li>Módulos não listados aqui continuam usando a regra fixa de papéis.</li>
        </ul>
      </Card>
    </div>
  );
}
