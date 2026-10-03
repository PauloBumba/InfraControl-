import { useCallback, useEffect, useState } from "react";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { listDevices, type DeviceItem } from "@/services/api/inventory";

/**
 * F17-T014 — WiFiPage conectada ao backend (substitui
 * `wifiNetworks`/`apData` de `data/dashboardData`).
 *
 * O backend NÃO tem domínio WiFi: não existem entidades de rede
 * (SSID/segurança/banda/canal), nem telemetria de AP (clientes/CPU/mem/
 * canais/tx) nem POST de rede — nada disso tem endpoint e tudo saiu
 * (mesma decisão do T008 com as abas DHCP/Rotas/Firewall). O que é real:
 * os APs são devices de `DeviceType.AccessPoint` no inventário, então a
 * página vira a lista desses devices via `GET /inventory/devices?
 * type=AccessPoint` (parâmetro real, do serviço do T007). O CTA "Criar
 * Rede" saiu (sem endpoint — o gate `<Can level="operate">` do F14-T009
 * ficava sem ação por trás).
 */

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function WiFiPage() {
  const [aps, setAps] = useState<DeviceItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await listDevices({ type: "AccessPoint" });
      setAps(result.items);
      setTotalCount(result.totalCount);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const truncated = totalCount > aps.length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div>
        <h1 style={{ fontSize: 20, fontWeight: 700 }}>WiFi</h1>
        <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>
          {loading
            ? "Carregando…"
            : `${totalCount} ${totalCount === 1 ? "access point" : "access points"} · inventário (DeviceType.AccessPoint)`}
        </p>
      </div>

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div>
        <div style={{ display: "flex", alignItems: "baseline", gap: 8, marginBottom: 10 }}>
          <h2 style={{ fontSize: 14, fontWeight: 600 }}>Access Points</h2>
          {truncated && (
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              mostrando {aps.length} de {totalCount} — página completa em Dispositivos
            </span>
          )}
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(300px, 1fr))", gap: 12 }}>
          {aps.map((ap) => (
            <div key={ap.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
                <div style={{ width: 36, height: 36, borderRadius: 8, background: "#f0fdf4", color: "#22c55e", display: "flex", alignItems: "center", justifyContent: "center" }}>
                  <i className="pi pi-wifi" />
                </div>
                <div>
                  <div style={{ fontSize: 13, fontWeight: 600 }}>{ap.name}</div>
                  <div style={{ fontSize: 10, color: "#9ca3af" }}>
                    {ap.model ?? ap.type} · {ap.ip ?? "sem IP"}
                  </div>
                </div>
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, fontSize: 11, color: "#6b7280" }}>
                <div><span style={{ color: "#9ca3af" }}>Vendor:</span> <span style={{ fontWeight: 500 }}>{ap.vendor ?? "—"}</span></div>
                <div><span style={{ color: "#9ca3af" }}>Serial:</span> <span style={{ fontWeight: 500, fontFamily: "monospace" }}>{ap.serial ?? "—"}</span></div>
              </div>
              {ap.description && (
                <div style={{ marginTop: 10, paddingTop: 10, borderTop: "1px solid #f3f4f6", fontSize: 11, color: "#6b7280" }}>
                  {ap.description}
                </div>
              )}
            </div>
          ))}
          {!loading && aps.length === 0 && !error && (
            <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
              Nenhum Access Point no inventário — cadastre devices do tipo AccessPoint em Dispositivos
            </div>
          )}
          {loading && aps.length === 0 && (
            <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
              Carregando…
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
