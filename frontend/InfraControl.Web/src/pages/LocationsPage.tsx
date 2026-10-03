import { useCallback, useEffect, useState } from "react";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import {
  listDevices,
  listLocations,
  listRacks,
  type DeviceItem,
  type LocationItem,
  type RackItem,
} from "@/services/api/inventory";

/**
 * F17-T018 — Locations & Racks conectada ao backend (substitui
 * `locations`/`racks` de `data/dashboardData`).
 *
 * Fontes reais (3 pedidos de leitura, página única de 100 — clamp do
 * endpoint, 1..100):
 * - `GET /inventory/locations` (F2-T001) — `{id, name, description}`;
 * - `GET /inventory/racks` (F2-T002) — `{id, locationId, name, description}`;
 * - `GET /inventory/devices` — só para as **contagens por local**
 *   (agrupamento local por `locationId`; não existe endpoint agregado).
 *
 * Divergências do protótipo: `type` do local (não existe no DTO); em racks
 * saíram a barra de uso de U (`usedUnits`/`units`), potência, temperatura
 * e a lista de devices da rack — **nenhuma dessas métricas tem backend**;
 * o nome do local do rack é resolvido pelo agrupamento de `locationId`.
 * Quando a contagem usa a primeira página truncada (total > 100), os
 * valores por card ganham `~` e um rodapé explica. Sem mutations (o mock
 * era somente leitura).
 */

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function LocationsPage() {
  const [tab, setTab] = useState<"locations" | "racks">("locations");

  const [locationItems, setLocationItems] = useState<LocationItem[]>([]);
  const [locationTotal, setLocationTotal] = useState(0);
  const [rackItems, setRackItems] = useState<RackItem[]>([]);
  const [rackTotal, setRackTotal] = useState(0);
  const [deviceItems, setDeviceItems] = useState<DeviceItem[]>([]);
  const [deviceTotal, setDeviceTotal] = useState(0);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [locations, racks, devices] = await Promise.all([
        listLocations(),
        listRacks(),
        listDevices(),
      ]);
      setLocationItems(locations.items);
      setLocationTotal(locations.totalCount);
      setRackItems(racks.items);
      setRackTotal(racks.totalCount);
      setDeviceItems(devices.items);
      setDeviceTotal(devices.totalCount);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const racksTruncated = rackTotal > rackItems.length;
  const devicesTruncated = deviceTotal > deviceItems.length;
  const countsTruncated = racksTruncated || devicesTruncated;

  const countFor = (list: Array<{ locationId: string }>, locationId: string): number =>
    list.filter((entry) => entry.locationId === locationId).length;

  if (loading) {
    return <div style={{ padding: 24, color: "#94a3b8", fontSize: 13 }}>Carregando…</div>;
  }

  if (!loading && locationItems.length === 0 && locationTotal === 0 && error) {
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
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Locations & Racks</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {locationTotal} locais · {rackTotal} racks · {deviceTotal} dispositivos
          </p>
        </div>
        <div style={{ display: "flex", gap: 4, borderBottom: "1px solid #e2e8f0" }}>
          {([
            { id: "locations", label: "Locations" },
            { id: "racks", label: "Racks" },
          ] as const).map((t) => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              style={{
                padding: "8px 14px",
                fontSize: 13,
                fontWeight: 500,
                color: tab === t.id ? "#3b82f6" : "#64748b",
                background: "none",
                border: "none",
                borderBottom: tab === t.id ? "2px solid #3b82f6" : "2px solid transparent",
                marginBottom: -1,
                cursor: "pointer",
              }}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      {countsTruncated && (
        <p style={{ fontSize: 11, color: "#94a3b8" }}>
          Contagens por local calculadas sobre os primeiros {rackItems.length} racks e {deviceItems.length} dispositivos (marcados com ~)
        </p>
      )}

      {tab === "locations" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 16 }}>
          {locationItems.map((loc) => (
            <div key={loc.id} className="uni-panel" style={{ padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
                <div style={{ width: 36, height: 36, borderRadius: 8, background: "#eff6ff", color: "#3b82f6", display: "flex", alignItems: "center", justifyContent: "center" }}>
                  <i className="pi pi-map-marker" />
                </div>
                <div style={{ fontWeight: 600, fontSize: 14 }}>{loc.name}</div>
              </div>
              <p style={{ fontSize: 12, color: "#64748b", marginBottom: 12 }}>{loc.description ?? "—"}</p>
              <div style={{ display: "flex", gap: 16 }}>
                <div>
                  <div style={{ fontSize: 10, color: "#94a3b8" }}>Racks</div>
                  <div style={{ fontSize: 16, fontWeight: 700 }}>
                    {racksTruncated ? "~" : ""}{countFor(rackItems, loc.id)}
                  </div>
                </div>
                <div>
                  <div style={{ fontSize: 10, color: "#94a3b8" }}>Dispositivos</div>
                  <div style={{ fontSize: 16, fontWeight: 700 }}>
                    {devicesTruncated ? "~" : ""}{countFor(deviceItems, loc.id)}
                  </div>
                </div>
              </div>
            </div>
          ))}
          {locationItems.length === 0 && !error && (
            <div className="uni-panel" style={{ padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
              Nenhum local cadastrado
            </div>
          )}
        </div>
      )}

      {tab === "racks" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(300px, 1fr))", gap: 16 }}>
          {rackItems.map((rack) => (
            <div key={rack.id} className="uni-panel" style={{ padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <i className="pi pi-server" style={{ color: "#8b5cf6" }} />
                  <span style={{ fontWeight: 600, fontSize: 14 }}>{rack.name}</span>
                </div>
                <span style={{ fontSize: 11, color: "#94a3b8" }}>
                  {locationItems.find((loc) => loc.id === rack.locationId)?.name ?? rack.locationId}
                </span>
              </div>
              <p style={{ fontSize: 12, color: "#64748b" }}>{rack.description ?? "—"}</p>
            </div>
          ))}
          {rackItems.length === 0 && !error && (
            <div className="uni-panel" style={{ padding: 24, textAlign: "center", color: "#94a3b8", fontSize: 13 }}>
              Nenhum rack cadastrado
            </div>
          )}
        </div>
      )}
    </div>
  );
}
