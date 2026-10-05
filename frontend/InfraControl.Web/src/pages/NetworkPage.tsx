import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Can } from "@/components/auth/Can";
import { NETWORK_PAGE_SIZE, createVlan, listVlans, type Paged, type VlanItem } from "@/services/api/network";
import { Pagination } from "@/components/ui/Pagination";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { Button } from "@/components/ui/Button";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { PageHeader } from "@/components/ui/PageHeader";
import { FormField, FormError } from "@/components/ui/FormField";


/**
 * F17-T008 — NetworkPage conectada ao backend (substitui `vlans`,
 * `dhcpLeases`, `routes` e `firewallRules` de `data/dashboardData`).
 *
 * Contrato real: `GET/POST /api/v1/network/vlans` (`VlanDto` = nome,
 * número 802.1Q e descrição). Divergências do protótipo: "VLAN {id}" vira
 * **VLAN {vlanNumber}** (o número real); `color`, `devices`, `subnet`,
 * `gateway` e `dhcp` do card não existem no DTO (saíram). As abas DHCP,
 * Rotas e Firewall **foram removidas — não há endpoint no backend** (F3
 * cobre switches/ports/VLANs/conexões/topologia; DHCP/rotas/firewall não
 * têm slice). A vinculação N:N de portas (SwitchPortVlan) também não tem
 * UI nesta task.
 */

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function NetworkPage() {
  const [pageData, setPageData] = useState<Paged<VlanItem> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [vlanNumber, setVlanNumber] = useState("");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setPageData(await listVlans(page));
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    void load();
  }, [load]);

  async function submitCreate(event: FormEvent) {
    event.preventDefault();
    const trimmedName = name.trim();
    const number = Number(vlanNumber);
    if (!trimmedName) {
      setFormError("Informe o nome da VLAN.");
      return;
    }
    if (!Number.isInteger(number) || number < 1 || number > 4094) {
      setFormError("O número da VLAN deve ser inteiro entre 1 e 4094.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      await createVlan({
        name: trimmedName,
        vlanNumber: number,
        description: description.trim() || undefined,
      });
      setCreating(false);
      setName("");
      setVlanNumber("");
      setDescription("");
      setNotice(`VLAN "${trimmedName}" (${number}) criada.`);
      await load();
    } catch (caught) {
      setFormError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  }

  const vlans = pageData?.items ?? [];
  const totalCount = pageData?.totalCount ?? 0;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Rede"
        subtitle={loading
          ? "Carregando…"
          : `${totalCount} ${totalCount === 1 ? "VLAN" : "VLANs"} cadastradas`}
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
        <form onSubmit={submitCreate} style={{ display: "flex", alignItems: "flex-end", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8, flexWrap: "wrap" }}>
          <FormField label="Nome *" width={180}>
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Ex.: Voz"
              autoFocus
              maxLength={120}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          <FormField label="Número (1–4094) *" width={130}>
            <input
              type="number"
              min={1}
              max={4094}
              value={vlanNumber}
              onChange={(event) => setVlanNumber(event.target.value)}
              placeholder="100"
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          <div style={{ display: "flex", flexDirection: "column", gap: 4, fontSize: 12, color: "#64748b", flex: 1, minWidth: 200 }}>
            Descrição
            <input
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Opcional"
              maxLength={500}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </div>
          <FormError message={formError ?? ""} style={{ alignSelf: "center" }} />
          <div style={{ display: "flex", gap: 8 }}>
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
              {saving ? "Salvando…" : "Criar VLAN"}
            </Button>
          </div>
        </form>
      )}

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 12 }}>
        {vlans.map((vlan) => (
          <div key={vlan.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16 }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <div style={{ width: 10, height: 10, borderRadius: "50%", background: "#3b82f6" }} />
                <div>
                  <div style={{ fontSize: 13, fontWeight: 600 }}>{vlan.name}</div>
                  <div style={{ fontSize: 10, color: "#9ca3af" }}>VLAN {vlan.vlanNumber}</div>
                </div>
              </div>
            </div>
            {vlan.description && (
              <div style={{ fontSize: 11, color: "#6b7280", lineHeight: 1.4 }}>{vlan.description}</div>
            )}
          </div>
        ))}
        {!loading && vlans.length === 0 && (
          <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13, background: "white", borderRadius: 8, border: "1px solid #e5e7eb" }}>
            {error ? "—" : "Nenhuma VLAN cadastrada"}
          </div>
        )}
        {loading && vlans.length === 0 && (
          <LoadingBlock style={{ gridColumn: "1 / -1", background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }} />
        )}
      </div>

      {totalCount > NETWORK_PAGE_SIZE && (
        <Pagination
          page={page}
          totalPages={Math.max(1, Math.ceil(totalCount / NETWORK_PAGE_SIZE))}
          disabled={loading}
          onPage={setPage}
        />
      )}
    </div>
  );
}
