import { useCallback, useEffect, useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { useCan } from "@/app/router/access";
import { listDevices, type DeviceItem } from "@/services/api/inventory";
import {
  closeSession,
  listDeviceSessions,
  openRdpSession,
  openSshSession,
  type RemoteSessionItem,
} from "@/services/api/remoteSessions";
import { formatDateTime } from "@/utils/format";

/**
 * F17-T012 — RemotePage conectada ao backend (substitui
 * `remoteSessions`/`remoteDevices` de `data/dashboardData`).
 *
 * O protótipo tinha tabelas GLOBAIS de "Sessões Ativas" e "Histórico" —
 * não existem no backend: as sessões são consultadas **por device**
 * (`GET /inventory/devices/{id}/remote/sessions`). A página virou um
 * seletor de dispositivo (reusa `listDevices` do T007) + o histórico daquele
 * device, com abrir SSH/RDP (`POST`, Operate, 201) e encerrar (`DELETE`,
 * Operate). Falha de conexão responde 409 com motivo pt-BR do backend
 * (banner). "Nova Conexão" global, colunas IP/Usuário-nome/Dados/online
 * não têm contrato (saíram); usuário viaja como GUID.
 *
 * Enums reais em PascalCase: protocol `Ssh`/`Rdp` (exibido SSH/RDP),
 * status `Active`/`Closed`/`Failed`. RDP é probe TCP sem autenticação
 * (F11-T006+ cuida do NLA); SSH usa as credenciais do device.
 */

const PAGE_LIMIT = 50;

const statusConfig: Record<string, { label: string; bg: string; color: string }> = {
  Active: { label: "Ativa", bg: "#f0fdf4", color: "#15803d" },
  Closed: { label: "Fechada", bg: "#f3f4f6", color: "#6b7280" },
  Failed: { label: "Falhou", bg: "#fef2f2", color: "#dc2626" },
};

const typeIcon: Record<string, string> = {
  Server: "pi pi-server",
  Switch: "pi pi-clone",
  Router: "pi pi-compass",
  Firewall: "pi pi-shield",
  AccessPoint: "pi pi-wifi",
};

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

/** Duração legível (StartedAt → EndedAt; sessão aberta usa "agora"). */
function durationOf(session: RemoteSessionItem): string {
  const start = new Date(session.startedAt).getTime();
  const end = session.endedAt ? new Date(session.endedAt).getTime() : Date.now();
  if (Number.isNaN(start) || Number.isNaN(end) || end < start) return "—";
  const seconds = Math.floor((end - start) / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ${seconds % 60}s`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ${minutes % 60}m`;
  return `${Math.floor(hours / 24)}d ${hours % 24}h`;
}

export function RemotePage() {
  // Abrir/encerrar = Operate no backend; sem nível os botões saem da UI.
  const canOperate = useCan("operate");

  const [devices, setDevices] = useState<DeviceItem[]>([]);
  const [deviceTotal, setDeviceTotal] = useState(0);
  const [devicesLoading, setDevicesLoading] = useState(true);
  const [devicesError, setDevicesError] = useState<string | null>(null);

  const [selected, setSelected] = useState<DeviceItem | null>(null);
  const [sessions, setSessions] = useState<RemoteSessionItem[]>([]);
  const [sessionsLoading, setSessionsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const loadDevices = useCallback(async () => {
    setDevicesLoading(true);
    setDevicesError(null);
    try {
      const result = await listDevices();
      setDevices(result.items);
      setDeviceTotal(result.totalCount);
    } catch (caught) {
      setDevicesError(messageFor(caught));
    } finally {
      setDevicesLoading(false);
    }
  }, []);

  const loadSessions = useCallback(async (deviceId: string) => {
    setSessionsLoading(true);
    setError(null);
    try {
      const result = await listDeviceSessions(deviceId, PAGE_LIMIT);
      setSessions(result?.sessions ?? []);
    } catch (caught) {
      setSessions([]);
      setError(messageFor(caught));
    } finally {
      setSessionsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadDevices();
  }, [loadDevices]);

  function selectDevice(device: DeviceItem) {
    setSelected(device);
    setNotice(null);
    setError(null);
    setSessions([]);
    void loadSessions(device.id);
  }

  async function open(protocol: "ssh" | "rdp") {
    if (!selected) return;
    setBusy(protocol);
    setError(null);
    setNotice(null);
    try {
      const action = protocol === "ssh" ? openSshSession : openRdpSession;
      await action(selected.id);
      setNotice(`Sessão ${protocol.toUpperCase()} aberta em '${selected.name}'.`);
      await loadSessions(selected.id);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setBusy(null);
    }
  }

  async function close(sessionId: string) {
    if (!selected) return;
    setBusy(`close:${sessionId}`);
    setError(null);
    setNotice(null);
    try {
      await closeSession(selected.id, sessionId);
      setNotice("Sessão encerrada.");
      await loadSessions(selected.id);
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setBusy(null);
    }
  }

  const truncated = deviceTotal > devices.length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Acesso Remoto"
        subtitle={devicesLoading
          ? "Carregando…"
          : `${deviceTotal} ${deviceTotal === 1 ? "dispositivo" : "dispositivos"} · selecione um para ver e abrir sessões`}
      />

      {devicesError && (
        <ErrorBanner
          message={devicesError}
          onRetry={() => void loadDevices()}
        />
      )}

      {/* Dispositivos (seletor — reusa o inventário do T007) */}
      <div>
        <div style={{ display: "flex", alignItems: "baseline", gap: 8, marginBottom: 10 }}>
          <h2 style={{ fontSize: 14, fontWeight: 600 }}>Dispositivos</h2>
          {truncated && (
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              mostrando {devices.length} de {deviceTotal} — inventário completo em Dispositivos
            </span>
          )}
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 12 }}>
          {devices.map((device) => {
            const isSelected = selected?.id === device.id;
            return (
              <div
                key={device.id}
                onClick={() => selectDevice(device)}
                style={{
                  background: isSelected ? "#eff6ff" : "white",
                  borderRadius: 8,
                  border: `1px solid ${isSelected ? "#3b82f6" : "#e5e7eb"}`,
                  padding: 14,
                  cursor: "pointer",
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
                  <div style={{ width: 32, height: 32, borderRadius: 6, background: "#f3f4f6", color: "#3b82f6", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 13 }}>
                    <i className={typeIcon[device.type] ?? "pi pi-question"} />
                  </div>
                  <div>
                    <div style={{ fontSize: 12, fontWeight: 600 }}>{device.name}</div>
                    <div style={{ fontSize: 10, color: "#9ca3af" }}>{device.type}</div>
                  </div>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#6b7280" }}>
                  <span style={{ fontFamily: "monospace" }}>{device.ip ?? "sem IP"}</span>
                  <span>{device.model ?? ""}</span>
                </div>
              </div>
            );
          })}
          {!devicesLoading && devices.length === 0 && !devicesError && (
            <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
              Nenhum dispositivo no inventário
            </div>
          )}
          {devicesLoading && devices.length === 0 && (
            <LoadingBlock style={{ gridColumn: "1 / -1" }} />
          )}
        </div>
      </div>

      {/* Sessões do device selecionado */}
      {selected && (
        <div>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 8, marginBottom: 10 }}>
            <div>
              <h2 style={{ fontSize: 14, fontWeight: 600 }}>
                Sessões de {selected.name}
              </h2>
              <span style={{ fontSize: 11, color: "#94a3b8" }}>
                mais recentes primeiro · até {PAGE_LIMIT} registradas
              </span>
            </div>
            {canOperate && (
              <div style={{ display: "flex", gap: 8 }}>
                <Button
                  variant="ghost"
                  onClick={() => void open("ssh")}
                  disabled={!!busy || !selected.ip}
                  title={!selected.ip ? "Device sem IP — acesso remoto indisponível" : "Abrir sessão SSH (credenciais do device)"}
                  style={{ padding: "6px 14px", borderColor: "#e2e8f0", color: selected.ip ? "#3b82f6" : "#9ca3af", cursor: selected.ip && !busy ? "pointer" : "default", opacity: busy ? 0.6 : 1 }}
                >
                  <i className="pi pi-terminal" style={{ fontSize: 11 }} />
                  {busy === "ssh" ? "Abrindo…" : "Abrir SSH"}
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => void open("rdp")}
                  disabled={!!busy || !selected.ip}
                  title={!selected.ip ? "Device sem IP — acesso remoto indisponível" : "Probe RDP (TCP, sem autenticação)"}
                  style={{ padding: "6px 14px", borderColor: "#e2e8f0", color: selected.ip ? "#8b5cf6" : "#9ca3af", cursor: selected.ip && !busy ? "pointer" : "default", opacity: busy ? 0.6 : 1 }}
                >
                  <i className="pi pi-desktop" style={{ fontSize: 11 }} />
                  {busy === "rdp" ? "Abrindo…" : "Abrir RDP"}
                </Button>
              </div>
            )}
          </div>

          {notice && (
            <NoticeBanner message={notice} style={{ marginBottom: 10 }} />
          )}

          {error && (
            <ErrorBanner
              message={error}
              onRetry={() => void loadSessions(selected.id)}
              style={{ marginBottom: 10 }}
            />
          )}

          <DataTablePanel value={sessions} dataKey="id" loading={sessionsLoading} emptyMessage={error ? "—" : "Nenhuma sessão registrada neste dispositivo"}>
              <Column header="Protocolo" body={(session: RemoteSessionItem) => <StatusChip label={session.protocol === "Ssh" ? "SSH" : "RDP"} background={session.protocol === "Ssh" ? "#eff6ff" : "#f5f3ff"} color={session.protocol === "Ssh" ? "#3b82f6" : "#8b5cf6"} fontWeight={600} fontSize={10} />} style={{ width: 100 }} />
              <Column header="Usuário" body={(session: RemoteSessionItem) => <span style={{ fontFamily: "monospace", fontSize: 11 }} title={session.userId}>{session.userId.slice(0, 8)}</span>} />
              <Column header="Início" body={(session: RemoteSessionItem) => formatDateTime(new Date(session.startedAt))} style={{ fontSize: 12, color: "#6b7280", width: 155 }} />
              <Column header="Fim" body={(session: RemoteSessionItem) => session.endedAt ? formatDateTime(new Date(session.endedAt)) : "—"} style={{ fontSize: 12, color: "#6b7280", width: 155 }} />
              <Column header="Duração" body={(session: RemoteSessionItem) => durationOf(session)} style={{ fontSize: 12, fontWeight: 500, width: 90 }} />
              <Column header="Status" body={(session: RemoteSessionItem) => { const status = statusConfig[session.status] ?? { label: session.status, bg: "#f3f4f6", color: "#6b7280" }; return <><StatusChip label={status.label} background={status.bg} color={status.color} fontSize={10} title={session.failureReason ?? undefined} />{session.status === "Failed" && session.failureReason && <div style={{ fontSize: 10, color: "#dc2626", marginTop: 3 }} title={session.failureReason}>{session.failureReason}</div>}</>; }} />
              <Column header="Ações" body={(session: RemoteSessionItem) => canOperate && session.status === "Active" ? <button onClick={() => void close(session.id)} disabled={!!busy} style={{ fontSize: 10, padding: "3px 8px", borderRadius: 4, background: "#fef2f2", color: "#dc2626", fontWeight: 500, cursor: busy ? "default" : "pointer", opacity: busy ? 0.6 : 1 }}><i className="pi pi-times" style={{ fontSize: 9, marginRight: 3 }} />{busy === `close:${session.id}` ? "Encerrando…" : "Encerrar"}</button> : null} style={{ width: 100 }} />
          </DataTablePanel>
        </div>
      )}
    </div>
  );
}
