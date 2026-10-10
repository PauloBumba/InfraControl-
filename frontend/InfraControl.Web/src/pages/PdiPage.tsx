import { useCallback, useEffect, useState, type FormEvent } from "react";
import { DataTable } from "primereact/datatable";
import { Column } from "primereact/column";
import { useAppSelector } from "@/app/store/hooks";
import { formatDateTime } from "@/utils/format";
import {
  generatePdiKnowledge,
  listPdis,
  upsertPdi,
  getPdiHistory,
  PDI_HISTORY_TYPE_LABEL,
  PDI_NEXT_STATUS,
  PDI_STATUS_LABEL,
  PDI_STATUS_OPTIONS,
  PDI_STATUS_STYLE,
  getPdiDashboard,
  getPdiManagerDashboard,
  getPdiAdminDashboard,
  type Pdi,
  type PdiAdminDashboard,
  type PdiDashboard,
  type PdiHistoryEntry,
  type PdiManagerDashboard,
  type PdiManagerPlan,
  type PdiStatus,
} from "@/services/api/pdi";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
import { PdiStructureDialog } from "@/components/pdi/PdiStructureDialog";
import { PdiEvaluationDialog } from "@/components/pdi/PdiEvaluationDialog";
import { PdiReportsDialog } from "@/components/pdi/PdiReportsDialog";
import {
  PdiAdminDashboardPanel,
  PdiManagerDashboardPanel,
  PdiOwnDashboardPanel,
} from "@/components/pdi/PdiDashboardPanels";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { StatusChip } from "@/components/ui/StatusChip";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { SelectField } from "@/components/ui/SelectField";
import { FormField, FormError } from "@/components/ui/FormField";
import { LoadingBlock } from "@/components/ui/LoadingBlock";

/**
 * PDI — Plano de Desenvolvimento Individual (docs/29 §8, docs/30,
 * ADR-029). Ciclo de 9 estados: o seletor de status mostra só os
 * próximos estados válidos (tabela docs/30 §3 — backend revalida);
 * Admin define gestor/período e transiciona tudo, Operador só edita
 * campos e o aceite (AwaitingAcceptance → Active). §14 (Fase 5,
 * ADR-035): mudar campos (estratégia/prazo/gestor/período) exige o
 * motivo — obrigatório no formulário, com valor anterior → novo no
 * timeline; transição de status sozinha mantém o motivo opcional
 * (§15 aceite). Criação é exclusiva do Admin e começa em Rascunho.
 * Rota nível `operate` (Viewer não alcança). Datas em `yyyy-MM-dd`
 * (DateOnly) formatadas à mão para não sofrer deslocamento de fuso do
 * `Date`.
 */
function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

function formatDateOnly(value: string): string {
  const [year, month, day] = value.split("-");
  return year && month && day ? `${day}/${month}/${year}` : value;
}

function formatPeriod(plan: Pdi): string {
  if (plan.periodStart && plan.periodEnd)
    return `${formatDateOnly(plan.periodStart)} a ${formatDateOnly(plan.periodEnd)}`;
  if (plan.periodStart) return `a partir de ${formatDateOnly(plan.periodStart)}`;
  if (plan.periodEnd) return `até ${formatDateOnly(plan.periodEnd)}`;
  return "—";
}

const dateInputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #e5e7eb",
  borderRadius: 6,
  width: "100%",
};

