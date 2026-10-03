import { useEffect, useState, useCallback } from "react";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { useNavigate } from "react-router-dom";
import { ApiError } from "@/services/api/client";
import { listAlerts, type AlertItem } from "@/services/api/alerts";
import { listDevices, type DeviceItem } from "@/services/api/inventory";
import { listTasks, type TaskItem } from "@/services/api/tasks";
import { listIncidents, type IncidentItem } from "@/services/api/incidents";
import { listClients, type ClientItem } from "@/services/api/clients";
import {
  AUDIT_OUTCOME_SUCCEEDED,
  listAuditEntries,
  type AuditEntry,
} from "@/services/api/audit";
import { formatDateTime } from "@/utils/format";

const taskStateLabels: Record<string, string> = {
  Open: "Aberta",
  InProgress: "Em progresso",
  Waiting: "Aguardando",
  Blocked: "Bloqueada",
  Resolved: "Resolvida",
  Cancelled: "Cancelada",
  Closed: "Fechada",
};
const TERMINAL_TASK_STATES = ["Resolved", "Cancelled", "Closed"];

const taskStateColors: Record<string, { bg: string; color: string }> = {
  Open: { bg: "#eff6ff", color: "#2563eb" },
  InProgress: { bg: "#dcfce7", color: "#16a34a" },
  Waiting: { bg: "#f1f5f9", color: "#64748b" },
  Blocked: { bg: "#fef2f2", color: "#dc2626" },
  Resolved: { bg: "#f0fdf4", color: "#15803d" },
  Cancelled: { bg: "#f1f5f9", color: "#94a3b8" },
  Closed: { bg: "#f1f5f9", color: "#64748b" },
};

const incidentSeverityColors: Record<string, { bg: string; color: string }> = {
  Critical: { bg: "#fef2f2", color: "#dc2626" },
  High: { bg: "#fff7ed", color: "#ea580c" },
  Medium: { bg: "#fefce8", color: "#ca8a04" },
  Low: { bg: "#f0fdf4", color: "#16a34a" },
};

function MiniStat({
  icon,
  label,
  value,
  color,
  sub,
  onClick,
}: {
  icon: string;
  label: string;
  value: string;
  color: string;
  sub?: string;
  onClick?: () => void;
}) {
  return (
    <div
      onClick={onClick}
      style={{
        display: "flex",
        alignItems: "center",
        gap: 12,
        padding: "14px 16px",
        background: "white",
        borderRadius: 8,
        border: "1px solid #e5e7eb",
        cursor: onClick ? "pointer" : "default",
        transition: "transform 0.15s ease, box-shadow 0.15s ease",
      }}
      onMouseEnter={(e) => {
        if (onClick) {
          e.currentTarget.style.transform = "translateY(-2px)";
          e.currentTarget.style.boxShadow = "0 4px 12px rgba(0,0,0,0.06)";
        }
      }}
      onMouseLeave={(e) => {
        if (onClick) {
          e.currentTarget.style.transform = "none";
          e.currentTarget.style.boxShadow = "none";
        }
      }}
    >
      <div
        style={{
          width: 40,
          height: 40,
          borderRadius: 8,
          background: `${color}12`,
          color,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: 16,
          flexShrink: 0,
        }}
      >
        <i className={`pi ${icon}`} />
      </div>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 10, color: "#9ca3af", textTransform: "uppercase", letterSpacing: 0.5 }}>{label}</div>
        <div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1.2 }}>{value}</div>
        {sub && <div style={{ fontSize: 10, color: "#9ca3af" }}>{sub}</div>}
      </div>
    </div>
  );
}

function ViewAll({ to, label }: { to: string; label: string }) {
  const navigate = useNavigate();
  return (
    <button
      onClick={() => navigate(to)}
      style={{ fontSize: 12, color: "#3b82f6", fontWeight: 500, background: "none", border: "none", cursor: "pointer", padding: 0 }}
    >
      {label}
    </button>
  );
}

