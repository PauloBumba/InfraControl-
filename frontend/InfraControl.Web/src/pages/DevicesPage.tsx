import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { PageHeader } from "@/components/ui/PageHeader";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { SelectField } from "@/components/ui/SelectField";

import { useNavigate } from "react-router-dom";
import { Can } from "@/components/auth/Can";
import {
  INVENTORY_PAGE_SIZE,
  createDevice,
  listDevices,
  listLocations,
  listSwitchPorts,
  type DeviceItem,
  type LocationItem,
  type Paged,
  type SwitchPortItem,
} from "@/services/api/inventory";

/**
 * F17-T007 — DevicesPage conectada ao backend (substitui `allDevices`,
 * `sectorMap` e `switchPortMap` de `data/deviceData`). Contrato real:
 * `GET /inventory/devices` (`DeviceDto`, filtro real `type`/`term`, teto de
 * 100/página) + `GET /inventory/locations` (resolve o `locationId` = "setor")
 * + `GET /network/switches/{id}/ports` (`SwitchPortDto`, `linkUp` tri-estado).
 *
 * Sem contrato (saíram): status online/atenção/offline (não existe endpoint
 * de estado atual — histórico de polls é por device), uptime, firmware,
 * "VLAN n" (o DTO traz `vlanId` Guid, sem número/nome), switch de uplink,
 * storage, PoE/speed/VLAN por porta e a lista de clientes (domínio
 * separado). Entraram: vendor/serial no card e contagem real "ativas/total"
 * das portas (`linkUp === true`).
 *
 * Filtro por tipo é client-side sobre a página carregada (os contadores de
 * cada tipo exigiriam N pedidos; o parâmetro `type` do endpoint fica
 * documentado como caminho de escala). Portas dos switches só são buscadas
 * quando a aba "Por Switch" é aberta (1 pedido por switch — sem endpoint
 * agregado).
 */

