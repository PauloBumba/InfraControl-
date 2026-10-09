import { useState } from "react";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { useCan } from "@/app/router/access";
import { listDevices, type DeviceItem } from "@/services/api/inventory";
import {
  closeSession,
  listDeviceSessions,
  openRdpSession,
  openSshSession,
  type RemoteSessionItem,
} from "@/services/api/remoteSessions";
import {
  useListQuery,
  usePagedQuery,
  useUpdateMutation,
  mutationErrorMessage,
} from "@/hooks/queries";
import { formatDateTime } from "@/utils/format";

/**
 * F17-T012 + ADR-030 Fase 5 — RemotePage com React Query (devices em
 * `["devices"]` compartilhado com a DevicesPage; sessões em
 * `["remoteSessions", deviceId]` com `enabled` no drill-down) e visões
 * tabela/grid/kanban nas sessões (preferência persistida; kanban agrupa
 * por status Active/Closed/Failed). Abrir SSH/RDP e encerrar são mutations
 * com invalidação do prefixo `["remoteSessions"]` (Operate; 409 pt-BR do
 * backend vira banner).
 *
 * As sessões são consultadas **por device** (`GET /inventory/devices/{id}/remote/sessions`);
 * usuário viaja como GUID. Enums PascalCase: protocol `Ssh`/`Rdp`,
 * status `Active`/`Closed`/`Failed`. RDP é probe TCP sem autenticação
 * (F11-T006+ cuida do NLA); SSH usa as credenciais do device.
 */

const PAGE_LIMIT = 50;

const statusConfig: Record<string, { label: string; bg: string; color: string }> = {
  Active: { label: "Ativa", bg: "#f0fdf4", color: "#15803d" },
  Closed: { label: "Fechada", bg: "#f3f4f6", color: "#6b7280" },
  Failed: { label: "Falhou", bg: "#fef2f2", color: "#dc2626" },
};

const statusColumns = Object.entries(statusConfig).map(([key, meta]) => ({
  key,
  label: meta.label,
}));

