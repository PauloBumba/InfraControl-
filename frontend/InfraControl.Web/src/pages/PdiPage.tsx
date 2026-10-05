import { useCallback, useEffect, useState, type FormEvent } from "react";
import { DataTable } from "primereact/datatable";
import { Column } from "primereact/column";
import { useAppSelector } from "@/app/store/hooks";
import { formatDateTime } from "@/utils/format";
import {
  listPdis,
  upsertPdi,
  PDI_STATUS_OPTIONS,
  PDI_STATUS_STYLE,
  type Pdi,
  type PdiStatus,
} from "@/services/api/pdi";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
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
 * PDI — Plano de Desenvolvimento Individual (docs/29 §8, decisão
 * 2026-10-03). "Admin gerencia todo o CRUD, Operador edita o próprio":
 * admin vê a lista de planos e cria/edita qualquer um (dialog com seletor
 * de usuário); o operador vê o SEU plano (card) e edita pelo mesmo dialog
 * sem seletor — sem plano, aviso para procurar o administrador (criação é
 * exclusiva do Admin, 403 no backend). Rota nível `operate` (Viewer não
 * alcança). Prazo em `yyyy-MM-dd` (DateOnly) formatado à mão para não
 * sofrer deslocamento de fuso do `Date`.
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

export function PdiPage() {
  const user = useAppSelector((state) => state.auth.user);
  const isAdmin = (user?.roles ?? []).includes("Admin");

  const [items, setItems] = useState<Pdi[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Dialog unificado: criar (admin, com dono) e editar (admin ou dono).
  const [dialogVisible, setDialogVisible] = useState(false);
  const [editUserId, setEditUserId] = useState<string | null>(null);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [ownerId, setOwnerId] = useState("");
  const [strategy, setStrategy] = useState("");
  const [targetDate, setTargetDate] = useState("");
  const [status, setStatus] = useState<PdiStatus>("InProgress");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

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

  const openCreate = async () => {
    setEditUserId(null);
    setStrategy("");
    setTargetDate("");
    setStatus("InProgress");
    setOwnerId("");
    setFormError(null);
    setDialogVisible(true);
    try {
      const all = await listUsers();
      setUsers(all.filter((candidate) => !items.some((plan) => plan.userId === candidate.userId)));
    } catch {
      setUsers([]);
    }
  };

  const openEdit = (plan: Pdi) => {
    setEditUserId(plan.userId);
    setStrategy(plan.strategy);
    setTargetDate(plan.targetDate);
    setStatus(plan.status);
    setOwnerId("");
    setFormError(null);
    setDialogVisible(true);
  };

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
    setSaving(true);
    setFormError(null);
    try {
      await upsertPdi(targetUserId, { strategy, targetDate, status });
      setDialogVisible(false);
      await load();
    } catch (caught) {
      setFormError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  };

  const ownPdi: Pdi | null = isAdmin ? null : (items[0] ?? null);

  const statusChip = (value: PdiStatus) => (
    <StatusChip
      label={PDI_STATUS_OPTIONS.find((option) => option.value === value)?.label ?? value}
      background={PDI_STATUS_STYLE[value].background}
      color={PDI_STATUS_STYLE[value].color}
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

  const editCell = (plan: Pdi) => (
    <Button variant="ghost" onClick={() => openEdit(plan)}>
      Editar
    </Button>
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
            <Button variant="primary" onClick={() => void openCreate()}>
              <i className="pi pi-plus" style={{ fontSize: 11 }} />
              Novo PDI
            </Button>
          ) : undefined
        }
      />

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

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
          <Column header="Usuário" body={userCell} style={{ minWidth: 220 }} />
          <Column
            header="Estratégia"
            body={(plan: Pdi) => (
              <span title={plan.strategy} style={{ display: "block", maxWidth: 380, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {plan.strategy}
              </span>
            )}
            style={{ minWidth: 260 }}
          />
          <Column header="Prazo" body={(plan: Pdi) => formatDateOnly(plan.targetDate)} style={{ width: 110 }} />
          <Column header="Status" body={(plan: Pdi) => statusChip(plan.status)} style={{ width: 140 }} />
          <Column header="Atualizado" body={(plan: Pdi) => formatDateTime(new Date(plan.updateDateTime))} style={{ width: 160 }} />
          <Column body={editCell} style={{ width: 110 }} />
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
              <Button variant="primary" onClick={() => openEdit(ownPdi)}>
                Editar
              </Button>
            </div>
          </div>
          <div style={{ fontSize: 13, color: "#334155", whiteSpace: "pre-wrap" }}>{ownPdi.strategy}</div>
          <div style={{ fontSize: 12, color: "#64748b" }}>
            Prazo: {formatDateOnly(ownPdi.targetDate)} · Atualizado em {formatDateTime(new Date(ownPdi.updateDateTime))}
          </div>
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
        width={520}
      >
        <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {!editUserId && (
            <FormField label="Usuário">
              <SelectField
                value={ownerId}
                onChange={setOwnerId}
                options={users.map((candidate) => ({
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
                style={{
                  padding: "7px 10px",
                  fontSize: 13,
                  border: "1px solid #e5e7eb",
                  borderRadius: 6,
                  width: "100%",
                }}
              />
            </FormField>

            <FormField label="Status">
              <SelectField
                value={status}
                onChange={(value) => setStatus(value as PdiStatus)}
                options={[...PDI_STATUS_OPTIONS]}
                aria-label="Status do PDI"
              />
            </FormField>
          </div>

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
    </div>
  );
}
