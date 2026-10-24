import { useEffect, useState, useCallback } from "react";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { StatusChip } from "@/components/ui/StatusChip";
import { Button } from "@/components/ui/Button";
import { MetricChart } from "@/components/ui/MetricChart";
import { HealthBar, type HealthItem } from "@/components/dashboard/HealthBar";
import { DeviceHealthCard } from "@/components/dashboard/DeviceHealthCard";
import { useNavigate } from "react-router-dom";
import { ApiError } from "@/services/api/client";
import { listAlerts, type AlertItem } from "@/services/api/alerts";
import { listDevices, type DeviceItem } from "@/services/api/inventory";
import { listTasks, type TaskItem } from "@/services/api/tasks";
import { listIncidents, type IncidentItem } from "@/services/api/incidents";
import { listClients, type ClientItem } from "@/services/api/clients";
import { getZabbixSummary, type ZabbixSummary } from "@/services/api/zabbix";
import {
  getMyInvitations,
  acceptInvitation,
  rejectInvitation,
  type InvitationItem,
} from "@/services/api/invitations";
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

/** Chips dark-safe: fundo/token + texto/token (sem hex fixo — F40-T001). */
const taskStateChips: Record<string, { bg: string; color: string }> = {
  Open: { bg: "var(--uni-chip-blue-bg)", color: "var(--uni-accent)" },
  InProgress: { bg: "var(--uni-chip-green-bg)", color: "var(--uni-green)" },
  Waiting: { bg: "var(--uni-chip-blue-bg)", color: "var(--uni-text-secondary)" },
  Blocked: { bg: "var(--uni-chip-red-bg)", color: "var(--uni-red)" },
  Resolved: { bg: "var(--uni-chip-green-bg)", color: "var(--uni-green)" },
  Cancelled: { bg: "var(--uni-chip-blue-bg)", color: "var(--uni-text-muted)" },
  Closed: { bg: "var(--uni-chip-blue-bg)", color: "var(--uni-text-secondary)" },
};

const incidentSeverityChips: Record<string, { bg: string; color: string }> = {
  Critical: { bg: "var(--uni-chip-red-bg)", color: "var(--uni-red)" },
  High: { bg: "var(--uni-chip-yellow-bg)", color: "#ea580c" },
  Medium: { bg: "var(--uni-chip-yellow-bg)", color: "var(--uni-yellow)" },
  Low: { bg: "var(--uni-chip-green-bg)", color: "var(--uni-green)" },
};

function ViewAll({ to, label }: { to: string; label: string }) {
  const navigate = useNavigate();
  return (
    <button type="button" className="uni-dash-view-all" onClick={() => navigate(to)}>
      {label}
    </button>
  );
}

/**
 * F40-T001 — deriva os dots de saúde (estilo UniFi) dos dados reais já
 * buscados pelo dashboard. `unknown` = sem dado (zero ≠ erro).
 */
function deriveHealth(
  loading: boolean,
  openAlerts: AlertItem[],
  openIncidentTotal: number,
  openTasksCount: number,
  deviceTotal: number,
): HealthItem[] {
  if (loading) {
    return [
      { id: "alerts", label: "Alertas", status: "unknown" },
      { id: "incidents", label: "Incidentes", status: "unknown" },
      { id: "tasks", label: "Tarefas", status: "unknown" },
      { id: "devices", label: "Inventário", status: "unknown" },
    ];
  }
  const criticalAlerts = openAlerts.filter((a) => a.severity === "Critical").length;
  return [
    {
      id: "alerts",
      label: "Alertas",
      status: criticalAlerts > 0 ? "critical" : openAlerts.length > 0 ? "warning" : "ok",
      detail: openAlerts.length > 0 ? `${openAlerts.length} abertos` : "nenhum aberto",
    },
    {
      id: "incidents",
      label: "Incidentes",
      status: openIncidentTotal > 0 ? "warning" : "ok",
      detail: openIncidentTotal > 0 ? `${openIncidentTotal} em aberto` : "nenhum em aberto",
    },
    {
      id: "tasks",
      label: "Tarefas",
      status: "ok",
      detail: `${openTasksCount} abertas`,
    },
    {
      id: "devices",
      label: "Inventário",
      status: deviceTotal > 0 ? "ok" : "unknown",
      detail: deviceTotal > 0 ? `${deviceTotal} devices` : "sem dispositivos",
    },
  ];
}