const typeIcons: Record<string, string> = {
  Server: "pi pi-desktop",
  Workstation: "pi pi-desktop",
  Switch: "pi pi-server",
  Router: "pi pi-sitemap",
  Firewall: "pi pi-shield",
  AccessPoint: "pi pi-wifi",
  Printer: "pi pi-print",
  Camera: "pi pi-video",
  Phone: "pi pi-phone",
  UPS: "pi pi-bolt",
  Other: "pi pi-circle",
};
const typeColors: Record<string, string> = {
  Server: "#3b82f6",
  Workstation: "#8b5cf6",
  Switch: "#8b5cf6",
  Router: "#3b82f6",
  Firewall: "#ec4899",
  AccessPoint: "#22c55e",
  Printer: "#f59e0b",
  Camera: "#ec4899",
  Phone: "#06b6d4",
  UPS: "#f59e0b",
  Other: "#94a3b8",
};
/** Rótulos pt-BR para os nomes do enum `DeviceType` (PascalCase no JSON). */
const typeNames: Record<string, string> = {
  Server: "Servidor",
  Workstation: "Estação de Trabalho",
  Switch: "Switch",
  Router: "Roteador",
  Firewall: "Firewall",
  AccessPoint: "Access Point",
  Printer: "Impressora",
  Camera: "Câmera",
  Phone: "Telefone",
  UPS: "UPS",
  Other: "Outro",
};
const DEVICE_TYPES = Object.keys(typeNames);

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function DevicesPage() {
  const navigate = useNavigate();
  const [pageData, setPageData] = useState<Paged<DeviceItem> | null>(null);
  const [locations, setLocations] = useState<LocationItem[]>([]);
  const [portsBySwitch, setPortsBySwitch] = useState<Map<string, SwitchPortItem[]>>(new Map());
  const [portsLoading, setPortsLoading] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [tab, setTab] = useState<"inventory" | "sector" | "switch">("inventory");
  const [filter, setFilter] = useState("all");

  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [type, setType] = useState("");
  const [locationId, setLocationId] = useState("");
  const [ip, setIp] = useState("");
  const [model, setModel] = useState("");
  const [vendor, setVendor] = useState("");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [devicePage, locationPage] = await Promise.all([
        listDevices(),
        listLocations(),
      ]);
      setPageData(devicePage);
      setLocations(locationPage.items);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  // Portas por switch: só quando a aba "Por Switch" é aberta (1 pedido por
  // switch; `cancelled` evita estado obsoleto se a aba mudar no meio).
  useEffect(() => {
    if (tab !== "switch") return;
    const switches = (pageData?.items ?? []).filter((device) => device.type === "Switch");
    if (switches.length === 0) {
      setPortsBySwitch(new Map());
      return;
    }
    let cancelled = false;
    setPortsLoading(true);
    Promise.all(
      switches.map(
        async (device) =>
          [device.id, (await listSwitchPorts(device.id)).items] as const,
      ),
    )
      .then((entries) => {
        if (!cancelled) setPortsBySwitch(new Map(entries));
      })
      .catch((caught: unknown) => {
        if (!cancelled) setError(messageFor(caught));
      })
      .finally(() => {
        if (!cancelled) setPortsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [tab, pageData]);

  async function submitCreate(event: FormEvent) {
    event.preventDefault();
    const trimmedName = name.trim();
    if (!trimmedName) {
      setFormError("Informe o nome do dispositivo.");
      return;
    }
    if (!type) {
      setFormError("Selecione o tipo do dispositivo.");
      return;
    }
    if (!locationId) {
      setFormError("Selecione a localização do dispositivo.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      await createDevice({
        locationId,
        name: trimmedName,
        type,
        ip: ip.trim() || undefined,
        model: model.trim() || undefined,
        vendor: vendor.trim() || undefined,
        description: description.trim() || undefined,
      });
      setCreating(false);
      setName("");
      setType("");
      setLocationId("");
      setIp("");
      setModel("");
      setVendor("");
      setDescription("");
      setNotice(`Dispositivo "${trimmedName}" criado.`);
      await load();
    } catch (caught) {
      setFormError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  }

  const items = pageData?.items ?? [];
  const totalCount = pageData?.totalCount ?? 0;
  const locationName = (id: string): string =>
    locations.find((location) => location.id === id)?.name ?? "Sem localização";

  const typeCounts: Record<string, number> = {};
  items.forEach((device) => {
    typeCounts[device.type] = (typeCounts[device.type] ?? 0) + 1;
  });
  const presentTypes = DEVICE_TYPES.filter((deviceType) => (typeCounts[deviceType] ?? 0) > 0);

  const filtered = filter === "all" ? items : items.filter((device) => device.type === filter);
  const switches = items.filter((device) => device.type === "Switch");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Dispositivos"
        subtitle={
          loading ? (
            "Carregando…"
          ) : (
            <>
              {totalCount} {totalCount === 1 ? "dispositivo" : "dispositivos"}
              {items.length < totalCount && ` · mostrando ${items.length} (refine por tipo)`}
            </>
          )
        }
        actions={
          <Can level="operate">
            <Button
              variant="primary"
              onClick={() => { setCreating((value) => !value); setFormError(null); }}
              style={{ padding: "6px 14px" }}
            >
              <i className="pi pi-plus" style={{ fontSize: 11 }} /> Adicionar
            </Button>
          </Can>
        }
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      {creating && (
        <form onSubmit={submitCreate} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Novo dispositivo</div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 10 }}>
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Nome *"
              autoFocus
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
            <SelectField
              value={type}
              onChange={setType}
              placeholder="Tipo *"
              options={[
                { value: "", label: "Tipo *" },
                ...DEVICE_TYPES.map((deviceType) => ({
                  value: deviceType,
                  label: typeNames[deviceType] ?? deviceType,
                })),
              ]}
            />
            <SelectField
              value={locationId}
              onChange={setLocationId}
              placeholder="Localização *"
              options={[
                { value: "", label: "Localização *" },
                ...locations.map((location) => ({
                  value: location.id,
                  label: location.name,
                })),
              ]}
            />
            <input
              value={ip}
              onChange={(event) => setIp(event.target.value)}
              placeholder="IP"
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
            <input
              value={model}
              onChange={(event) => setModel(event.target.value)}
              placeholder="Modelo"
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
            <input
              value={vendor}
              onChange={(event) => setVendor(event.target.value)}
              placeholder="Fabricante"
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </div>
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Descrição (opcional)"
            rows={2}
            style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit" }}
          />
          {formError && <div style={{ fontSize: 12, color: "#b91c1c" }}>{formError}</div>}
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => { setCreating(false); setFormError(null); }}
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
              {saving ? "Salvando…" : "Criar dispositivo"}
            </Button>
          </div>
        </form>
      )}

      <div style={{ display: "flex", gap: 4, borderBottom: "2px solid #f3f4f6", paddingBottom: 0 }}>
        {[
          { key: "inventory" as const, label: "Inventário", icon: "pi pi-list" },
          { key: "sector" as const, label: "Por Setor", icon: "pi pi-map" },
          { key: "switch" as const, label: "Por Switch", icon: "pi pi-server" },
        ].map((entry) => (
          <button
            key={entry.key}
            onClick={() => setTab(entry.key)}
            style={{
              display: "flex", alignItems: "center", gap: 6, padding: "8px 14px", border: "none",
              borderBottom: tab === entry.key ? "2px solid #3b82f6" : "2px solid transparent",
              marginBottom: -2, background: "transparent",
              color: tab === entry.key ? "#3b82f6" : "#6b7280",
              fontSize: 12, fontWeight: tab === entry.key ? 600 : 400, cursor: "pointer",
            }}
          >
            <i className={entry.icon} style={{ fontSize: 12 }} /> {entry.label}
          </button>
        ))}
      </div>

      {tab === "inventory" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            <button
              onClick={() => setFilter("all")}
              style={{ padding: "5px 10px", borderRadius: 6, border: `1.5px solid ${filter === "all" ? "#3b82f6" : "#e5e7eb"}`, background: filter === "all" ? "#3b82f612" : "white", color: filter === "all" ? "#3b82f6" : "#6b7280", fontSize: 11, fontWeight: 500, cursor: "pointer" }}
            >
              Todos ({items.length})
            </button>
            {presentTypes.map((deviceType) => (
              <button
                key={deviceType}
                onClick={() => setFilter(deviceType)}
                style={{ padding: "5px 10px", borderRadius: 6, border: `1.5px solid ${filter === deviceType ? (typeColors[deviceType] ?? "#94a3b8") : "#e5e7eb"}`, background: filter === deviceType ? `${typeColors[deviceType] ?? "#94a3b8"}12` : "white", color: filter === deviceType ? (typeColors[deviceType] ?? "#94a3b8") : "#6b7280", fontSize: 11, fontWeight: 500, cursor: "pointer" }}
              >
                {typeNames[deviceType] ?? deviceType} ({typeCounts[deviceType]})
              </button>
            ))}
          </div>
          <div className="uni-devices-grid">
            {filtered.map((device) => {
              const color = typeColors[device.type] ?? "#94a3b8";
              return (
                <div key={device.id} className="uni-device-card" onClick={() => navigate(`/devices/${device.id}`)}>
                  <div className="uni-device-top">
                    <div className={`uni-device-icon ${device.type}`} style={{ background: `${color}15`, color }}>
                      <i className={typeIcons[device.type] ?? "pi pi-circle"} />
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="uni-device-name">{device.name}</div>
                      <div className="uni-device-model">{device.model ?? typeNames[device.type] ?? device.type}</div>
                    </div>
                    <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: `${color}12`, color, whiteSpace: "nowrap" }}>
                      {typeNames[device.type] ?? device.type}
                    </span>
                  </div>
                  <div className="uni-device-details">
                    <div className="uni-device-detail"><i className="pi pi-desktop" />{device.ip ?? "IP não informado"}</div>
                    <div className="uni-device-detail"><i className="pi pi-map-marker" />{locationName(device.locationId)}</div>
                    {device.vendor && <div className="uni-device-detail"><i className="pi pi-tag" />{device.vendor}</div>}
                  </div>
                  <div className="uni-device-bottom">
                    {device.model && <div className="uni-device-stat"><i className="pi pi-server" />{device.model}</div>}
                    {device.serial && <div className="uni-device-stat"><i className="pi pi-hashtag" />{device.serial}</div>}
                  </div>
                </div>
              );
            })}
            {!loading && filtered.length === 0 && (
              <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13, background: "white", borderRadius: 8, border: "1px solid #e5e7eb" }}>
                {error ? "—" : "Nenhum dispositivo encontrado"}
              </div>
            )}
            {loading && filtered.length === 0 && (
              <LoadingBlock style={{ gridColumn: "1 / -1", background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }} />
            )}
          </div>
        </div>
      )}

      {tab === "sector" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(340px, 1fr))", gap: 12 }}>
          {locations.map((location) => {
            const locationDevices = items.filter((device) => device.locationId === location.id);
            return (
              <div key={location.id} className="uni-panel" style={{ overflow: "hidden" }}>
                <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "12px 16px", borderBottom: "1px solid #f3f4f6" }}>
                  <div style={{ width: 32, height: 32, borderRadius: 6, background: "#eff6ff", color: "#3b82f6", display: "flex", alignItems: "center", justifyContent: "center" }}>
                    <i className="pi pi-map-marker" />
                  </div>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 13, fontWeight: 600 }}>{location.name}</div>
                    <div style={{ fontSize: 10, color: "#9ca3af" }}>{location.description ?? "—"}</div>
                  </div>
                  <span style={{ fontSize: 11, color: "#64748b" }}>
                    {locationDevices.length} {locationDevices.length === 1 ? "device" : "devices"}
                  </span>
                </div>
                <div className="uni-panel-body" style={{ padding: "10px 16px" }}>
                  {locationDevices.length === 0 && (
                    <div style={{ fontSize: 11, color: "#9ca3af", textAlign: "center", padding: 8 }}>
                      Nenhum dispositivo neste setor
                    </div>
                  )}
                  <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                    {locationDevices.map((device) => {
                      const color = typeColors[device.type] ?? "#94a3b8";
                      return (
                        <div
                          key={device.id}
                          onClick={() => navigate(`/devices/${device.id}`)}
                          style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 8px", borderRadius: 4, cursor: "pointer", background: "#f9fafb", border: "1px solid #f3f4f6" }}
                        >
                          <div style={{ width: 22, height: 22, borderRadius: 4, background: `${color}12`, color, display: "flex", alignItems: "center", justifyContent: "center", fontSize: 10, flexShrink: 0 }}>
                            <i className={typeIcons[device.type] ?? "pi pi-circle"} />
                          </div>
                          <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{ fontSize: 11, fontWeight: 500, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{device.name}</div>
                            <div style={{ fontSize: 9, color: "#9ca3af", fontFamily: "monospace" }}>{device.ip ?? "—"}</div>
                          </div>
                          <span style={{ fontSize: 9, color: "#9ca3af" }}>{typeNames[device.type] ?? device.type}</span>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </div>
            );
          })}
          {!loading && locations.length === 0 && (
            <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13, background: "white", borderRadius: 8, border: "1px solid #e5e7eb" }}>
              {error ? "—" : "Nenhuma localização cadastrada"}
            </div>
          )}
          {loading && locations.length === 0 && (
            <LoadingBlock style={{ gridColumn: "1 / -1", background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }} />
          )}
        </div>
      )}

      {tab === "switch" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {switches.map((device) => {
            const ports = portsBySwitch.get(device.id) ?? [];
            const activePorts = ports.filter((port) => port.linkUp === true).length;
            return (
              <div key={device.id} className="uni-panel" style={{ overflow: "hidden" }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "12px 16px", borderBottom: "1px solid #f3f4f6", flexWrap: "wrap", gap: 8 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                    <div style={{ width: 36, height: 36, borderRadius: 8, background: "#8b5cf612", color: "#8b5cf6", display: "flex", alignItems: "center", justifyContent: "center" }}>
                      <i className="pi pi-server" />
                    </div>
                    <div>
                      <div style={{ fontSize: 14, fontWeight: 600 }}>{device.name}</div>
                      <div style={{ fontSize: 11, color: "#9ca3af" }}>
                        {[device.model, locationName(device.locationId)].filter(Boolean).join(" · ")}
                      </div>
                    </div>
                  </div>
                  <div style={{ display: "flex", gap: 12, fontSize: 11 }}>
                    <span style={{ color: "#22c55e", fontWeight: 500 }}>{activePorts} ativas</span>
                    <span style={{ color: "#6b7280" }}>/ {ports.length} portas</span>
                  </div>
                </div>
                <div className="uni-panel-body">
                  {portsLoading && ports.length === 0 && (
                    <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>
                      Carregando portas…
                    </div>
                  )}
                  {!portsLoading && ports.length === 0 && (
                    <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>
                      Nenhuma porta cadastrada
                    </div>
                  )}
                  <div className="uni-switch-grid">
                    {ports.map((port) => {
                      const borderColor = port.linkUp === true ? "#22c55e" : port.linkUp === false ? "#ef4444" : "#d1d5db";
                      const bgColor = port.linkUp === true ? "#22c55e08" : port.linkUp === false ? "#fef2f2" : "#f9fafb";
                      return (
                        <div key={port.id} className="uni-switch-port" style={{ borderColor, background: bgColor }}>
                          <div style={{ fontSize: 10, fontWeight: 700, color: port.linkUp === null ? "#9ca3af" : "#111827" }}>{port.name}</div>
                          <div style={{ width: 6, height: 6, borderRadius: "50%", background: borderColor, margin: "2px 0" }} />
                          <div style={{ fontSize: 7, color: "#6b7280" }}>
                            {port.linkUp === true ? "Cima" : port.linkUp === false ? "Baixo" : "Não testada"}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </div>
            );
          })}
          {!loading && switches.length === 0 && (
            <div style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13, background: "white", borderRadius: 8, border: "1px solid #e5e7eb" }}>
              {error ? "—" : "Nenhum switch no inventário"}
            </div>
          )}
          {loading && switches.length === 0 && (
            <LoadingBlock style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }} />
          )}
        </div>
      )}

      {totalCount > items.length && tab !== "switch" && (
        <div style={{ fontSize: 11, color: "#94a3b8", textAlign: "center" }}>
          Exibindo até {INVENTORY_PAGE_SIZE} dispositivos por página (o endpoint não expõe navegação além disso — use os filtros por tipo).
        </div>
      )}
    </div>
  );
}
