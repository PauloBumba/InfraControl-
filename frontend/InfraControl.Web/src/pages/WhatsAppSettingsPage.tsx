import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PageHeader } from "@/components/ui/PageHeader";
import { FormField, FormError } from "@/components/ui/FormField";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { Button } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/StatusChip";
import { mutationErrorMessage } from "@/hooks/queries";
import { formatDateTime } from "@/utils/format";
import {
  createWhatsAppInstance,
  disconnectWhatsAppInstance,
  generateWhatsAppQr,
  getWhatsAppConnectionState,
  getWhatsAppSettings,
  listWhatsAppGroups,
  saveWhatsAppSettings,
  sendWhatsAppTest,
  type WhatsAppGroup,
  type WhatsAppQr,
  type WhatsAppSettings,
  type WhatsAppSettingsPayload,
} from "@/services/api/whatsapp";

const inputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #d1d5db",
  borderRadius: 6,
  width: "100%",
};

const panelStyle: React.CSSProperties = { padding: 16 };

function formatUtc(value: string | null): string {
  return value ? formatDateTime(new Date(value)) : "—";
}

/** Estado de conexão → chip legível (open/connecting/close/error). */
function connectionChip(state: string | null | undefined): {
  label: string;
  background: string;
  color: string;
} {
  switch (state) {
    case "open":
      return { label: "Conectado", background: "#dcfce7", color: "#166534" };
    case "connecting":
      return { label: "Conectando…", background: "#fef9c3", color: "#854d0e" };
    case "close":
      return { label: "Desconectado", background: "#f1f5f9", color: "#64748b" };
    case "error":
      return { label: "Erro", background: "#fee2e2", color: "#b91c1c" };
    default:
      return { label: "—", background: "#f1f5f9", color: "#64748b" };
  }
}

/**
 * ADR-062 — Administração → Integrações → WhatsApp.
 *
 * Gestão do canal dentro da plataforma: conexão (QR + status com poll),
 * configuração persistida no banco (ApiKey write-only, cifrada no servidor),
 * escolha do grupo de destino (fetchAllGroups da Evolution) e teste de envio.
 * Escrita atrás de `administer` (policy do controller); o backend é a
 * autoridade — a UI só reflete e dispara.
 */