export function DashboardPage() {
  const navigate = useNavigate();
  const [devices, setDevices] = useState<DeviceItem[]>([]);
  const [deviceTotal, setDeviceTotal] = useState(0);
  const [openAlerts, setOpenAlerts] = useState<AlertItem[]>([]);
  const [openAlertTotal, setOpenAlertTotal] = useState(0);
  const [incidents, setIncidents] = useState<IncidentItem[]>([]);
  const [incidentTotal, setIncidentTotal] = useState(0);
  const [openIncidentTotal, setOpenIncidentTotal] = useState(0);
  const [clients, setClients] = useState<ClientItem[]>([]);
  const [clientTotal, setClientTotal] = useState(0);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [events, setEvents] = useState<AuditEntry[] | null>(null);
  const [eventsForbidden, setEventsForbidden] = useState(false);
  const [eventsError, setEventsError] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pendingInvitations, setPendingInvitations] = useState<InvitationItem[]>([]);
  const [invitationsLoaded, setInvitationsLoaded] = useState(false);
  const [invitationsError, setInvitationsError] = useState<string | null>(null);
  const [invitationsNotice, setInvitationsNotice] = useState<string | null>(null);
  const [invitationBusyId, setInvitationBusyId] = useState<string | null>(null);
  const [zabbix, setZabbix] = useState<ZabbixSummary | null>(null);

  /**
   * F40-T003 — summary do Zabbix carregado INDEPENDENTEMENTE do restante:
   * falha aqui (Zabbix fora do ar / não configurado) não derruba os demais
   * painéis — mesmo tratamento dos convites e da auditoria.
   */
  const loadZabbix = useCallback(async () => {
    try {
      setZabbix(await getZabbixSummary());
    } catch {
      setZabbix(null);
    }
  }, []);

  useEffect(() => {
    void loadZabbix();
  }, [loadZabbix]);

  const fetchDashboardData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [devicePage, alertPage, incidentPage, openIncidentPage, clientPage, taskPage] = await Promise.all([
        listDevices({ pageSize: 6 }),
        listAlerts({ status: "open", pageSize: 100 }),
        listIncidents({ pageSize: 5 }),
        // M7: "active" = != Resolved no servidor — o totalCount é o número
        // real de abertos (filtrar no front sobre a 1ª página mentia o KPI).
        listIncidents({ status: "active", pageSize: 1 }),
        listClients("", 1, 5),
        listTasks({ pageSize: 100 }),
      ]);
      setDevices(devicePage.items);
      setDeviceTotal(devicePage.totalCount);
      setOpenAlerts(alertPage.items);
      setOpenAlertTotal(alertPage.totalCount);
      setIncidents(incidentPage.items);
      setIncidentTotal(incidentPage.totalCount);
      setOpenIncidentTotal(openIncidentPage.totalCount);
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

  /**
   * F29-T004 — era um efeito auto-contido sem nenhuma forma de tentar de
   * novo: o usuário via o texto vermelho e só recarregando a página inteira.
   * Tirado para dentro de um `useCallback` para que o `ErrorBanner` possa
   * oferercer o retry.
   */
  const loadEvents = useCallback(async () => {
    setEventsError(false);
    setEventsForbidden(false);
    try {
      const page = await listAuditEntries({ pageSize: 6 });
      setEvents(page.items);
    } catch (caught) {
      if (caught instanceof ApiError && caught.isForbidden) {
        setEventsForbidden(true);
      } else {
        setEventsError(true);
      }
    }
  }, []);

  useEffect(() => {
    void loadEvents();
  }, [loadEvents]);

  // Convites pendentes do e-mail logado (GET /invitations/me) — carga
  // independente do dashboard: falha aqui não derruba os demais painéis.
  const loadPendingInvitations = useCallback(async () => {
    try {
      const all = await getMyInvitations();
      setPendingInvitations(
        all.filter(
          (inv) =>
            inv.status === "Pending" &&
            new Date(inv.expiresAt).getTime() > Date.now(),
        ),
      );
      setInvitationsError(null);
    } catch (caught) {
      setInvitationsError(
        caught instanceof Error
          ? caught.message
          : "Não foi possível carregar seus convites.",
      );
    } finally {
      setInvitationsLoaded(true);
    }
  }, []);

  useEffect(() => {
    void loadPendingInvitations();
  }, [loadPendingInvitations]);

  const decideInvitation = async (
    invitation: InvitationItem,
    decision: "accept" | "reject",
  ) => {
    setInvitationBusyId(invitation.id);
    setInvitationsError(null);
    try {
      if (decision === "accept") {
        await acceptInvitation(invitation.id);
        setInvitationsNotice(`Convite de ${invitation.email} aceito.`);
      } else {
        await rejectInvitation(invitation.id);
        setInvitationsNotice(`Convite de ${invitation.email} recusado.`);
      }
      setPendingInvitations((current) =>
        current.filter((inv) => inv.id !== invitation.id),
      );
    } catch (caught) {
      setInvitationsError(
        caught instanceof Error
          ? caught.message
          : "Não foi possível processar o convite.",
      );
    } finally {
      setInvitationBusyId(null);
    }
  };

  const openTasks = tasks.filter((task) => !TERMINAL_TASK_STATES.includes(task.state));
  const inProgressTasks = openTasks.filter((task) => task.state === "InProgress");
  const criticalOpenAlerts = openAlerts.filter((alert) => alert.severity === "Critical");
  const healthItems = deriveHealth(loading, openAlerts, openIncidentTotal, openTasks.length, deviceTotal);

  return (
    <div className="uni-dash-page">
      {error && <ErrorBanner message={error} />}

      {/* ============ CONVITES PENDENTES (ações do usuário) ============ */}
      {/* O aviso de resultado também mantém o painel de pé: aceitar/recusar o
          ÚLTIMO convite esvazia `pendingInvitations`, e a condição antiga
          desmontava o painel — junto com o próprio aviso. Ou seja, a
          confirmação nunca chegava a ser vista (F29-T004). */}
      {invitationsLoaded &&
        (pendingInvitations.length > 0 || invitationsError || invitationsNotice) && (
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>
              Convites pendentes
              {pendingInvitations.length > 0 && (
                <span className="uni-dash-invite-count">
                  {pendingInvitations.length}
                </span>
              )}
            </h2>
          </div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {invitationsError && (
              <ErrorBanner
                message={invitationsError}
                onRetry={() => void loadPendingInvitations()}
              />
            )}
            {invitationsNotice && (
              <NoticeBanner
                message={invitationsNotice}
                onDismiss={() => setInvitationsNotice(null)}
                style={{ fontSize: 12, padding: "8px 10px" }}
              />
            )}
            {pendingInvitations.map((inv) => (
              <div key={inv.id} className="uni-dash-invite">
                <div className="uni-dash-row-body">
                  <div className="uni-dash-row-title">{inv.email}</div>
                  <div className="uni-dash-row-sub">
                    {inv.projectId ? (
                      <span>
                        Projeto: {inv.projectCode ? `${inv.projectCode} · ` : ""}{inv.projectTitle ?? inv.projectId}
                        {inv.projectRole ? ` · Papel na equipe: ${inv.projectRole}` : ""} ·
                      </span>
                    ) : (
                      inv.roles.length > 0 && <span>Papéis: {inv.roles.join(", ")} · </span>
                    )}
                    <span>expira em {formatDateTime(new Date(inv.expiresAt))}</span>
                  </div>
                </div>
                <div className="uni-dash-invite-actions">
                  <StatusChip
                    label="Pendente"
                    background="var(--uni-chip-yellow-bg)"
                    color="var(--uni-yellow)"
                    fontSize={9}
                  />
                  <Button
                    variant="primary"
                    icon="pi pi-check"
                    disabled={invitationBusyId === inv.id}
                    onClick={() => void decideInvitation(inv, "accept")}
                  >
                    Aceitar
                  </Button>
                  <Button
                    variant="ghost"
                    icon="pi pi-times"
                    disabled={invitationBusyId === inv.id}
                    onClick={() => void decideInvitation(inv, "reject")}
                  >
                    Recusar
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Ação rápida do index: criar projeto — deep-link abre o diálogo na
          própria página de projetos (`?novo=1`). */}
      <div className="uni-dash-actions">
        <Button
          variant="primary"
          icon="pi pi-plus"
          onClick={() => navigate("/projects?novo=1")}
        >
          Novo projeto
        </Button>
      </div>

      {/* ============ HEALTH BAR (estilo UniFi — F40-T001) ============ */}
      <HealthBar items={healthItems} />

      {/* ============ QUICK STATS (clicáveis — estilo UniFi) ============ */}
      <div className="uni-dash-stats">
        <DeviceHealthCard
          icon="pi-server"
          label="Dispositivos"
          value={loading ? "…" : String(deviceTotal)}
          tone="blue"
          sub="no inventário"
          onClick={() => navigate("/devices")}
        />
        <DeviceHealthCard
          icon="pi-bell"
          label="Alertas"
          value={loading ? "…" : String(openAlertTotal)}
          tone={criticalOpenAlerts.length > 0 ? "critical" : "warning"}
          sub={criticalOpenAlerts.length > 0 ? `${criticalOpenAlerts.length} críticos abertos` : "abertos"}
          onClick={() => navigate("/alerts")}
        />
        <DeviceHealthCard
          icon="pi-exclamation-triangle"
          label="Incidentes"
          value={loading ? "…" : String(incidentTotal)}
          tone="critical"
          sub={`${openIncidentTotal} em aberto`}
          onClick={() => navigate("/incidents")}
        />
        <DeviceHealthCard
          icon="pi-users"
          label="Clientes"
          value={loading ? "…" : String(clientTotal)}
          tone="purple"
          sub="descobertos"
          onClick={() => navigate("/clients")}
        />
        <DeviceHealthCard
          icon="pi-check-square"
          label="Tarefas"
          value={loading ? "…" : String(openTasks.length)}
          tone="green"
          sub={`${inProgressTasks.length} em progresso`}
          onClick={() => navigate("/tasks")}
        />
      </div>

      {/* ============ LAYOUT DUAS COLUNAS (F40-T002 — estilo UniFi) ============
          Coluna principal: o que o operador precisa ler (incidentes,
          dispositivos, clientes). Coluna lateral: o que precisa de ação
          (alertas, tarefas) + auditoria. O doughnut "Distribuição
          operacional" saiu — o espaço é do gráfico de tráfego real (F40-T003). */}
      <div className="uni-dash-layout">
        <div className="uni-dash-main">
          {/* ---- Incidentes Recentes ---- */}
          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Incidentes Recentes</h2>
              <ViewAll to="/incidents" label="Ver todos" />
            </div>
            <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {incidents.slice(0, 5).map((inc) => {
                const sev = incidentSeverityChips[inc.severity] ?? { bg: "var(--uni-chip-blue-bg)", color: "var(--uni-text-secondary)" };
                return (
                  <div key={inc.id} className="uni-dash-row">
                    <div className="uni-dash-row-body">
                      <div className="uni-dash-row-title">{inc.title}</div>
                      <div className="uni-dash-row-sub">
                        {formatDateTime(new Date(inc.openedAt))} · {inc.status}
                      </div>
                    </div>
                    <StatusChip
                      label={inc.severity}
                      background={sev.bg}
                      color={sev.color}
                      fontSize={9}
                      style={{ flexShrink: 0 }}
                    />
                  </div>
                );
              })}
              {!loading && incidents.length === 0 && (
                <div className="uni-dash-empty">Nenhum incidente cadastrado</div>
              )}
              {loading && incidents.length === 0 && (
                <LoadingBlock style={{ fontSize: 12, padding: 12 }} />
              )}
            </div>
          </div>

          {/* ---- Dispositivos ---- */}
          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Dispositivos</h2>
              <ViewAll to="/devices" label="Ver todos" />
            </div>
            <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {devices.slice(0, 6).map((device) => (
                <div key={device.id} className="uni-dash-row">
                  <i className="pi pi-desktop" style={{ fontSize: 11, color: "var(--uni-accent)", flexShrink: 0 }} />
                  <div className="uni-dash-row-body">
                    <div className="uni-dash-row-title">{device.name}</div>
                    <div className="uni-dash-row-sub">{device.model ?? device.ip ?? device.type}</div>
                  </div>
                </div>
              ))}
              {!loading && devices.length === 0 && (
                <div className="uni-dash-empty">Nenhum dispositivo</div>
              )}
              {loading && devices.length === 0 && (
                <LoadingBlock style={{ fontSize: 12, padding: 12 }} />
              )}
            </div>
          </div>

          {/* ---- Clientes Descobertos ---- */}
          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Clientes Descobertos</h2>
              <ViewAll to="/clients" label="Ver todos" />
            </div>
            <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {clients.slice(0, 5).map((cli, idx) => (
                <div key={cli.mac ?? cli.ip ?? idx} className="uni-dash-row">
                  <i className="pi pi-desktop" style={{ fontSize: 12, color: "var(--uni-accent)", flexShrink: 0 }} />
                  <div className="uni-dash-row-body">
                    <div className="uni-dash-row-title">
                      {cli.hostname || cli.ip || "Cliente sem nome"}
                    </div>
                    <div className="uni-dash-row-sub">
                      {cli.ip && <span>IP: {cli.ip} </span>}
                      {cli.mac && <span>· MAC: {cli.mac} </span>}
                      {cli.vlan && <span>· VLAN: {cli.vlan}</span>}
                    </div>
                  </div>
                  <StatusChip
                    label={cli.kind}
                    background="var(--uni-chip-blue-bg)"
                    color="var(--uni-text-secondary)"
                    fontSize={9}
                    style={{ flexShrink: 0 }}
                  />
                </div>
              ))}
              {!loading && clients.length === 0 && (
                <div className="uni-dash-empty">Nenhum cliente descoberto</div>
              )}
              {loading && clients.length === 0 && (
                <LoadingBlock style={{ fontSize: 12, padding: 12 }} />
              )}
            </div>
          </div>

          {/* ---- Monitoramento (Zabbix) — F40-T003 ----
              Só renderiza quando o summary carregou com sucesso; Zabbix
              fora do ar / não configurado simplesmente não mostra o painel
              (o dashboard inteiro continua de pé). */}
          {zabbix?.isConfigured && (
            <div className="uni-panel uni-dash-zabbix">
              <div className="uni-panel-header">
                <h2>Monitoramento (Zabbix)</h2>
                <ViewAll to="/zabbix" label="Ver detalhes" />
              </div>
              <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                <div className="uni-dash-stats">
                  <DeviceHealthCard
                    icon="pi-server"
                    label="Hosts"
                    value={String(zabbix.totalHosts)}
                    tone="blue"
                    sub={`${zabbix.enabledHosts} habilitados`}
                  />
                  <DeviceHealthCard
                    icon="pi-exclamation-circle"
                    label="Problemas"
                    value={String(zabbix.problemsTotal)}
                    tone={zabbix.problemsTotal > 0 ? "warning" : "green"}
                    sub={`${zabbix.hostsWithProblems} hosts afetados`}
                  />
                  <DeviceHealthCard
                    icon="pi-sync"
                    label="Versão"
                    value={zabbix.detectedVersion ?? "—"}
                    tone="blue"
                    sub={
                      zabbix.lastInventorySyncAtUtc
                        ? `sync ${formatDateTime(new Date(zabbix.lastInventorySyncAtUtc))}`
                        : "sem sync"
                    }
                  />
                </div>
                {zabbix.problemsAvailable && zabbix.problemsBySeverity.some((n) => n > 0) && (
                  <MetricChart
                    type="bar"
                    labels={["Infor", "Aviso", "Médio", "Alta", "grave", "Desastre"]}
                    values={zabbix.problemsBySeverity}
                    label="Problemas"
                    ariaLabel="Problemas por severidade (Zabbix)"
                    height={180}
                  />
                )}
                {!zabbix.problemsAvailable && (
                  <div className="uni-dash-empty">
                    Problemas indisponíveis{zabbix.problemsError ? `: ${zabbix.problemsError}` : "."}
                  </div>
                )}
              </div>
            </div>
          )}
        </div>

        <div className="uni-dash-side">
          {/* ---- Alertas Abertos ---- */}
          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Alertas Abertos</h2>
              <ViewAll to="/alerts" label="Ver todos" />
            </div>
            <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {openAlerts.slice(0, 5).map((alert) => {
                const critical = alert.severity === "Critical";
                return (
                  <div key={alert.id} className={`uni-dash-alert${critical ? " is-critical" : ""}`}>
                    <div className="uni-dash-alert-title">
                      <i className={`pi ${critical ? "pi-exclamation-triangle" : "pi-exclamation-circle"}`} />
                      <span>{alert.deviceName ?? "Sem device"} · {alert.check ?? alert.source}</span>
                    </div>
                    <div className="uni-dash-alert-message">{alert.message}</div>
                  </div>
                );
              })}
              {!loading && openAlerts.length === 0 && (
                <div className="uni-dash-empty">Nenhum alerta aberto 🎉</div>
              )}
              {loading && openAlerts.length === 0 && (
                <LoadingBlock style={{ fontSize: 12, padding: 12 }} />
              )}
            </div>
          </div>

          {/* ---- Tarefas ---- */}
          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Tarefas</h2>
              <ViewAll to="/tasks" label="Ver todas" />
            </div>
            <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {openTasks.slice(0, 6).map((task) => {
                const chip = taskStateChips[task.state] ?? { bg: "var(--uni-chip-blue-bg)", color: "var(--uni-text-secondary)" };
                return (
                  <div key={task.id} className="uni-dash-row" style={{ display: "block" }}>
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 4 }}>
                      <span className="uni-dash-row-title" style={{ fontWeight: 500 }}>{task.title}</span>
                      <StatusChip
                        label={taskStateLabels[task.state] ?? task.state}
                        background={chip.bg}
                        color={chip.color}
                        fontSize={9}
                        title={task.state}
                        style={{ flexShrink: 0 }}
                      />
                    </div>
                    <div className="uni-dash-row-sub" style={{ display: "flex", alignItems: "center", gap: 8 }}>
                      <span><i className="pi pi-tag" style={{ marginRight: 3 }} />{task.kind}</span>
                      <span><i className="pi pi-calendar" style={{ marginRight: 3 }} />{formatDateTime(new Date(task.openedAt))}</span>
                    </div>
                  </div>
                );
              })}
              {!loading && openTasks.length === 0 && (
                <div className="uni-dash-empty">Nenhuma tarefa aberta</div>
              )}
              {loading && openTasks.length === 0 && (
                <LoadingBlock style={{ fontSize: 12, padding: 12 }} />
              )}
            </div>
          </div>

          {/* ---- Eventos Recentes (auditoria) ---- */}
          <div className="uni-panel">
            <div className="uni-panel-header">
              <h2>Eventos Recentes</h2>
              {!eventsForbidden && !eventsError && <ViewAll to="/audit" label="Ver todos" />}
            </div>
            <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {eventsForbidden && (
                <div className="uni-dash-empty">
                  Acesso à auditoria restrito ao nível de administração.
                </div>
              )}
              {eventsError && (
                <ErrorBanner
                  message="Não foi possível carregar os eventos."
                  onRetry={() => void loadEvents()}
                  style={{ fontSize: 12, justifyContent: "center", padding: 8 }}
                />
              )}
              {events === null && !eventsForbidden && !eventsError && (
                <LoadingBlock style={{ fontSize: 12, padding: 12 }} />
              )}
              {events?.map((event) => (
                <div key={event.id} className="uni-dash-event">
                  <span
                    className={`uni-dash-event-dot${event.outcome === AUDIT_OUTCOME_SUCCEEDED ? "" : " is-failed"}`}
                  />
                  <div className="uni-dash-event-body">
                    <div>{event.action}</div>
                    <div className="uni-dash-event-resource">{event.resource}{event.error ? ` · ${event.error}` : ""}</div>
                  </div>
                  <span className="uni-dash-event-time">{formatDateTime(new Date(event.occurredAt))}</span>
                </div>
              ))}
              {events && events.length === 0 && (
                <div className="uni-dash-empty">Nenhum evento registrado</div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
