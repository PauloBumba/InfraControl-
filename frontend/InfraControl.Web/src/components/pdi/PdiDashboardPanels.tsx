import {
  PDI_PHASE_STATUS_STYLE,
  PDI_STATUS_LABEL,
  PDI_STATUS_STYLE,
  type PdiAdminDashboard,
  type PdiDashboard,
  type PdiManagerDashboard,
  type PdiManagerPlan,
  type PdiPhaseStatus,
} from "@/services/api/pdi";
import { StatusChip } from "@/components/ui/StatusChip";
import { Button } from "@/components/ui/Button";

/**
 * Dashboards do PDI (docs/30 §18, §19, §20 — Fase 7, ADR-037).
 * Três painéis: resumo do colaborador (dentro do "Meu plano"), visão
 * do gestor com acesso direto aos casos 🟡/🔴 e visão administrativa
 * com as contagens globais e a taxa de conclusão.
 */

function formatDateOnly(value: string | null): string {
  if (!value) return "—";
  const [year, month, day] = value.split("-");
  return year && month && day ? `${day}/${month}/${year}` : value;
}

function phaseGlyph(status: PdiPhaseStatus): string {
  switch (status) {
    case "Completed":
      return "✓";
    case "InProgress":
      return "●";
    case "PendingValidation":
      return "◐";
    default:
      return "○";
  }
}

function Stat({ label, value }: { label: string; value: number | string }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 96 }}>
      <div style={{ fontSize: 11, color: "#64748b" }}>{label}</div>
      <div style={{ fontSize: 18, fontWeight: 600, color: "#0f172a" }}>{value}</div>
    </div>
  );
}

/** §18 — faixa de resumo dentro do card "Meu plano". */
export function PdiOwnDashboardPanel({ dashboard }: { dashboard: PdiDashboard }) {
  return (
    <div
      style={{
        borderTop: "1px dashed #e5e7eb",
        paddingTop: 10,
        display: "flex",
        flexDirection: "column",
        gap: 8,
      }}
    >
      <div className="flex align-items-center gap-2" style={{ fontSize: 13 }}>
        <span style={{ fontWeight: 600 }}>Progresso</span>
        <div
          style={{
            flex: 1,
            height: 8,
            background: "#e2e8f0",
            borderRadius: 4,
            overflow: "hidden",
          }}
        >
          <div
            style={{
              width: `${dashboard.progressPercent}%`,
              height: "100%",
              background: "#22c55e",
            }}
          />
        </div>
        <span style={{ fontWeight: 600, color: "#15803d", minWidth: 40, textAlign: "right" }}>
          {dashboard.progressPercent}%
        </span>
      </div>

      {dashboard.phases.length > 0 && (
        <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
          {dashboard.phases.map((phase) => (
            <span
              key={phase.id}
              title={phase.title}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 5,
                fontSize: 11,
                padding: "3px 8px",
                borderRadius: 999,
                border: "1px solid #e5e7eb",
                background: PDI_PHASE_STATUS_STYLE[phase.status]?.background ?? "#f1f5f9",
                color: PDI_PHASE_STATUS_STYLE[phase.status]?.color ?? "#64748b",
                maxWidth: 220,
              }}
            >
              <span>{phaseGlyph(phase.status)}</span>
              <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {phase.title}
              </span>
            </span>
          ))}
        </div>
      )}

      <div style={{ fontSize: 12, color: "#64748b" }}>
        Próximo prazo {formatDateOnly(dashboard.nextDueDate)} · Pendências{" "}
        {dashboard.openImpediments} · Evidências {dashboard.totalEvidences} · Check-ins{" "}
        {dashboard.totalCheckIns}
      </div>
    </div>
  );
}

