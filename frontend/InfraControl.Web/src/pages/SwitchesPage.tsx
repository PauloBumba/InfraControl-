import { useCallback, useEffect, useRef, useState } from "react";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import {
  listLocations,
  listSwitchPorts,
  type DeviceItem,
  type SwitchPortItem,
} from "@/services/api/inventory";
import { listSwitches } from "@/services/api/network";

/**
 * F17-T020 — Switches & Portas conectada ao backend (substitui
 * `switches`/`switchPortDetail` de `data/dashboardData`).
 *
 * Fontes reais:
 * - `GET /network/switches` (F3-T001) — `PagedResult<DeviceDto>`: switch é
 *   Device com type=Switch (docs/03), então o card traz name/model/ip e a
 *   localização resolvida via `GET /inventory/locations`;
 * - `GET /network/switches/{id}/ports` (F3-T002, serviço do T007) —
 *   `{id, deviceId, name, description, linkUp}`, carregada por seleção.
 *
 * Divergências do protótipo: chip Online/Offline e o contador "X online"
 * (sem endpoint de estado atual — decisão do T007); por card saíram
 * `activePorts/totalPorts`, `poePorts` e `firmware` (sem contrato — não
 * há contagem agregada sem estourar 1 requisição por switch); na tabela
 * de detalhes saíram Dispositivo/Tipo/Velocidade/PoE/VLAN (`SwitchPortDto`
 * não tem nenhum desses — VLAN por porta só existe via
 * `GET /network/switch-ports/{id}/vlans`, 1 requisição POR PORTA, futuro).
 * A grade visual e a contagem do cabeçalho de portas usam o `linkUp`
 * tri-estado real (Up/Down/—). Sem mutations (o mock era somente
 * leitura).
 */

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function SwitchesPage() {
  const [switches, setSwitches] = useState<DeviceItem[]>([]);
  const [switchesTotal, setSwitchesTotal] = useState(0);
  const [locations, setLocations] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [ports, setPorts] = useState<SwitchPortItem[]>([]);
  const [portsTotal, setPortsTotal] = useState(0);
  const [portsLoading, setPortsLoading] = useState(false);
  const [portsError, setPortsError] = useState<string | null>(null);

  // Evita o duplo-fetch do StrictMode; trocar de switch refaz o load.
  const portsLoadedFor = useRef<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [switchPage, locationPage] = await Promise.all([
        listSwitches(),
        listLocations(),
      ]);
      setSwitches(switchPage.items);
      setSwitchesTotal(switchPage.totalCount);
      const map: Record<string, string> = {};
      locationPage.items.forEach((entry) => { map[entry.id] = entry.name; });
      setLocations(map);
      setSelectedId((current) => current ?? switchPage.items[0]?.id ?? null);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const loadPorts = useCallback(async (switchId: string) => {
    setPortsLoading(true);
    setPortsError(null);
    try {
      const page = await listSwitchPorts(switchId);
      setPorts(page.items);
      setPortsTotal(page.totalCount);
    } catch (caught) {
      setPorts([]);
      setPortsTotal(0);
      setPortsError(messageFor(caught));
    } finally {
      setPortsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!selectedId || portsLoadedFor.current === selectedId) return;
    portsLoadedFor.current = selectedId;
    void loadPorts(selectedId);
  }, [selectedId, loadPorts]);

  const retryPorts = () => {
    portsLoadedFor.current = null;
    if (selectedId) {
      portsLoadedFor.current = selectedId;
      void loadPorts(selectedId);
    }
  };

  const selectedSwitch = switches.find((entry) => entry.id === selectedId) ?? null;
  const upCount = ports.filter((port) => port.linkUp === true).length;
  const downCount = ports.filter((port) => port.linkUp === false).length;

  if (loading) {
    return <div style={{ padding: 24, color: "#94a3b8", fontSize: 13 }}>Carregando…</div>;
  }

  if (!loading && switches.length === 0 && switchesTotal === 0 && error) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>{error}</h2>
        <button onClick={() => void load()} style={{ marginTop: 8, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          Tentar de novo
        </button>
      </div>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Switches & Portas</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {switchesTotal} switches
          </p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <select
            value={selectedId ?? ""}
            onChange={(e) => setSelectedId(e.target.value)}
            style={{ padding: "6px 10px", borderRadius: 6, border: "1px solid #e2e8f0", fontSize: 13, color: "#64748b", background: "white" }}
          >
            {switches.map((sw) => (
              <option key={sw.id} value={sw.id}>{sw.name}</option>
            ))}
          </select>
        </div>
      </div>

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 16 }}>
        {switches.map((sw) => (
          <div
            key={sw.id}
            className="uni-panel"
            style={{
              padding: 16,
              cursor: "pointer",
              border: selectedId === sw.id ? "2px solid #3b82f6" : "1px solid #e5e7eb",
            }}
            onClick={() => setSelectedId(sw.id)}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
              <i className="pi pi-server" style={{ color: "#8b5cf6" }} />
              <span style={{ fontWeight: 600, fontSize: 14 }}>{sw.name}</span>
            </div>
            <div style={{ fontSize: 12, color: "#64748b" }}>
              {sw.model ?? sw.type} · {sw.ip ?? "sem IP"} · {locations[sw.locationId] ?? sw.locationId}
            </div>
          </div>
        ))}
        {switches.length === 0 && !error && (
          <div className="uni-panel" style={{ padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
            Nenhum switch cadastrado
          </div>
        )}
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Portas — {selectedSwitch?.name ?? "—"}</h2>
          {!portsLoading && !portsError && (
            <span style={{ fontSize: 12, color: "#64748b" }}>
              {portsTotal} portas · {upCount} up · {downCount} down
            </span>
          )}
        </div>

        {portsError && (
          <ErrorBanner
            message={portsError}
            onRetry={retryPorts}
            style={{ padding: "10px 16px", borderRadius: 0 }}
          />
        )}

        {portsLoading && (
          <div style={{ padding: 24, color: "#94a3b8", fontSize: 13 }}>Carregando…</div>
        )}

        {!portsLoading && !portsError && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(90px, 1fr))", gap: 8, padding: 16 }}>
            {ports.map((port) => {
              const up = port.linkUp === true;
              const down = port.linkUp === false;
              return (
                <div
                  key={port.id}
                  style={{
                    padding: "10px 6px",
                    borderRadius: 6,
                    border: `2px solid ${up ? "#22c55e" : down ? "#ef4444" : "#e5e7eb"}`,
                    background: up ? "#f0fdf4" : down ? "#fef2f2" : "#f9fafb",
                    textAlign: "center",
                    fontSize: 10,
                  }}
                  title={port.description ?? port.name}
                >
                  <div style={{ fontSize: 15, fontWeight: 700 }}>{port.name}</div>
                  <div style={{ fontSize: 8, fontWeight: 700, color: up ? "#16a34a" : down ? "#dc2626" : "#94a3b8" }}>
                    {up ? "Up" : down ? "Down" : "—"}
                  </div>
                </div>
              );
            })}
            {ports.length === 0 && (
              <div style={{ gridColumn: "1 / -1", padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
                Nenhuma porta cadastrada neste switch
              </div>
            )}
          </div>
        )}
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Detalhe de portas</h2></div>
        <table className="uni-clients-table">
          <thead>
            <tr>
              <th style={{ width: 120 }}>Porta</th>
              <th>Descrição</th>
              <th style={{ width: 90 }}>Link</th>
            </tr>
          </thead>
          <tbody>
            {!portsLoading && !portsError && ports.map((port) => (
              <tr key={`${port.id}-row`}>
                <td style={{ fontFamily: "monospace", fontWeight: 600, fontSize: 12 }}>{port.name}</td>
                <td style={{ fontSize: 13, color: "#64748b" }}>{port.description ?? "—"}</td>
                <td>
                  <span
                    style={{
                      fontSize: 11,
                      fontWeight: 500,
                      padding: "2px 8px",
                      borderRadius: 4,
                      background: port.linkUp === true ? "#dcfce7" : port.linkUp === false ? "#fee2e2" : "#f1f5f9",
                      color: port.linkUp === true ? "#16a34a" : port.linkUp === false ? "#dc2626" : "#64748b",
                    }}
                  >
                    {port.linkUp === true ? "Up" : port.linkUp === false ? "Down" : "—"}
                  </span>
                </td>
              </tr>
            ))}
            {!portsLoading && !portsError && ports.length === 0 && (
              <tr>
                <td colSpan={3} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                  Nenhuma porta cadastrada
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
