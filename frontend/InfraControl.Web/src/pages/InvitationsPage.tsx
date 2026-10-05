import { useCallback, useEffect, useState } from "react";
import { DataTable } from "primereact/datatable";
import { Column } from "primereact/column";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { SearchInput } from "@/components/ui/SearchInput";
import { Button } from "@/components/ui/Button";
import { InlineMessages, useInlineMessages } from "@/components/ui/InlineMessages";
import { useAppSelector } from "@/app/store/hooks";
import { formatDateTime } from "@/utils/format";
import {
  listInvitations,
  getMyInvitations,
  acceptInvitation,
  rejectInvitation,
  type InvitationItem,
} from "@/services/api/invitations";

const statusColors: Record<string, { bg: string; color: string; label: string }> = {
  Pending: { bg: "#fef3c7", color: "#d97706", label: "Pendente" },
  Accepted: { bg: "#dcfce7", color: "#16a34a", label: "Aceito" },
  Rejected: { bg: "#fee2e2", color: "#dc2626", label: "Recusado" },
  Expired: { bg: "#f1f5f9", color: "#64748b", label: "Expirado" },
  Cancelled: { bg: "#f1f5f9", color: "#64748b", label: "Cancelado" },
  Activated: { bg: "#dcfce7", color: "#16a34a", label: "Ativado" },
};

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function InvitationsPage() {
  const [invitations, setInvitations] = useState<InvitationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { messagesRef, showMessage } = useInlineMessages();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [busyId, setBusyId] = useState<string | null>(null);

  const roles = useAppSelector((state) => state.auth.user?.roles || []);
  const isAdmin = roles.includes("Admin");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      if (isAdmin) {
        // Admin vê todos os convites
        const data = await listInvitations({
          status: statusFilter || undefined,
          email: search || undefined,
          skip: 0,
          take: 50,
        });
        setInvitations(data.items);
      } else {
        // Usuário comum vê apenas seus convites
        const data = await getMyInvitations();
        setInvitations(data);
      }
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, isAdmin]);

  useEffect(() => {
    void load();
  }, [load]);

  const handleAccept = async (id: string) => {
    setBusyId(id);
    try {
      await acceptInvitation(id);
      showMessage({ severity: "success", summary: "Convite aceito com sucesso." });
      await load();
    } catch (caught) {
      showMessage({ severity: "error", summary: messageFor(caught) });
    } finally {
      setBusyId(null);
    }
  };

  const handleReject = async (id: string) => {
    setBusyId(id);
    try {
      await rejectInvitation(id);
      showMessage({ severity: "success", summary: "Convite recusado com sucesso." });
      await load();
    } catch (caught) {
      showMessage({ severity: "error", summary: messageFor(caught) });
    } finally {
      setBusyId(null);
    }
  };

  const filtered = invitations.filter((inv) => {
    if (statusFilter && inv.status !== statusFilter) return false;
    if (search && !inv.email.toLowerCase().includes(search.toLowerCase())) return false;
    return true;
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Convites"
        subtitle="Gerencie convites de usuários à plataforma"
      />

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      {isAdmin && (
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <div style={{ flex: 1 }}>
            <SearchInput
              value={search}
              onChange={setSearch}
              placeholder="Buscar por e-mail..."
            />
          </div>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            style={{
              padding: "8px 12px",
              borderRadius: 6,
              border: "1px solid #e5e7eb",
              background: "white",
              fontSize: 13,
            }}
          >
            <option value="">Todos os status</option>
            <option value="Pending">Pendente</option>
            <option value="Accepted">Aceito</option>
            <option value="Rejected">Recusado</option>
            <option value="Expired">Expirado</option>
            <option value="Cancelled">Cancelado</option>
            <option value="Activated">Ativado</option>
          </select>
        </div>
      )}

      <InlineMessages ref={messagesRef} />

      <DataTable
        value={filtered}
        loading={loading}
        stripedRows
        showGridlines
        emptyMessage="Nenhum convite encontrado"
        style={{ fontSize: 13 }}
      >
        <Column
          field="email"
          header="E-mail"
          sortable
          style={{ minWidth: 200 }}
        />
        <Column
          field="status"
          header="Status"
          sortable
          body={(row) => {
            const meta = statusColors[row.status] || {
              bg: "#f1f5f9",
              color: "#64748b",
              label: row.status,
            };
            return (
              <StatusChip
                label={meta.label}
                background={meta.bg}
                color={meta.color}
              />
            );
          }}
          style={{ minWidth: 120 }}
        />
        <Column
          field="roles"
          header="Roles"
          body={(row) => row.roles.join(", ")}
          style={{ minWidth: 150 }}
        />
        <Column
          field="createdAt"
          header="Criado em"
          sortable
          body={(row) => formatDateTime(row.createdAt)}
          style={{ minWidth: 150 }}
        />
        <Column
          field="expiresAt"
          header="Expira em"
          sortable
          body={(row) => formatDateTime(row.expiresAt)}
          style={{ minWidth: 150 }}
        />
        <Column
          header="Ações"
          body={(row) => (
            <div style={{ display: "flex", gap: 8 }}>
              {row.status === "Pending" && (
                <>
                  <Button
                    variant="primary"
                    onClick={() => void handleAccept(row.id)}
                    disabled={busyId === row.id}
                  >
                    {busyId === row.id ? "..." : "Aceitar"}
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() => void handleReject(row.id)}
                    disabled={busyId === row.id}
                  >
                    {busyId === row.id ? "..." : "Recusar"}
                  </Button>
                </>
              )}
            </div>
          )}
          style={{ minWidth: 150 }}
        />
      </DataTable>
    </div>
  );
}