export function PdiPage() {
  const user = useAppSelector((state) => state.auth.user);
  const isAdmin = (user?.roles ?? []).includes("Admin");

  const [items, setItems] = useState<Pdi[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // F24-T006/K6 — aviso do "Gerar Knowledge" (docs/32 §4).
  const [notice, setNotice] = useState<string | null>(null);

  // Dialog unificado: criar (admin, com dono) e editar (admin ou dono).
  const [dialogVisible, setDialogVisible] = useState(false);
  const [editUserId, setEditUserId] = useState<string | null>(null);
  const [original, setOriginal] = useState<Pdi | null>(null);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [ownerId, setOwnerId] = useState("");
  const [strategy, setStrategy] = useState("");
  const [targetDate, setTargetDate] = useState("");
  const [status, setStatus] = useState<PdiStatus>("Draft");
  const [managerId, setManagerId] = useState("");
  const [periodStart, setPeriodStart] = useState("");
  const [periodEnd, setPeriodEnd] = useState("");
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Timeline do plano (docs/30 §17).
  const [historyVisible, setHistoryVisible] = useState(false);
  const [historyOwnerName, setHistoryOwnerName] = useState("");
  const [historyEntries, setHistoryEntries] = useState<PdiHistoryEntry[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);

  // Estrutura do desenvolvimento (docs/30 §5-§8, Fase 2).
  const [structurePlan, setStructurePlan] = useState<Pdi | null>(null);

  // Avaliação final (docs/30 §15.3/§16, Fase 6).
  const [evaluationPlan, setEvaluationPlan] = useState<Pdi | null>(null);

  // Relatórios/indicadores (docs/30 §21, §22 — Fase 8, ADR-038).
  const [reportsVisible, setReportsVisible] = useState(false);

  // Dashboards (docs/30 §18-§20, Fase 7) — painéis opcionais: um erro
  // em um deles não derruba a página.
  const [ownDashboard, setOwnDashboard] = useState<PdiDashboard | null>(null);
  const [managerDashboard, setManagerDashboard] =
    useState<PdiManagerDashboard | null>(null);
  const [adminDashboard, setAdminDashboard] =
    useState<PdiAdminDashboard | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setItems(await listPdis());
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const ownPdiId = !isAdmin ? (items[0]?.userId ?? null) : null;

  useEffect(() => {
    if (loading) return;
    let cancelled = false;
    void (async () => {
      const loadOwn = ownPdiId
        ? getPdiDashboard(ownPdiId).catch(() => null)
        : Promise.resolve(null);
      const [own, managed, admin] = await Promise.all([
        loadOwn,
        getPdiManagerDashboard().catch(() => null),
        isAdmin ? getPdiAdminDashboard().catch(() => null) : Promise.resolve(null),
      ]);
      if (cancelled) return;
      setOwnDashboard(own);
      setManagerDashboard(managed);
      setAdminDashboard(admin);
    })();
    return () => {
      cancelled = true;
    };
  }, [loading, ownPdiId, isAdmin]);

  const resetDialog = () => {
    setEditUserId(null);
    setOriginal(null);
    setStrategy("");
    setTargetDate("");
    setStatus("Draft");
    setOwnerId("");
    setManagerId("");
    setPeriodStart("");
    setPeriodEnd("");
    setReason("");
    setFormError(null);
  };

  const openCreate = async () => {
    resetDialog();
    setDialogVisible(true);
    try {
      setUsers(await listUsers());
    } catch {
      setUsers([]);
    }
  };

  const openEdit = async (plan: Pdi) => {
    resetDialog();
    setEditUserId(plan.userId);
    setOriginal(plan);
    setStrategy(plan.strategy);
    setTargetDate(plan.targetDate);
    setStatus(plan.status);
    setManagerId(plan.managerId ?? "");
    setPeriodStart(plan.periodStart ?? "");
    setPeriodEnd(plan.periodEnd ?? "");
    setDialogVisible(true);
    if (isAdmin) {
      try {
        setUsers(await listUsers());
      } catch {
        setUsers([]);
      }
    }
  };

  const openHistory = async (plan: Pdi) => {
    setHistoryOwnerName(plan.ownerName);
    setHistoryEntries([]);
    setHistoryError(null);
    setHistoryLoading(true);
    setHistoryVisible(true);
    try {
      setHistoryEntries(await getPdiHistory(plan.userId));
    } catch (caught) {
      setHistoryError(messageFor(caught));
    } finally {
      setHistoryLoading(false);
    }
  };

  const statusOptions = original
    ? PDI_STATUS_OPTIONS.filter(
        (option) =>
          option.value === original.status ||
          PDI_NEXT_STATUS[original.status].includes(option.value),
      )
    : PDI_STATUS_OPTIONS;

  const statusChanged = !!original && status !== original.status;

  // §14 (Fase 5): campos alterados ⇒ motivo obrigatório. Gestor/período
  // só o Admin envia (o Operador nem inclui no payload).
  const fieldsChanged =
    !!original &&
    (strategy !== original.strategy ||
      targetDate !== original.targetDate ||
      (isAdmin &&
        (managerId !== (original.managerId ?? "") ||
          periodStart !== (original.periodStart ?? "") ||
          periodEnd !== (original.periodEnd ?? ""))));

  const needsReason = fieldsChanged;
  const showReason = fieldsChanged || statusChanged;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const targetUserId = editUserId ?? ownerId;
    if (!targetUserId) {
      setFormError("Selecione o usuário do PDI.");
      return;
    }
    if (!targetDate) {
      setFormError("Informe o prazo do PDI.");
      return;
    }
    if (periodStart && periodEnd && periodEnd < periodStart) {
      setFormError("O fim do período não pode ser anterior ao início.");
      return;
    }
    if (needsReason && !reason.trim()) {
      setFormError("O motivo da alteração é obrigatório.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      const payload: Parameters<typeof upsertPdi>[1] = {
        strategy,
        targetDate,
        status,
      };
      if (isAdmin) {
        // Gestor/período: só o Admin define (ADR-029 §3) — o Operador
        // nem envia, o backend manteria os valores vigentes.
        payload.managerId = managerId || null;
        payload.periodStart = periodStart || null;
        payload.periodEnd = periodEnd || null;
      }
      if (reason.trim()) payload.reason = reason.trim();
      await upsertPdi(targetUserId, payload);
      setDialogVisible(false);
      await load();
    } catch (caught) {
      setFormError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  };

  const ownPdi: Pdi | null = isAdmin ? null : (items[0] ?? null);

  // §19 — "acesso direto aos casos": abre o diálogo de avaliação do
  // plano gerenciado (leitura permitida ao gestor — ADR-037).
  const openManagedPlan = (plan: PdiManagerPlan) => {
    const known = items.find((item) => item.userId === plan.userId);
    if (known) {
      setEvaluationPlan(known);
      return;
    }
    setEvaluationPlan({
      userId: plan.userId,
      ownerName: plan.ownerName,
      ownerEmail: "",
      strategy: "",
      targetDate: plan.targetDate,
      status: plan.status,
      managerId: user?.id ?? null,
      managerName: null,
      periodStart: null,
      periodEnd: null,
      updateDateTime: new Date().toISOString(),
    });
  };

  const managerPanel =
    managerDashboard && managerDashboard.totalPlans > 0 ? (
      <PdiManagerDashboardPanel
        dashboard={managerDashboard}
        onOpenPlan={openManagedPlan}
      />
    ) : null;

  const statusChip = (value: PdiStatus) => (
    <StatusChip
      label={PDI_STATUS_LABEL[value] ?? value}
      background={PDI_STATUS_STYLE[value]?.background ?? "#f1f5f9"}
      color={PDI_STATUS_STYLE[value]?.color ?? "#64748b"}
    />
  );

  const userCell = (plan: Pdi) => (
    <div>
      <div style={{ fontWeight: 500, display: "flex", alignItems: "center", gap: 6 }}>
        <i className="pi pi-user" style={{ fontSize: 11, color: "#94a3b8" }} />
        {plan.ownerName}
      </div>
      <div style={{ fontSize: 11, color: "#94a3b8" }}>{plan.ownerEmail}</div>
    </div>
  );

  /**
   * F24-T006/K6 (docs/32 §4, ADR-035): rascunho montado dos registros
   * do PDI concluído — nasce Draft e vai para revisão.
   */
  const generateKnowledge = async (plan: Pdi) => {
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      await generatePdiKnowledge(plan.userId);
      setNotice("Rascunho de Knowledge gerado — revise na Base de Conhecimento.");
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  };

  const actionsCell = (plan: Pdi) => (
    <div style={{ display: "flex", gap: 6 }}>
      <Button variant="ghost" onClick={() => setStructurePlan(plan)}>
        Estrutura
      </Button>
      <Button variant="ghost" onClick={() => setEvaluationPlan(plan)}>
        Avaliar
      </Button>
      <Button variant="ghost" onClick={() => void openHistory(plan)}>
        Histórico
      </Button>
      {/* F24-T006/K6 — só o PDI concluído disponibiliza o rascunho (§4). */}
      {plan.status === "Completed" && (
        <Button
          variant="ghost"
          disabled={saving}
          onClick={() => void generateKnowledge(plan)}
        >
          Gerar Knowledge
        </Button>
      )}
      <Button variant="ghost" onClick={() => void openEdit(plan)}>
        Editar
      </Button>
    </div>
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
      <PageHeader
        title="PDI"
        subtitle={
          loading
            ? "Carregando…"
            : isAdmin
              ? `${items.length} ${items.length === 1 ? "plano" : "planos"} · Plano de Desenvolvimento Individual`
              : "Meu Plano de Desenvolvimento Individual"
        }
        actions={
          isAdmin ? (
            <div style={{ display: "flex", gap: 8 }}>
              <Button variant="ghost" onClick={() => setReportsVisible(true)}>
                Relatórios
              </Button>
              <Button variant="primary" onClick={() => void openCreate()}>
                <i className="pi pi-plus" style={{ fontSize: 11 }} />
                Novo PDI
              </Button>
            </div>
          ) : undefined
        }
      />

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      {notice && (
        <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />
      )}

      {!loading && adminDashboard && (
        <PdiAdminDashboardPanel dashboard={adminDashboard} />
      )}
      {!loading && managerPanel}

      {loading ? (
        <LoadingBlock />
      ) : isAdmin ? (
        <DataTable
          value={items}
          paginator
          rows={10}
          rowsPerPageOptions={[10, 20, 50]}
          emptyMessage={'Nenhum PDI cadastrado — use "Novo PDI".'}
          size="small"
        >
          <Column header="Usuário" body={userCell} style={{ minWidth: 200 }} />
          <Column
            header="Estratégia"
            body={(plan: Pdi) => (
              <span title={plan.strategy} style={{ display: "block", maxWidth: 300, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {plan.strategy}
              </span>
            )}
            style={{ minWidth: 220 }}
          />
          <Column header="Prazo" body={(plan: Pdi) => formatDateOnly(plan.targetDate)} style={{ width: 105 }} />
          <Column
            header="Gestor"
            body={(plan: Pdi) => plan.managerName ?? "—"}
            style={{ width: 140 }}
          />
          <Column header="Status" body={(plan: Pdi) => statusChip(plan.status)} style={{ width: 155 }} />
          <Column header="Atualizado" body={(plan: Pdi) => formatDateTime(new Date(plan.updateDateTime))} style={{ width: 155 }} />
          <Column body={actionsCell} style={{ width: 360 }} />
        </DataTable>
      ) : ownPdi ? (
        <div
          style={{
            background: "white",
            border: "1px solid #e5e7eb",
            borderRadius: 8,
            padding: 16,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            maxWidth: 720,
          }}
        >
          <div className="flex align-items-center justify-content-between flex-wrap gap-2">
            <div style={{ fontWeight: 600 }}>Meu plano</div>
            <div className="flex align-items-center gap-2">
              {statusChip(ownPdi.status)}
              <Button variant="ghost" onClick={() => setReportsVisible(true)}>
                Relatório
              </Button>
              <Button variant="ghost" onClick={() => setStructurePlan(ownPdi)}>
                Estrutura
              </Button>
              <Button variant="ghost" onClick={() => setEvaluationPlan(ownPdi)}>
                Avaliar
              </Button>
              <Button variant="ghost" onClick={() => void openHistory(ownPdi)}>
                Histórico
              </Button>
              {/* F24-T006/K6 — rascunho só quando o plano concluiu (§4). */}
              {ownPdi.status === "Completed" && (
                <Button
                  variant="ghost"
                  disabled={saving}
                  onClick={() => void generateKnowledge(ownPdi)}
                >
                  Gerar Knowledge
                </Button>
              )}
              <Button variant="primary" onClick={() => void openEdit(ownPdi)}>
                Editar
              </Button>
            </div>
          </div>
          <div style={{ fontSize: 13, color: "#334155", whiteSpace: "pre-wrap" }}>{ownPdi.strategy}</div>
          <div style={{ fontSize: 12, color: "#64748b" }}>
            Prazo: {formatDateOnly(ownPdi.targetDate)} · Gestor: {ownPdi.managerName ?? "—"} · Período: {formatPeriod(ownPdi)}
          </div>
          <div style={{ fontSize: 12, color: "#64748b" }}>
            Atualizado em {formatDateTime(new Date(ownPdi.updateDateTime))}
          </div>
          {ownDashboard && <PdiOwnDashboardPanel dashboard={ownDashboard} />}
        </div>
      ) : (
        <NoticeBanner
          message="Nenhum PDI definido para o seu usuário — contate o administrador para criar o seu plano."
          background="#fffbeb"
          color="#92400e"
        />
      )}

      <Dialog
        visible={dialogVisible}
        onHide={() => setDialogVisible(false)}
        header={editUserId ? "Editar PDI" : "Novo PDI"}
        width={560}
      >
        <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {!editUserId && (
            <FormField label="Usuário">
              <SelectField
                value={ownerId}
                onChange={setOwnerId}
                options={users
                  .filter((candidate) => !items.some((plan) => plan.userId === candidate.userId))
                  .map((candidate) => ({
                    value: candidate.userId,
                    label: `${candidate.displayName} · ${candidate.email}`,
                  }))}
                placeholder="Selecione o usuário"
                aria-label="Usuário do PDI"
              />
            </FormField>
          )}

          <FormField label="Estratégia">
            <textarea
              value={strategy}
              onChange={(event) => setStrategy(event.target.value)}
              rows={5}
              maxLength={4000}
              placeholder="Direção do desenvolvimento: o que desenvolver, como e até quando."
              style={{
                resize: "vertical",
                padding: "8px 10px",
                fontSize: 13,
                border: "1px solid #e5e7eb",
                borderRadius: 6,
                fontFamily: "inherit",
              }}
            />
          </FormField>

          <div style={{ display: "flex", gap: 12 }}>
            <FormField label="Prazo" width={180}>
              <input
                type="date"
                value={targetDate}
                onChange={(event) => setTargetDate(event.target.value)}
                style={dateInputStyle}
              />
            </FormField>

            <FormField label="Status">
              <SelectField
                value={status}
                onChange={(value) => setStatus(value as PdiStatus)}
                options={[...statusOptions]}
                aria-label="Status do PDI"
              />
            </FormField>
          </div>

          {showReason && (
            <FormField
              label={
                needsReason
                  ? "Motivo da alteração"
                  : "Motivo da transição (opcional)"
              }
            >
              <input
                type="text"
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                maxLength={500}
                placeholder="Ex.: replanejado com o gestor no ciclo semestral."
                style={dateInputStyle}
              />
            </FormField>
          )}

          {isAdmin && (
            <FormField label="Gestor responsável">
              <SelectField
                value={managerId}
                onChange={setManagerId}
                options={users.map((candidate) => ({
                  value: candidate.userId,
                  label: `${candidate.displayName} · ${candidate.email}`,
                }))}
                placeholder="Sem gestor"
                aria-label="Gestor do PDI"
              />
            </FormField>
          )}

          {isAdmin && (
            <div style={{ display: "flex", gap: 12 }}>
              <FormField label="Início do período" width={180}>
                <input
                  type="date"
                  value={periodStart}
                  onChange={(event) => setPeriodStart(event.target.value)}
                  style={dateInputStyle}
                />
              </FormField>
              <FormField label="Fim do período" width={180}>
                <input
                  type="date"
                  value={periodEnd}
                  onChange={(event) => setPeriodEnd(event.target.value)}
                  style={dateInputStyle}
                />
              </FormField>
            </div>
          )}

          {formError && <FormError message={formError} />}

          <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
            <Button variant="ghost" type="button" onClick={() => setDialogVisible(false)}>
              Cancelar
            </Button>
            <Button variant="primary" type="submit" disabled={saving}>
              {saving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        </form>
      </Dialog>

      <Dialog
        visible={historyVisible}
        onHide={() => setHistoryVisible(false)}
        header={`Histórico · ${historyOwnerName}`}
        width={560}
      >
        {historyLoading ? (
          <LoadingBlock />
        ) : historyError ? (
          <ErrorBanner message={historyError} />
        ) : historyEntries.length === 0 ? (
          <NoticeBanner
            message="Nenhum evento registrado neste plano."
            background="#f8fafc"
            color="#475569"
          />
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {historyEntries.map((entry) => (
              <div
                key={entry.id}
                style={{ borderLeft: "3px solid #e5e7eb", paddingLeft: 12 }}
              >
                <div style={{ fontWeight: 600, fontSize: 13 }}>
                  {PDI_HISTORY_TYPE_LABEL[entry.type] ?? entry.type}
                  {entry.type === "StatusChanged" &&
                    entry.fromStatus &&
                    entry.toStatus && (
                      <>
                        {" · "}
                        {PDI_STATUS_LABEL[entry.fromStatus] ?? entry.fromStatus}
                        {" → "}
                        {PDI_STATUS_LABEL[entry.toStatus] ?? entry.toStatus}
                      </>
                    )}
                </div>
                <div style={{ fontSize: 12, color: "#64748b" }}>
                  {entry.actorName ?? "Usuário removido"} · {formatDateTime(new Date(entry.occurredAt))}
                </div>
                {entry.detail && (
                  <div style={{ fontSize: 12, color: "#334155" }}>
                    {entry.type === "Updated"
                      ? `Campos: ${entry.detail}`
                      : entry.detail}
                  </div>
                )}
                {entry.reason && (
                  <div style={{ fontSize: 12, color: "#334155" }}>Motivo: {entry.reason}</div>
                )}
              </div>
            ))}
          </div>
        )}
      </Dialog>

      <PdiReportsDialog
        visible={reportsVisible}
        onHide={() => setReportsVisible(false)}
        isAdmin={isAdmin}
        plans={items}
        ownUserId={ownPdi?.userId ?? null}
      />

      <PdiStructureDialog
        visible={structurePlan !== null}
        onHide={() => setStructurePlan(null)}
        plan={structurePlan}
        isAdmin={isAdmin}
        currentUserId={user?.id ?? null}
      />

      <PdiEvaluationDialog
        visible={evaluationPlan !== null}
        onHide={() => setEvaluationPlan(null)}
        plan={evaluationPlan}
        isAdmin={isAdmin}
        currentUserId={user?.id ?? null}
      />
    </div>
  );
}