/** §19 — "Meus PDIs": classificação 🟢/🟡/🔴 + agregados + casos diretos. */
export function PdiManagerDashboardPanel({
  dashboard,
  onOpenPlan,
}: {
  dashboard: PdiManagerDashboard;
  onOpenPlan: (plan: PdiManagerPlan) => void;
}) {
  const cases = dashboard.plans.filter(
    (plan) => plan.overdue || plan.needsAttention,
  );

  return (
    <div
      style={{
        background: "white",
        border: "1px solid #e5e7eb",
        borderRadius: 8,
        padding: 16,
        display: "flex",
        flexDirection: "column",
        gap: 10,
      }}
    >
      <div style={{ fontWeight: 600 }}>
        Meus PDIs — {dashboard.totalPlans}{" "}
        {dashboard.totalPlans === 1 ? "colaborador" : "colaboradores"}
      </div>

      <div style={{ display: "flex", flexWrap: "wrap", gap: 16, fontSize: 13 }}>
        <span>🟢 {dashboard.onTrack} dentro do prazo</span>
        <span>🟡 {dashboard.needsAttention} precisam de atenção</span>
        <span>🔴 {dashboard.overdue} atrasados</span>
      </div>

      <div style={{ fontSize: 12, color: "#64748b" }}>
        Fases próximas do vencimento: {dashboard.phasesDueSoon} · Impedimentos ativos:{" "}
        {dashboard.openImpediments} · Evidências aguardando validação:{" "}
        {dashboard.pendingEvidences}
      </div>

      {cases.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          {cases.map((plan) => (
            <div
              key={plan.userId}
              className="flex align-items-center justify-content-between flex-wrap gap-2"
              style={{ border: "1px solid #f1f5f9", borderRadius: 6, padding: "6px 10px" }}
            >
              <div className="flex align-items-center gap-2" style={{ fontSize: 13 }}>
                <span style={{ fontWeight: 500 }}>{plan.ownerName}</span>
                <StatusChip
                  label={plan.overdue ? "Atrasado" : "Atenção"}
                  background={plan.overdue ? "#fee2e2" : "#fef3c7"}
                  color={plan.overdue ? "#b91c1c" : "#b45309"}
                />
                <StatusChip
                  label={PDI_STATUS_LABEL[plan.status] ?? plan.status}
                  background={PDI_STATUS_STYLE[plan.status]?.background ?? "#f1f5f9"}
                  color={PDI_STATUS_STYLE[plan.status]?.color ?? "#64748b"}
                />
                <span style={{ color: "#64748b" }}>
                  prazo {formatDateOnly(plan.targetDate)}
                </span>
              </div>
              <Button
                variant="ghost"
                onClick={() => onOpenPlan(plan)}
              >
                Abrir
              </Button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/** §20 — visão administrativa: contagens globais e taxa de conclusão. */
export function PdiAdminDashboardPanel({ dashboard }: { dashboard: PdiAdminDashboard }) {
  return (
    <div
      style={{
        background: "white",
        border: "1px solid #e5e7eb",
        borderRadius: 8,
        padding: 16,
        display: "flex",
        flexDirection: "column",
        gap: 12,
      }}
    >
      <div style={{ fontWeight: 600 }}>PDI — visão administrativa</div>

      <div style={{ display: "flex", flexWrap: "wrap", gap: 18 }}>
        <Stat label="PDIs criados" value={dashboard.totalPlans} />
        <Stat label="Concluídos" value={dashboard.completedPlans} />
        <Stat label="Em andamento" value={dashboard.inProgressPlans} />
        <Stat label="Replanejados" value={dashboard.replannedPlans} />
        <Stat label="Cancelados" value={dashboard.cancelledPlans} />
        <Stat
          label="Taxa de conclusão"
          value={`${dashboard.completionRatePercent}%`}
        />
      </div>

      <div style={{ display: "flex", flexWrap: "wrap", gap: 18 }}>
        <Stat label="Fases concluídas" value={dashboard.completedPhases} />
        <Stat label="Check-ins" value={dashboard.totalCheckIns} />
        <Stat label="Evidências" value={dashboard.totalEvidences} />
        <Stat label="Impedimentos" value={dashboard.totalImpediments} />
      </div>
    </div>
  );
}