export function WhatsAppSettingsPage() {
  const queryClient = useQueryClient();

  const settingsQuery = useQuery({
    queryKey: ["whatsapp-settings"],
    queryFn: getWhatsAppSettings,
  });

  const [baseUrl, setBaseUrl] = useState("");
  const [instanceId, setInstanceId] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [phone, setPhone] = useState("");
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);

  const [saveError, setSaveError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [groups, setGroups] = useState<WhatsAppGroup[] | null>(null);
  const [groupsError, setGroupsError] = useState<string | null>(null);
  const [loadingGroups, setLoadingGroups] = useState(false);

  const [qr, setQr] = useState<WhatsAppQr | null>(null);
  const [qrError, setQrError] = useState<string | null>(null);

  const [testResult, setTestResult] = useState<{ ok: boolean; error: string | null } | null>(null);

  // Preenche o formulário quando a config chega; preserva a seleção de grupo.
  useEffect(() => {
    if (!settingsQuery.data) return;
    setBaseUrl(settingsQuery.data.baseUrl);
    setInstanceId(settingsQuery.data.instanceId);
    setSelectedGroupId(settingsQuery.data.groupId ?? null);
  }, [settingsQuery.data]);

  // Estado de conexão: poll de 5s enquanto não estiver "open" (a chamada é
  // cacheada no servidor por 5s — barata, e reflete o QR recém-gerado).
  const connection = useQuery({
    queryKey: ["whatsapp-connection"],
    queryFn: getWhatsAppConnectionState,
    refetchInterval: (query) => (query.state.data?.state === "open" ? false : 5000),
  });

  const save = useMutation({
    mutationFn: (payload: WhatsAppSettingsPayload) => saveWhatsAppSettings(payload),
    onSuccess: (saved: WhatsAppSettings) => {
      setSaveError(null);
      setNotice("Configuração do WhatsApp salva.");
      setApiKey("");
      setSelectedGroupId(saved.groupId ?? null);
      queryClient.setQueryData(["whatsapp-settings"], saved);
      void queryClient.invalidateQueries({ queryKey: ["whatsapp-connection"] });
    },
    onError: (error) => setSaveError(mutationErrorMessage(error as Error)),
  });

  const connect = useMutation({
    mutationFn: generateWhatsAppQr,
    onSuccess: (result) => {
      setQr(result);
      setQrError(null);
      setNotice("QR gerado — escaneie com o WhatsApp do número conectado.");
      void queryClient.invalidateQueries({ queryKey: ["whatsapp-connection"] });
    },
    onError: (error) => setQrError(mutationErrorMessage(error as Error)),
  });

  const disconnect = useMutation({
    mutationFn: disconnectWhatsAppInstance,
    onSuccess: () => {
      setQr(null);
      setNotice("Instância desconectada.");
      void queryClient.invalidateQueries({ queryKey: ["whatsapp-connection"] });
    },
    onError: (error) => setQrError(mutationErrorMessage(error as Error)),
  });

  const createInstance = useMutation({
    mutationFn: createWhatsAppInstance,
    onSuccess: () => {
      setNotice("Instância criada na Evolution.");
      void queryClient.invalidateQueries({ queryKey: ["whatsapp-connection"] });
    },
    onError: (error) => setQrError(mutationErrorMessage(error as Error)),
  });

  const loadGroups = async () => {
    setLoadingGroups(true);
    setGroupsError(null);
    try {
      const list = await listWhatsAppGroups();
      setGroups(list);
      if (list.length === 0) setGroupsError("Nenhum grupo encontrado nesta instância.");
    } catch (error) {
      setGroupsError(mutationErrorMessage(error as Error));
      setGroups(null);
    } finally {
      setLoadingGroups(false);
    }
  };

  const test = useMutation({
    mutationFn: sendWhatsAppTest,
    onSuccess: (result) => {
      setTestResult(result);
      void queryClient.invalidateQueries({ queryKey: ["whatsapp-settings"] });
    },
    onError: (error) =>
      setTestResult({ ok: false, error: mutationErrorMessage(error as Error) }),
  });

  function submit(event: React.FormEvent) {
    event.preventDefault();
    setNotice(null);
    save.mutate({
      baseUrl: baseUrl.trim(),
      instanceId: instanceId.trim(),
      apiKey: apiKey.trim() === "" ? null : apiKey,
      groupId: selectedGroupId,
      phone: phone.trim() === "" ? null : phone.trim(),
    });
  }

  if (settingsQuery.isLoading) return <LoadingBlock />;

  const settings = settingsQuery.data;
  const state = connection.data?.state ?? settings?.connectionState ?? null;
  const chip = connectionChip(state);
  const qrSrc = qr?.png ? `data:image/png;base64,${qr.png}` : null;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="WhatsApp"
        subtitle="Administração → Integrações · Evolution API (conexão, grupos e destino do canal)"
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {(settingsQuery.error || saveError) && (
        <ErrorBanner
          message={
            saveError ??
            mutationErrorMessage(settingsQuery.error as Error | null) ??
            "Não foi possível falar com o servidor. Tente novamente."
          }
          onRetry={() => {
            setSaveError(null);
            void settingsQuery.refetch();
          }}
        />
      )}

      {/* ── Conexão ─────────────────────────────────────────────── */}
      <div className="uni-panel" style={panelStyle}>
        <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 10 }}>Conexão</div>
        <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap", marginBottom: 12 }}>
          <span style={{ fontSize: 12, color: "#64748b" }}>Estado da instância</span>
          <StatusChip label={chip.label} background={chip.background} color={chip.color} />
          <span style={{ fontSize: 11, color: "#94a3b8" }}>
            {connection.isFetching ? "atualizando…" : "poll a cada 5s"}
          </span>
        </div>

        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <Button
            variant="primary"
            icon="pi pi-qrcode"
            disabled={connect.isPending}
            onClick={() => connect.mutate()}
          >
            {connect.isPending ? "Gerando…" : "Gerar QR Code"}
          </Button>
          <Button
            variant="info"
            icon="pi pi-plus"
            disabled={createInstance.isPending}
            onClick={() => createInstance.mutate()}
          >
            {createInstance.isPending ? "Criando…" : "Criar instância"}
          </Button>
          <Button
            variant="danger"
            icon="pi pi-power-off"
            disabled={disconnect.isPending}
            onClick={() => disconnect.mutate()}
          >
            {disconnect.isPending ? "Desconectando…" : "Desconectar"}
          </Button>
        </div>

        {qrError && <div style={{ marginTop: 10, fontSize: 12, color: "#b91c1c" }}>{qrError}</div>}

        {qrSrc && (
          <div
            role="status"
            style={{
              marginTop: 14,
              display: "flex",
              alignItems: "center",
              gap: 14,
              padding: 12,
              borderRadius: 8,
              background: "#f8fafc",
              border: "1px solid #e2e8f0",
            }}
          >
            <img
              src={qrSrc}
              alt="QR Code de pareamento do WhatsApp"
              width={160}
              height={160}
              style={{ border: "1px solid #e2e8f0", borderRadius: 6, background: "#fff" }}
            />
            <div style={{ fontSize: 12, color: "#475569", lineHeight: 1.6 }}>
              <div>
                <strong>Escaneie</strong> com o WhatsApp do número conectado
                (Aparelhos conectados → Conectar aparelho).
              </div>
              {qr?.pairingCode && (
                <div style={{ marginTop: 4 }}>
                  Código de pareamento: <code>{qr?.pairingCode}</code>
                </div>
              )}
              <div style={{ marginTop: 4, color: "#94a3b8" }}>
                O QR expira em alguns segundos — gere outro se necessário.
              </div>
            </div>
          </div>
        )}
      </div>

      {/* ── Configuração ────────────────────────────────────────── */}
      <div className="uni-panel" style={panelStyle}>
        <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Configuração</div>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
              gap: 12,
            }}
          >
            <FormField label="URL da Evolution API (ex.: http://evolution-api:8080)">
              <input
                value={baseUrl}
                onChange={(event) => setBaseUrl(event.target.value)}
                placeholder="http://evolution-api:8080"
                style={inputStyle}
                autoComplete="off"
              />
            </FormField>
            <FormField label="Nome da instância">
              <input
                value={instanceId}
                onChange={(event) => setInstanceId(event.target.value)}
                placeholder="Admin"
                style={inputStyle}
                autoComplete="off"
              />
            </FormField>
            <FormField
              label={settings?.apiKeySet ? "ApiKey (deixe em branco para manter a atual)" : "ApiKey da Evolution"}
            >
              <input
                type="password"
                value={apiKey}
                onChange={(event) => setApiKey(event.target.value)}
                placeholder={settings?.apiKeySet ? "••••••••" : ""}
                style={inputStyle}
                autoComplete="new-password"
              />
            </FormField>
            <FormField label="Telefone individual (fallback, sem @g.us)">
              <input
                value={phone}
                onChange={(event) => setPhone(event.target.value)}
                placeholder="5511999990000"
                style={inputStyle}
                autoComplete="off"
              />
            </FormField>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <Button
              type="submit"
              variant="primary"
              disabled={save.isPending || baseUrl.trim() === "" || instanceId.trim() === ""}
            >
              {save.isPending ? "Salvando…" : "Salvar"}
            </Button>
            <Button
              type="button"
              variant="info"
              disabled={test.isPending}
              onClick={() => test.mutate()}
            >
              {test.isPending ? "Enviando…" : "Enviar mensagem de teste"}
            </Button>
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              ApiKey cifrada no servidor (AES-GCM) · nunca exibida de volta
            </span>
          </div>

          {saveError && <FormError message={saveError} />}
        </form>
      </div>

      {/* ── Grupos ──────────────────────────────────────────────── */}
      <div className="uni-panel" style={panelStyle}>
        <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 10 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Grupos do número</div>
          <Button
            variant="ghost"
            icon="pi pi-refresh"
            disabled={loadingGroups}
            onClick={() => void loadGroups()}
          >
            {loadingGroups ? "Carregando…" : "Carregar grupos"}
          </Button>
        </div>

        {groupsError && <div style={{ fontSize: 12, color: "#b91c1c", marginBottom: 8 }}>{groupsError}</div>}

        {groups && groups.length === 0 && !groupsError && (
          <div style={{ fontSize: 12, color: "#64748b" }}>Nenhum grupo encontrado.</div>
        )}

        {groups && groups.length > 0 && (
          <div
            role="radiogroup"
            aria-label="Grupos do WhatsApp"
            style={{
              display: "flex",
              flexDirection: "column",
              gap: 6,
              maxHeight: 320,
              overflowY: "auto",
              border: "1px solid #e2e8f0",
              borderRadius: 8,
              padding: 8,
            }}
          >
            {groups.map((group) => {
              const selected = group.id === selectedGroupId;
              return (
                <label
                  key={group.id}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 10,
                    padding: "8px 10px",
                    borderRadius: 6,
                    border: `1px solid ${selected ? "#3b82f6" : "#e2e8f0"}`,
                    background: selected ? "#eff6ff" : "#fff",
                    cursor: "pointer",
                  }}
                >
                  <input
                    type="radio"
                    name="whatsapp-group"
                    checked={selected}
                    onChange={() => setSelectedGroupId(group.id)}
                  />
                  <span style={{ display: "flex", flexDirection: "column", minWidth: 0 }}>
                    <span style={{ fontSize: 13, fontWeight: 600 }}>{group.subject}</span>
                    <span style={{ fontSize: 11, color: "#64748b", wordBreak: "break-all" }}>
                      {group.id} · {group.size} participante(s)
                      {group.restrict || group.announce ? " · restrito" : ""}
                    </span>
                  </span>
                </label>
              );
            })}
          </div>
        )}

        {selectedGroupId && (
          <div style={{ marginTop: 10, fontSize: 12, color: "#166534" }}>
            Destino atual: <code>{selectedGroupId}</code> — grave a configuração para persistir.
          </div>
        )}
      </div>

      {/* ── Estado / teste ──────────────────────────────────────── */}
      <div className="uni-panel" style={panelStyle}>
        <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 10 }}>Estado</div>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
            gap: 10,
            fontSize: 12,
          }}
        >
          <div>
            <div style={{ color: "#64748b" }}>Fonte da configuração</div>
            <div style={{ fontWeight: 500 }}>
              {settings?.source === "database" ? "Banco de dados" : "Variável de ambiente"}
            </div>
          </div>
          <div>
            <div style={{ color: "#64748b" }}>ApiKey configurada</div>
            <div style={{ marginTop: 2 }}>
              {settings?.apiKeySet ? (
                <StatusChip label="Sim" background="#dcfce7" color="#166534" />
              ) : (
                <StatusChip label="Não" background="#f1f5f9" color="#64748b" />
              )}
            </div>
          </div>
          <div>
            <div style={{ color: "#64748b" }}>Grupo de destino</div>
            <div style={{ fontWeight: 500, wordBreak: "break-all" }}>{settings?.groupId ?? "—"}</div>
          </div>
          <div>
            <div style={{ color: "#64748b" }}>Último teste</div>
            <div style={{ fontWeight: 500 }}>
              {settings?.lastTestedAtUtc ? (
                <>
                  {formatUtc(settings.lastTestedAtUtc)}{" "}
                  <StatusChip
                    label={settings.lastTestOk ? "OK" : "Falha"}
                    background={settings.lastTestOk ? "#dcfce7" : "#fee2e2"}
                    color={settings.lastTestOk ? "#166534" : "#b91c1c"}
                  />
                </>
              ) : (
                "—"
              )}
            </div>
          </div>
          <div>
            <div style={{ color: "#64748b" }}>Última atualização</div>
            <div style={{ fontWeight: 500 }}>{formatUtc(settings?.updatedAtUtc ?? null)}</div>
          </div>
        </div>

        {settings?.lastTestError && (
          <div style={{ marginTop: 10, fontSize: 12, color: "#b91c1c" }}>
            Último erro: {settings.lastTestError}
          </div>
        )}

        {testResult && (
          <div
            role="status"
            style={{
              marginTop: 12,
              padding: "10px 12px",
              borderRadius: 6,
              fontSize: 12,
              background: testResult.ok ? "#f0fdf4" : "#fef2f2",
              color: testResult.ok ? "#15803d" : "#b91c1c",
              border: `1px solid ${testResult.ok ? "#bbf7d0" : "#fecaca"}`,
            }}
          >
            {testResult.ok
              ? "Mensagem de teste enviada para o destino configurado."
              : `Falha no teste: ${testResult.error ?? "erro desconhecido"}`}
          </div>
        )}
      </div>
    </div>
  );
}
