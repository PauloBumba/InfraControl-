import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
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
  getZabbixConfig,
  getZabbixSummary,
  saveZabbixConfig,
  syncZabbixInventory,
  testZabbixConnection,
  type ZabbixConnectionTestResult,
  type ZabbixSettingsPayload,
  type ZabbixSyncResult,
} from "@/services/api/zabbix";

const inputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #d1d5db",
  borderRadius: 6,
  width: "100%",
};

function formatUtc(value: string | null): string {
  return value ? formatDateTime(new Date(value)) : "—";
}

/**
 * F27 Fases 2.2/3.1 — Administração → Integrações → Zabbix.
 * Configuração persistida no InfraControl (senha write-only, AES-GCM no
 * servidor), teste de conexão (nunca derruba a tela) e estado da última
 * comunicação/sincronização. Escrita atrás de `Can level="administer"`; o
 * backend continua sendo a autoridade (policy `Administer`).
 */
export function ZabbixSettingsPage() {
  const queryClient = useQueryClient();

  const config = useQuery({
    queryKey: ["zabbix-config"],
    queryFn: getZabbixConfig,
  });

  // Resumo operacional (read-model + problemas ao vivo) — falha segregada
  // em problemsAvailable, igual ao ZabbixOverviewPage.
  const summary = useQuery({
    queryKey: ["zabbix-summary"],
    queryFn: getZabbixSummary,
    refetchInterval: 60_000,
  });

  const [apiUrl, setApiUrl] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [saveError, setSaveError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<ZabbixConnectionTestResult | null>(null);

  useEffect(() => {
    if (!config.data) return;
    setApiUrl(config.data.apiUrl);
    setUsername(config.data.username);
  }, [config.data]);

  const save = useMutation({
    mutationFn: (payload: ZabbixSettingsPayload) => saveZabbixConfig(payload),
    onSuccess: (saved) => {
      setSaveError(null);
      setNotice("Configuração do Zabbix salva.");
      setPassword("");
      queryClient.setQueryData(["zabbix-config"], saved);
    },
    onError: (error) => setSaveError(mutationErrorMessage(error as Error)),
  });

  const test = useMutation({
    mutationFn: testZabbixConnection,
    onSuccess: (result) => {
      setTestResult(result);
      void queryClient.invalidateQueries({ queryKey: ["zabbix-config"] });
    },
    onError: (error) =>
      setTestResult({
        ok: false,
        version: null,
        error: mutationErrorMessage(error as Error),
      }),
  });

  const [syncResult, setSyncResult] = useState<ZabbixSyncResult | null>(null);
  const [syncError, setSyncError] = useState<string | null>(null);

  const sync = useMutation({
    mutationFn: syncZabbixInventory,
    onSuccess: (result) => {
      setSyncError(null);
      setSyncResult(result);
      void queryClient.invalidateQueries({ queryKey: ["zabbix-config"] });
      void queryClient.invalidateQueries({ queryKey: ["zabbix-summary"] });
    },
    onError: (error) => {
      setSyncResult(null);
      setSyncError(mutationErrorMessage(error as Error));
    },
  });

  function submit(event: React.FormEvent) {
    event.preventDefault();
    setNotice(null);
    save.mutate({
      apiUrl: apiUrl.trim(),
      username: username.trim(),
      password: password.trim() === "" ? null : password,
    });
  }

  if (config.isLoading) return <LoadingBlock />;

  const settings = config.data;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Zabbix"
        subtitle="Administração → Integrações · API JSON-RPC (coleta, triggers e problemas ficam no Zabbix)"
        actions={
          <Link to="/zabbix/hosts" style={{ fontSize: 12, color: "#3b82f6" }}>
            Hosts monitorados →
          </Link>
        }
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {(config.error || saveError) && (
        <ErrorBanner
          message={
            saveError ??
            mutationErrorMessage(config.error as Error | null) ??
            "Não foi possível falar com o servidor. Tente novamente."
          }
          onRetry={() => {
            setSaveError(null);
            void config.refetch();
          }}
        />
      )}

      <div className="uni-panel" style={{ padding: 16 }}>
        <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Conexão com a API</div>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
              gap: 12,
            }}
          >
            <FormField label="URL da API (ex.: http://zabbix:8086/api_jsonrpc.php)">
              <input
                value={apiUrl}
                onChange={(event) => setApiUrl(event.target.value)}
                placeholder="http://zabbix:8086/api_jsonrpc.php"
                style={inputStyle}
                autoComplete="off"
              />
            </FormField>
            <FormField label="Usuário da API">
              <input
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                placeholder="Admin"
                style={inputStyle}
                autoComplete="off"
              />
            </FormField>
            <FormField
              label={
                settings?.passwordSet
                  ? "Senha (deixe em branco para manter a atual)"
                  : "Senha da API"
              }
            >
              <input
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                placeholder={settings?.passwordSet ? "••••••••" : ""}
                style={inputStyle}
                autoComplete="new-password"
              />
            </FormField>
          </div>

          <div className="flex align-items-center gap-2 flex-wrap">
            <Button
              type="submit"
              variant="primary"
              disabled={save.isPending || apiUrl.trim() === "" || username.trim() === ""}
              style={{ padding: "6px 14px" }}
            >
              {save.isPending ? "Salvando…" : "Salvar"}
            </Button>
            <Button
              type="button"
              variant="info"
              disabled={test.isPending || settings === undefined}
              style={{ padding: "6px 14px" }}
              onClick={() => test.mutate()}
            >
              {test.isPending ? "Testando…" : "Testar conexão"}
            </Button>
            <Button
              type="button"
              variant="primary"
              disabled={sync.isPending}
              style={{ padding: "6px 14px" }}
              onClick={() => {
                setSyncError(null);
                sync.mutate();
              }}
            >
              {sync.isPending ? "Sincronizando…" : "Sincronizar inventário"}
            </Button>
            <span style={{ fontSize: 11, color: "#94a3b8" }}>
              Credencial cifrada no servidor (AES-GCM) · nunca exibida de volta
            </span>
          </div>

          {saveError && <FormError message={saveError} />}
        </form>
      </div>

      <div className="uni-panel" style={{ padding: 16 }}>
        <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 10 }}>Estado</div>
        {test.isPending && !testResult && (
          <div style={{ fontSize: 12, color: "#64748b" }}>Testando conexão…</div>
        )}
        {testResult && (
          <div
            role="status"
            style={{
              padding: "10px 12px",
              borderRadius: 6,
              marginBottom: 10,
              fontSize: 12,
              background: testResult.ok ? "#f0fdf4" : "#fef2f2",
              color: testResult.ok ? "#15803d" : "#b91c1c",
              border: `1px solid ${testResult.ok ? "#bbf7d0" : "#fecaca"}`,
            }}
          >
            {testResult.ok
              ? `Conexão OK · versão ${testResult.version ?? "desconhecida"}`
              : `Falha: ${testResult.error ?? "erro desconhecido"}`}
          </div>
        )}
        {syncResult && (
          <div
            role="status"
            style={{
              padding: "10px 12px",
              borderRadius: 6,
              marginBottom: 10,
              fontSize: 12,
              background: syncResult.errors > 0 ? "#fffbeb" : "#f0fdf4",
              color: syncResult.errors > 0 ? "#b45309" : "#15803d",
              border: `1px solid ${syncResult.errors > 0 ? "#fde68a" : "#bbf7d0"}`,
            }}
          >
            Sincronização concluída · criados {syncResult.created} · atualizados{" "}
            {syncResult.updated} · ausentes {syncResult.missing} · erros {syncResult.errors}
            {syncResult.version ? ` · versão ${syncResult.version}` : ""}
          </div>
        )}
        {syncError && (
          <div
            role="alert"
            style={{
              padding: "10px 12px",
              borderRadius: 6,
              marginBottom: 10,
              fontSize: 12,
              background: "#fef2f2",
              color: "#b91c1c",
              border: "1px solid #fecaca",
            }}
          >
            Falha na sincronização: {syncError}
          </div>
        )}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
            gap: 10,
            fontSize: 12,
          }}
        >
          <div>
            <div style={{ color: "#64748b" }}>Versão detectada</div>
            <div style={{ fontWeight: 500 }}>{settings?.detectedVersion ?? "—"}</div>
          </div>
          <div>
            <div style={{ color: "#64748b" }}>Fonte da configuração</div>
            <div style={{ fontWeight: 500 }}>
              {settings?.source === "database" ? "Banco de dados" : "Variável de ambiente"}
            </div>
          </div>
          <div>
            <div style={{ color: "#64748b" }}>Senha configurada</div>
            <div style={{ marginTop: 2 }}>
              {settings?.passwordSet ? (
                <StatusChip label="Sim" background="#dcfce7" color="#166534" />
              ) : (
                <StatusChip label="Não" background="#f1f5f9" color="#64748b" />
              )}
            </div>
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
            <div style={{ color: "#64748b" }}>Última comunicação</div>
            <div style={{ fontWeight: 500 }}>{formatUtc(settings?.lastCommunicationAtUtc ?? null)}</div>
          </div>
          <div>
            <div style={{ color: "#64748b" }}>Última sincronização de hosts</div>
            <div style={{ fontWeight: 500 }}>{formatUtc(settings?.lastInventorySyncAtUtc ?? null)}</div>
          </div>
        </div>
        {settings?.lastTestError && (
          <div style={{ marginTop: 10, fontSize: 12, color: "#b91c1c" }}>
            Último erro: {settings.lastTestError}
          </div>
        )}
      </div>

      <div className="uni-panel" style={{ padding: 16 }}>
        <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 10 }}>
          Inventário sincronizado
        </div>
        {summary.isLoading && (
          <div style={{ fontSize: 12, color: "#64748b" }}>Carregando resumo…</div>
        )}
        {summary.error && (
          <div style={{ fontSize: 12, color: "#b91c1c" }}>
            Não foi possível carregar o resumo:{" "}
            {mutationErrorMessage(summary.error) ?? "erro desconhecido"}
          </div>
        )}
        {summary.data && (
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(150px, 1fr))",
              gap: 10,
              fontSize: 12,
            }}
          >
            <div>
              <div style={{ color: "#64748b" }}>Hosts no Zabbix</div>
              <div style={{ fontWeight: 600, fontSize: 18 }}>{summary.data.totalHosts}</div>
            </div>
            <div>
              <div style={{ color: "#64748b" }}>Habilitados</div>
              <div style={{ fontWeight: 600, fontSize: 18 }}>{summary.data.enabledHosts}</div>
            </div>
            <div>
              <div style={{ color: "#64748b" }}>Ausentes</div>
              <div style={{ fontWeight: 600, fontSize: 18 }}>{summary.data.missingHosts}</div>
            </div>
            <div>
              <div style={{ color: "#64748b" }}>Com asset vinculado</div>
              <div style={{ fontWeight: 600, fontSize: 18 }}>{summary.data.hostsWithAsset}</div>
            </div>
            <div>
              <div style={{ color: "#64748b" }}>Sem asset</div>
              <div style={{ fontWeight: 600, fontSize: 18 }}>{summary.data.hostsWithoutAsset}</div>
            </div>
            <div>
              <div style={{ color: "#64748b" }}>Problemas ativos</div>
              <div style={{ fontWeight: 600, fontSize: 18 }}>
                {summary.data.problemsAvailable ? summary.data.problemsTotal : "—"}
              </div>
            </div>
          </div>
        )}
        {summary.data && !summary.data.problemsAvailable && (
          <div style={{ marginTop: 8, fontSize: 11, color: "#b45309" }}>
            Contagem de problemas indisponível
            {summary.data.problemsError ? `: ${summary.data.problemsError}` : ""}
          </div>
        )}
        {summary.data && (
          <div style={{ marginTop: 10, fontSize: 11, color: "#94a3b8" }}>
            Última sincronização:{" "}
            {formatUtc(summary.data.lastInventorySyncAtUtc)} · detalhe em{" "}
            <Link to="/zabbix/hosts" style={{ color: "#3b82f6" }}>
              Hosts monitorados
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}