export function DashboardPage() {
  const navigate = useNavigate();
  const [devices, setDevices] = useState<DeviceItem[]>([]);
  const [deviceTotal, setDeviceTotal] = useState(0);
  const [openAlerts, setOpenAlerts] = useState<AlertItem[]>([]);
  const [openAlertTotal, setOpenAlertTotal] = useState(0);
  const [incidents, setIncidents] = useState<IncidentItem[]>([]);
  const [incidentTotal, setIncidentTotal] = useState(0);
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [clientTotal, setClientTotal] = useState(0);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [events, setEvents] = useState<AuditEntry[] | null>(null);
  const [eventsForbidden, setEventsForbidden] = useState(false);
  const [eventsError, setEventsError] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchDashboardData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [devicePage, alertPage, incidentPage, clientPage, taskPage] = await Promise.all([
        listDevices({ pageSize: 6 }),
        listAlerts({ status: "open", pageSize: 100 }),
        listIncidents({ pageSize: 5 }),
        listClients("", 1, 5),
        listTasks({ pageSize: 100 }),
      ]);
      setDevices(devicePage.items);
      setDeviceTotal(devicePage.totalCount);
      setOpenAlerts(alertPage.items);
      setOpenAlertTotal(alertPage.totalCount);
      setIncidents(incidentPage.items);
      setIncidentTotal(incidentPage.totalCount);
      setClients(clientPage.items);
      setClientTotal(clientPage.totalCount);
      setTasks(taskPage.items);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Não foi possível falar com o servidor. Tente novamente.",
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchDashboardData();
  }, [fetchDashboardData]);

  useEffect(() => {
    const handleRefresh = () => {
      fetchDashboardData();
    };
    window.addEventListener("infracontrol:refresh", handleRefresh);
    return () => window.removeEventListener("infracontrol:refresh", handleRefresh);
  }, [fetchDashboardData]);

  useEffect(() => {
    let cancelled = false;
    listAuditEntries({ pageSize: 6 })
      .then((page) => {
        if (!cancelled) setEvents(page.items);
      })
      .catch((caught: unknown) => {
        if (cancelled) return;
        if (caught instanceof ApiError && caught.isForbidden) {
          setEventsForbidden(true);
        } else {
          setEventsError(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const openTasks = tasks.filter((task) => !TERMINAL_TASK_STATES.includes(task.state));
  const inProgressTasks = openTasks.filter((task) => task.state === "InProgress");
  const criticalOpenAlerts = openAlerts.filter((alert) => alert.severity === "Critical");
  const openIncidents = incidents.filter((inc) => inc.status !== "Resolved");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {error && <ErrorBanner message={error} />}

      {/* ============ QUICK STATS (clicáveis — estilo UniFi) ============ */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 12 }}>
        <MiniStat
          icon="pi-server"
          label="Dispositivos"
          value={loading ? "…" : String(deviceTotal)}
          color="#3b82f6"
          sub="no inventário"
          onClick={() => navigate("/devices")}
        />
        <MiniStat
          icon="pi-bell"
          label="Alertas"
          value={loading ? "…" : String(openAlertTotal)}
          color="#f59e0b"
          sub={criticalOpenAlerts.length > 0 ? `${criticalOpenAlerts.length} críticos abertos` : "abertos"}
          onClick={() => navigate("/alerts")}
        />
        <MiniStat
          icon="pi-exclamation-triangle"
          label="Incidentes"
          value={loading ? "…" : String(incidentTotal)}
          color="#ef4444"
          sub={`${openIncidents.length} em aberto`}
          onClick={() => navigate("/incidents")}
        />
        <MiniStat
          icon="pi-users"
          label="Clientes"
          value={loading ? "…" : String(clientTotal)}
          color="#8b5cf6"
          sub="descobertos"
          onClick={() => navigate("/clients")}
        />
        <MiniStat
          icon="pi-check-square"
          label="Tarefas"
          value={loading ? "…" : String(openTasks.length)}
          color="#06b6d4"
          sub={`${inProgressTasks.length} em progresso`}
          onClick={() => navigate("/tasks")}
        />
      </div>

      {/* ============ INCIDENTES + CLIENTES ============ */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Incidentes Recentes</h2>
            <ViewAll to="/incidents" label="Ver todos" />
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {incidents.slice(0, 5).map((inc) => {
              const sevColor = incidentSeverityColors[inc.severity] ?? { bg: "#f1f5f9", color: "#64748b" };
              return (
                <div
                  key={inc.id}
                  style={{
                    padding: "10px 12px",
                    borderRadius: 6,
                    border: "1px solid #f3f4f6",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    gap: 10,
                  }}
                >
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <div style={{ fontSize: 12, fontWeight: 600, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                      {inc.title}
                    </div>
                    <div style={{ fontSize: 10, color: "#9ca3af" }}>
                      {formatDateTime(new Date(inc.openedAt))} · {inc.status}
                    </div>
                  </div>
                  <span
                    style={{
                      fontSize: 10,
                      padding: "2px 6px",
                      borderRadius: 4,
                      background: sevColor.bg,
                      color: sevColor.color,
                      fontWeight: 600,
                      flexShrink: 0,
                    }}
                  >
                    {inc.severity}
                  </span>
                </div>
              );
            })}
            {!loading && incidents.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Nenhum incidente cadastrado</div>
            )}
            {loading && incidents.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Carregando…</div>
            )}
          </div>
        </div>

        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Clientes Descobertos</h2>
            <ViewAll to="/clients" label="Ver todos" />
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {clients.slice(0, 5).map((cli, idx) => (
              <div
                key={cli.mac ?? cli.ip ?? idx}
                style={{
                  padding: "8px 10px",
                  borderRadius: 6,
                  background: "#f9fafb",
                  border: "1px solid #f3f4f6",
                  display: "flex",
                  alignItems: "center",
                  gap: 10,
                }}
              >
                <i className="pi pi-desktop" style={{ fontSize: 12, color: "#8b5cf6", flexShrink: 0 }} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 12, fontWeight: 500, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                    {cli.hostname || cli.ip || "Cliente sem nome"}
                  </div>
                  <div style={{ fontSize: 10, color: "#9ca3af" }}>
                    {cli.ip && <span>IP: {cli.ip} </span>}
                    {cli.mac && <span>· MAC: {cli.mac} </span>}
                    {cli.vlan && <span>· VLAN: {cli.vlan}</span>}
                  </div>
                </div>
                <span style={{ fontSize: 9, padding: "2px 5px", borderRadius: 4, background: "#f1f5f9", color: "#475569", flexShrink: 0 }}>
                  {cli.kind}
                </span>
              </div>
            ))}
            {!loading && clients.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Nenhum cliente descoberto</div>
            )}
            {loading && clients.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Carregando…</div>
            )}
          </div>
        </div>
      </div>

      {/* ============ DEVICES + ALERTS + TASKS ============ */}
      <div style={{ display: "grid", gridTemplateColumns: "1.2fr 1fr 1fr", gap: 12 }}>
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Dispositivos</h2>
            <ViewAll to="/devices" label="Ver todos" />
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {devices.slice(0, 6).map((device) => (
              <div key={device.id} style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 10px", borderRadius: 6, background: "#f9fafb", border: "1px solid #f3f4f6" }}>
                <i className="pi pi-desktop" style={{ fontSize: 11, color: "#3b82f6", flexShrink: 0 }} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 12, fontWeight: 500, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{device.name}</div>
                  <div style={{ fontSize: 10, color: "#9ca3af" }}>{device.model ?? device.ip ?? device.type}</div>
                </div>
              </div>
            ))}
            {!loading && devices.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Nenhum dispositivo</div>
            )}
            {loading && devices.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Carregando…</div>
            )}
          </div>
        </div>

        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Alertas Abertos</h2>
            <ViewAll to="/alerts" label="Ver todos" />
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {openAlerts.slice(0, 5).map((alert) => {
              const critical = alert.severity === "Critical";
              return (
                <div key={alert.id} style={{ padding: "10px 12px", borderRadius: 6, border: "1px solid #f3f4f6", borderLeft: `3px solid ${critical ? "#ef4444" : "#f59e0b"}` }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 4 }}>
                    <i className={`pi ${critical ? "pi-exclamation-triangle" : "pi-exclamation-circle"}`} style={{ fontSize: 11, color: critical ? "#ef4444" : "#f59e0b" }} />
                    <span style={{ fontSize: 12, fontWeight: 600 }}>{alert.deviceName ?? "Sem device"} · {alert.check ?? alert.source}</span>
                  </div>
                  <div style={{ fontSize: 11, color: "#6b7280" }}>{alert.message}</div>
                </div>
              );
            })}
            {!loading && openAlerts.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Nenhum alerta aberto 🎉</div>
            )}
            {loading && openAlerts.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Carregando…</div>
            )}
          </div>
        </div>

        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Tarefas</h2>
            <ViewAll to="/tasks" label="Ver todas" />
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {openTasks.slice(0, 6).map((task) => {
              const color = taskStateColors[task.state] ?? { bg: "#f1f5f9", color: "#64748b" };
              return (
                <div key={task.id} style={{ padding: "10px 12px", borderRadius: 6, border: "1px solid #f3f4f6" }}>
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 4 }}>
                    <span style={{ fontSize: 12, fontWeight: 500, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{task.title}</span>
                    <span style={{ fontSize: 9, padding: "2px 6px", borderRadius: 4, background: color.bg, color: color.color, fontWeight: 500, flexShrink: 0 }}>
                      {taskStateLabels[task.state] ?? task.state}
                    </span>
                  </div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 10, color: "#9ca3af" }}>
                    <span><i className="pi pi-tag" style={{ marginRight: 3 }} />{task.kind}</span>
                    <span><i className="pi pi-calendar" style={{ marginRight: 3 }} />{formatDateTime(new Date(task.openedAt))}</span>
                  </div>
                </div>
              );
            })}
            {!loading && openTasks.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Nenhuma tarefa aberta</div>
            )}
            {loading && openTasks.length === 0 && (
              <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Carregando…</div>
            )}
          </div>
        </div>
      </div>

      {/* ============ EVENTOS RECENTES (auditoria) ============ */}
      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Eventos Recentes</h2>
          {!eventsForbidden && !eventsError && <ViewAll to="/audit" label="Ver todos" />}
        </div>
        <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          {eventsForbidden && (
            <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>
              Acesso à auditoria restrito ao nível de administração.
            </div>
          )}
          {eventsError && (
            <div style={{ fontSize: 12, color: "#b91c1c", textAlign: "center", padding: 12 }}>
              Não foi possível carregar os eventos.
            </div>
          )}
          {events === null && !eventsForbidden && !eventsError && (
            <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Carregando…</div>
          )}
          {events?.map((event, i) => (
            <div key={event.id} style={{ display: "flex", alignItems: "flex-start", gap: 8, padding: "8px 0", borderBottom: i < events.length - 1 ? "1px solid #f3f4f6" : "none" }}>
              <span style={{ width: 7, height: 7, borderRadius: "50%", background: event.outcome === AUDIT_OUTCOME_SUCCEEDED ? "#22c55e" : "#ef4444", marginTop: 4, flexShrink: 0 }} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 12 }}>{event.action}</div>
                <div style={{ fontSize: 10, color: "#9ca3af" }}>{event.resource}{event.error ? ` · ${event.error}` : ""}</div>
              </div>
              <span style={{ fontSize: 10, color: "#9ca3af", whiteSpace: "nowrap" }}>{formatDateTime(new Date(event.occurredAt))}</span>
            </div>
          ))}
          {events && events.length === 0 && (
            <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>Nenhum evento registrado</div>
          )}
        </div>
      </div>
    </div>
  );
}