const typeIcon: Record<string, string> = {
  Server: "pi pi-server",
  Switch: "pi pi-clone",
  Router: "pi pi-compass",
  Firewall: "pi pi-shield",
  AccessPoint: "pi pi-wifi",
};

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
  const [view, setView] = useEntityViewMode("remoteSessions", "table");

  const devices = usePagedQuery<DeviceItem>(["devices"], () => listDevices());
  const [selected, setSelected] = useState<DeviceItem | null>(null);

  const sessions = useListQuery<RemoteSessionItem>(
    ["remoteSessions", selected?.id ?? ""],
    async () => {
      const result = await listDeviceSessions(selected?.id as string, PAGE_LIMIT);
      return result?.sessions ?? [];
    },
    { enabled: selected !== null },
  );

  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const openMutation = useUpdateMutation({
    invalidate: ["remoteSessions"],
    mutationFn: (input: { deviceId: string; protocol: "ssh" | "rdp" }) =>
      input.protocol === "ssh"
        ? openSshSession(input.deviceId)
        : openRdpSession(input.deviceId),
    onSuccess: (_data, vars) => {
      setActionError(null);
      setNotice(`Sessão ${vars.protocol.toUpperCase()} aberta em '${selected?.name ?? ""}'.`);
    },
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });
  const closeMutation = useUpdateMutation({
    invalidate: ["remoteSessions"],
    mutationFn: (input: { deviceId: string; sessionId: string }) =>
      closeSession(input.deviceId, input.sessionId),
    onSuccess: () => {
      setActionError(null);
      setNotice("Sessão encerrada.");
    },
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });

  const busy =
    openMutation.isPending
      ? (openMutation.variables?.protocol ?? null)
      : closeMutation.isPending
        ? `close:${closeMutation.variables?.sessionId ?? ""}`
        : null;

  function selectDevice(device: DeviceItem) {
    setSelected(device);
    setNotice(null);
    setActionError(null);
  }

  function open(protocol: "ssh" | "rdp") {
    if (!selected) return;
    openMutation.mutate({ deviceId: selected.id, protocol });
  }

  function close(sessionId: string) {
    if (!selected) return;
    closeMutation.mutate({ deviceId: selected.id, sessionId });
  }

  const deviceTotal = devices.totalCount;
  const truncated = deviceTotal > devices.items.length;
  const devicesError = mutationErrorMessage(devices.error as Error | null);
  const sessionsError = mutationErrorMessage(sessions.error as Error | null);
  const listError = actionError ?? sessionsError;

  if (devices.isPending) {
    return <LoadingBlock />;
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Acesso Remoto"
        subtitle={`${deviceTotal} ${deviceTotal === 1 ? "dispositivo" : "dispositivos"} · selecione um para ver e abrir sessões`}
        actions={selected ? <ViewSwitcher mode={view} onChange={setView} /> : undefined}
      />

      {devicesError && (
        <ErrorBanner
          message={devicesError}
          onRetry={() => void devices.refetch()}
        />
      )}

      {/* Dispositivos (seletor — reusa o inventário do T007) */}
      <div>
        <div style={{ display: "flex", alignItems: "baseline", gap: 8, marginBottom: 10 }}>
          <h2 style={{ fontSize: 14, fontWeight: 600 }}>Dispositivos</h2>
          {truncated && (
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              mostrando {devices.items.length} de {deviceTotal} — inventário completo em Dispositivos
            </span>
          )}
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 12 }}>
          {devices.items.map((device) => {
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
          {!devices.isPending && devices.items.length === 0 && !devicesError && (
            <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
              Nenhum dispositivo no inventário
            </div>
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
                  onClick={() => open("ssh")}
                  disabled={!!busy || !selected.ip}
                  title={!selected.ip ? "Device sem IP — acesso remoto indisponível" : "Abrir sessão SSH (credenciais do device)"}
                  style={{ padding: "6px 14px", borderColor: "#e2e8f0", color: selected.ip ? "#3b82f6" : "#9ca3af", cursor: selected.ip && !busy ? "pointer" : "default", opacity: busy ? 0.6 : 1 }}
                >
                  <i className="pi pi-terminal" style={{ fontSize: 11 }} />
                  {busy === "ssh" ? "Abrindo…" : "Abrir SSH"}
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => open("rdp")}
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

          {listError && (
            <ErrorBanner
              message={listError}
              onRetry={() => {
                setActionError(null);
                void sessions.refetch();
              }}
              style={{ marginBottom: 10 }}
            />
          )}

          <EntityView<RemoteSessionItem>
            mode={view}
            items={sessions.items}
            getKey={(session) => session.id}
            emptyMessage={listError ? "—" : "Nenhuma sessão registrada neste dispositivo"}
            kanban={{ groupOf: (session) => session.status, columns: statusColumns }}
            table={
              <DataTablePanel value={sessions.items} dataKey="id" loading={sessions.isPending} emptyMessage={listError ? "—" : "Nenhuma sessão registrada neste dispositivo"}>
                  <Column header="Protocolo" body={(session: RemoteSessionItem) => <StatusChip label={session.protocol === "Ssh" ? "SSH" : "RDP"} background={session.protocol === "Ssh" ? "#eff6ff" : "#f5f3ff"} color={session.protocol === "Ssh" ? "#3b82f6" : "#8b5cf6"} fontWeight={600} fontSize={10} />} style={{ width: 100 }} />
                  <Column header="Usuário" body={(session: RemoteSessionItem) => <span style={{ fontFamily: "monospace", fontSize: 11 }} title={session.userId}>{session.userId.slice(0, 8)}</span>} />
                  <Column header="Início" body={(session: RemoteSessionItem) => formatDateTime(new Date(session.startedAt))} style={{ fontSize: 12, color: "#6b7280", width: 155 }} />
                  <Column header="Fim" body={(session: RemoteSessionItem) => session.endedAt ? formatDateTime(new Date(session.endedAt)) : "—"} style={{ fontSize: 12, color: "#6b7280", width: 155 }} />
                  <Column header="Duração" body={(session: RemoteSessionItem) => durationOf(session)} style={{ fontSize: 12, fontWeight: 500, width: 90 }} />
                  <Column header="Status" body={(session: RemoteSessionItem) => { const status = statusConfig[session.status] ?? { label: session.status, bg: "#f3f4f6", color: "#6b7280" }; return <><StatusChip label={status.label} background={status.bg} color={status.color} fontSize={10} title={session.failureReason ?? undefined} />{session.status === "Failed" && session.failureReason && <div style={{ fontSize: 10, color: "#dc2626", marginTop: 3 }} title={session.failureReason}>{session.failureReason}</div>}</>; }} />
                  <Column header="Ações" body={(session: RemoteSessionItem) => canOperate && session.status === "Active" ? <button onClick={() => close(session.id)} disabled={!!busy} style={{ fontSize: 10, padding: "3px 8px", borderRadius: 4, background: "#fef2f2", color: "#dc2626", fontWeight: 500, cursor: busy ? "default" : "pointer", opacity: busy ? 0.6 : 1 }}><i className="pi pi-times" style={{ fontSize: 9, marginRight: 3 }} />{busy === `close:${session.id}` ? "Encerrando…" : "Encerrar"}</button> : null} style={{ width: 100 }} />
              </DataTablePanel>
            }
            renderCard={(session) => {
              const status = statusConfig[session.status] ?? { label: session.status, bg: "#f3f4f6", color: "#6b7280" };
              return (
                <div className="uni-panel" style={{ padding: 14 }}>
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 6 }}>
                    <StatusChip
                      label={session.protocol === "Ssh" ? "SSH" : "RDP"}
                      background={session.protocol === "Ssh" ? "#eff6ff" : "#f5f3ff"}
                      color={session.protocol === "Ssh" ? "#3b82f6" : "#8b5cf6"}
                      fontWeight={600}
                      fontSize={10}
                    />
                    <StatusChip label={status.label} background={status.bg} color={status.color} fontSize={10} />
                  </div>
                  <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 4 }}>
                    <span style={{ fontFamily: "monospace" }} title={session.userId}>
                      {session.userId.slice(0, 8)}
                    </span>
                    {" · "}
                    {durationOf(session)}
                  </div>
                  <div style={{ fontSize: 11, color: "#9ca3af" }}>
                    {formatDateTime(new Date(session.startedAt))}
                    {session.endedAt && ` → ${formatDateTime(new Date(session.endedAt))}`}
                  </div>
                  {session.status === "Failed" && session.failureReason && (
                    <div style={{ fontSize: 10, color: "#dc2626", marginTop: 4 }} title={session.failureReason}>
                      {session.failureReason}
                    </div>
                  )}
                  {canOperate && session.status === "Active" && (
                    <div style={{ marginTop: 8 }}>
                      <button
                        onClick={() => close(session.id)}
                        disabled={!!busy}
                        style={{
                          fontSize: 10, padding: "4px 10px", borderRadius: 4,
                          background: "#fef2f2", color: "#dc2626", fontWeight: 500,
                          cursor: busy ? "default" : "pointer", opacity: busy ? 0.6 : 1,
                        }}
                      >
                        <i className="pi pi-times" style={{ fontSize: 9, marginRight: 3 }} />
                        {busy === `close:${session.id}` ? "Encerrando…" : "Encerrar"}
                      </button>
                    </div>
                  )}
                </div>
              );
            }}
          />
        </div>
      )}
    </div>
  );
}
