import { useEffect, useState, type CSSProperties } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PageHeader } from "@/components/ui/PageHeader";
import { FormField, FormError } from "@/components/ui/FormField";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { Button } from "@/components/ui/Button";
import { SelectField } from "@/components/ui/SelectField";
import {
  createUniFiAssetLink,
  deleteUniFiAssetLink,
  getUniFiCorrelation,
  listUniFiSyncRuns,
} from "@/services/api/unifi";
import { listDevices } from "@/services/api/inventory";
import { StatusChip } from "@/components/ui/StatusChip";
import { mutationErrorMessage } from "@/hooks/queries";
import {
  getUnifiConfig,
  getUnifiSites,
  saveUnifiConfig,
  syncUnifiInventory,
  testUnifiConnection,
  type UniFiSyncResult,
  type UniFiTestResult,
} from "@/services/api/unifi";

/**
 * F26 Fase 2.2 — Administração → Integrações → UniFi.
 * Configuração persistida no InfraControl (senha write-only, cifrada no
 * servidor), teste de conexão e sync do inventário. Escrita atrás de
 * `Can level="administer"` (exibição); o backend continua sendo a
 * autoridade (policy `Administer`).
 */
export function IntegrationsPage() {
  const queryClient = useQueryClient();

  const config = useQuery({
    queryKey: ["unifi-config"],
    queryFn: getUnifiConfig,
  });

  const syncRuns = useQuery({
    queryKey: ["unifi-sync-runs"],
    queryFn: () => listUniFiSyncRuns(30),
    enabled: config.data != null,
  });
  const correlation = useQuery({
    queryKey: ["unifi-correlation"],
    queryFn: getUniFiCorrelation,
    enabled: config.data != null,
  });
  const inventory = useQuery({
    queryKey: ["unifi-correlation-devices"],
    queryFn: async () => (await listDevices({ pageSize: 100 })).items,
    enabled: config.data != null,
  });
  const [manual, setManual] = useState<Record<string, string>>({});

  const unlink = useMutation({
    mutationFn: deleteUniFiAssetLink,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["unifi-correlation"] });
      queryClient.invalidateQueries({ queryKey: ["devices"] });
    },
    onError: (error) => setSyncError(error instanceof Error ? error.message : String(error)),
  });
  const link = useMutation({
    mutationFn: ({ mac, deviceId }: { mac: string; deviceId: string }) =>
      createUniFiAssetLink(mac, deviceId),
    onSuccess: () => {
      setManual({});
      queryClient.invalidateQueries({ queryKey: ["unifi-correlation"] });
      queryClient.invalidateQueries({ queryKey: ["devices"] });
    },
    onError: (error) => setSyncError(error instanceof Error ? error.message : String(error)),
  });

  const sites = useQuery({
    queryKey: ["unifi-sites"],
    queryFn: getUnifiSites,
  });

  const [baseUrl, setBaseUrl] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [site, setSite] = useState("default");
  const [allowInvalidTls, setAllowInvalidTls] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<UniFiTestResult | null>(null);
  const [syncResult, setSyncResult] = useState<UniFiSyncResult | null>(null);
  const [syncError, setSyncError] = useState<string | null>(null);

  useEffect(() => {
    if (!config.data) return;
    setBaseUrl(config.data.baseUrl);
    setUsername(config.data.username);
    setSite(config.data.site);
    setAllowInvalidTls(config.data.allowInvalidTlsCertificate);
  }, [config.data]);

  const save = useMutation({
    mutationFn: () =>
      saveUnifiConfig({
        baseUrl: baseUrl.trim(),
        username: username.trim(),
        password: password.trim() === "" ? null : password,
        site: site.trim() || "default",
        allowInvalidTlsCertificate: allowInvalidTls,
      }),
    onSuccess: (saved) => {
      setSaveError(null);
      setPassword("");
      queryClient.setQueryData(["unifi-config"], saved);
    },
    onError: (error) => setSaveError(mutationErrorMessage(error as Error)),
  });

  const test = useMutation({
    mutationFn: testUnifiConnection,
    onSuccess: setTestResult,
    onError: (error) =>
      setTestResult({
        ok: false,
        devicesCount: 0,
        error: mutationErrorMessage(error as Error),
      }),
  });

  const sync = useMutation({
    mutationFn: syncUnifiInventory,
    onSuccess: (result) => {
      setSyncError(null);
      setSyncResult(result);
    },
    onError: (error) => setSyncError(mutationErrorMessage(error as Error)),
  });

  if (config.isLoading) return <LoadingBlock />;

  const passwordPlaceholder = config.data?.passwordSet
    ? "•••••• (deixe em branco para manter)"
    : "Senha do controlador";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Integrações"
        subtitle="UniFi Network — conexão, teste e sincronização de inventário"
      />

      {config.isError && (
        <ErrorBanner
          message={mutationErrorMessage(config.error) ?? "Não foi possível carregar a configuração."}
          onRetry={() => void config.refetch()}
        />
      )}

      <div className="uni-settings-section" style={{ maxWidth: 640 }}>
        <h3 style={{ display: "flex", alignItems: "center", gap: 8 }}>
          Conexão com o controlador
          {config.data && (
            <StatusChip
              label={config.data.source === "database" ? "configurada (banco)" : "configurada (env)"}
              background={config.data.source === "database" ? "#dcfce7" : "#fef3c7"}
              color={config.data.source === "database" ? "#166534" : "#92400e"}
            />
          )}
        </h3>

        <FormField label="URL do controlador *">
          <input
            type="url"
            value={baseUrl}
            onChange={(e) => setBaseUrl(e.target.value)}
            placeholder="https://172.21.0.1:8443"
            style={inputStyle}
          />
        </FormField>

        <FormField label="Usuário *">
          <input
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="off"
            style={inputStyle}
          />
        </FormField>

        <FormField
          label={`Senha${config.data?.passwordSet ? " (opcional para manter a atual)" : " *"}`}
        >
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="new-password"
            placeholder={passwordPlaceholder}
            style={inputStyle}
          />
        </FormField>

        <FormField label="Site">
          <input
            type="text"
            value={site}
            onChange={(e) => setSite(e.target.value)}
            placeholder="default"
            style={inputStyle}
          />
        </FormField>

        <FormField label=" ">
          <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13, color: "#374151" }}>
            <input
              type="checkbox"
              checked={allowInvalidTls}
              onChange={(e) => setAllowInvalidTls(e.target.checked)}
            />
            Aceitar certificado TLS autoassinado (somente para controlador local)
          </label>
        </FormField>

        <FormError message={saveError ?? ""} />

        <div style={{ display: "flex", gap: 8, marginTop: 8, flexWrap: "wrap" }}>
          <Button icon="pi pi-save" disabled={save.isPending} onClick={() => save.mutate()}>
            Salvar
          </Button>
          <Button
            variant="ghost"
            icon="pi pi-plug"
            disabled={test.isPending}
            onClick={() => test.mutate()}
          >
            Testar conexão
          </Button>
          <Button
            variant="ghost"
            icon="pi pi-refresh"
            disabled={sync.isPending}
            onClick={() => sync.mutate()}
          >
            Sincronizar inventário
          </Button>
        </div>
      </div>

      {testResult && (
        <div className="uni-settings-section" style={{ maxWidth: 640 }}>
          <h3 style={{ display: "flex", alignItems: "center", gap: 8 }}>
            Teste de conexão
            <StatusChip
              label={testResult.ok ? "ok" : "falhou"}
              background={testResult.ok ? "#dcfce7" : "#fee2e2"}
              color={testResult.ok ? "#166534" : "#991b1b"}
            />
          </h3>
          <p style={{ fontSize: 13, color: "#4b5563" }}>
            {testResult.ok
              ? `Controlador respondeu — ${testResult.devicesCount} dispositivo(s) no inventário.` +
                (testResult.version ? ` Versão: ${testResult.version}.` : "")
              : testResult.error ?? "Falha sem detalhe."}
          </p>
        </div>
      )}

      {sites.data && sites.data.length > 0 && (
        <div className="uni-settings-section" style={{ maxWidth: 640 }}>
          <h3>Sites do controller</h3>
          <ul style={{ fontSize: 13, color: "#374151", margin: 0, paddingLeft: 18 }}>
            {sites.data.map((site) => (
              <li key={site.shortId}>
                <strong>{site.name}</strong> ({site.shortId})
                {site.hidden ? " · oculto" : ""}
                {site.missing ? " · ausente no controller" : ""}
                {site.description ? ` — ${site.description}` : ""}
              </li>
            ))}
          </ul>
        </div>
      )}

      {correlation.data && (
        <div className="uni-settings-section" style={{ maxWidth: 720 }}>
          <h3>Correlação com inventário</h3>
          <p style={{ fontSize: 13, color: "#4b5563", margin: "0 0 8px" }}>
            {correlation.data.matched.length} vinculado(s) ·{" "}
            {correlation.data.unlinked.length} sem Asset ·{" "}
            {correlation.data.conflicts.length} conflito(s). Vínculo = MAC ↔ Device
            (remover apaga só o vínculo, nunca o histórico).
          </p>
          {correlation.data.conflicts.length > 0 && (
            <ul style={{ fontSize: 13, color: "#991b1b", margin: "0 0 8px", paddingLeft: 18 }}>
              {correlation.data.conflicts.map((c) => (
                <li key={c.mac}>
                  Conflito: {c.deviceName ?? c.deviceId} — MAC do vínculo ({c.mac}) ≠ MAC do Device
                </li>
              ))}
            </ul>
          )}
          {correlation.data.matched.length > 0 && (
            <ul style={{ fontSize: 13, margin: "0 0 8px", paddingLeft: 18 }}>
              {correlation.data.matched.map((m) => (
                <li key={m.mac}>
                  <a href={`/devices/${m.deviceId}`}>{m.deviceName ?? m.deviceId}</a> ← {m.mac}{" "}
                  {m.controllerPresent ? (
                    <StatusChip label="no controller" background="#f0fdf4" color="#15803d" />
                  ) : (
                    <StatusChip label="fora do controller" background="#f1f5f9" color="#475569" />
                  )}{" "}
                  <Button
                    variant="ghost"
                    disabled={unlink.isPending}
                    onClick={() => unlink.mutate(m.mac)}
                  >
                    Desvincular
                  </Button>
                </li>
              ))}
            </ul>
          )}
          {correlation.data.unlinked.map((u) => (
            <div key={u.mac} style={{ display: "flex", gap: 8, alignItems: "center", marginBottom: 6 }}>
              <span style={{ fontSize: 13, minWidth: 260 }}>
                {u.name ?? u.mac} ({u.mac}){u.type ? ` · ${u.type}` : ""}
              </span>
              <span style={{ minWidth: 220 }}>
                <SelectField
                  value={manual[u.mac] ?? ""}
                  onChange={(value) => setManual((prev) => ({ ...prev, [u.mac]: value }))}
                  options={(inventory.data ?? [])
                    .filter((d) => !correlation.data.matched.some((m) => m.deviceId === d.id))
                    .map((d) => ({ value: d.id, label: d.name }))}
                  placeholder="Vincular a um Asset…"
                  style={{ width: "100%" }}
                />
              </span>
              <Button
                variant="primary"
                disabled={!manual[u.mac] || link.isPending}
                onClick={() => {
                  const deviceId = manual[u.mac];
                  if (deviceId) link.mutate({ mac: u.mac, deviceId });
                }}
              >
                Vincular
              </Button>
            </div>
          ))}
          {correlation.data.unlinked.length === 0 &&
            correlation.data.matched.length === 0 && (
              <p style={{ fontSize: 13, color: "#6b7280" }}>
                Nenhum device UniFi para correlacionar — execute a sincronização.
              </p>
            )}
        </div>
      )}

      {syncResult && (
        <div className="uni-settings-section" style={{ maxWidth: 640 }}>
          <h3>Última sincronização</h3>
          <p style={{ fontSize: 13, color: "#4b5563" }}>
            {syncResult.created} criado(s) · {syncResult.updated} atualizado(s) ·{" "}
            {syncResult.references} referência(s) · {syncResult.errors} erro(s)
          </p>
        </div>
      )}

      {syncError && <ErrorBanner message={syncError} />}

      {syncRuns.data && syncRuns.data.length > 0 && (
        <div className="uni-settings-section" style={{ maxWidth: 760 }}>
          <h3>Histórico de sincronizações</h3>
          <table style={{ width: "100%", fontSize: 13, borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ textAlign: "left", color: "#64748b" }}>
                <th style={histTh}>Início (UTC)</th>
                <th style={histTh}>Duração</th>
                <th style={histTh}>Resultado</th>
                <th style={histTh}>+/~/ref</th>
                <th style={histTh}>Erros</th>
                <th style={histTh}>Sites</th>
              </tr>
            </thead>
            <tbody>
              {syncRuns.data.map((run) => (
                <tr key={run.id} style={{ borderTop: "1px solid #e5e7eb" }}>
                  <td style={histTd}>{new Date(run.startedAtUtc).toLocaleString("pt-BR")}</td>
                  <td style={histTd}>
                    {Math.max(
                      0,
                      Math.round(
                        (new Date(run.finishedAtUtc).getTime() -
                          new Date(run.startedAtUtc).getTime()) / 1000,
                      ),
                    )}
                    s
                  </td>
                  <td style={histTd}>
                    {run.succeeded ? (
                      <StatusChip label="ok" background="#f0fdf4" color="#15803d" />
                    ) : (
                      <StatusChip label="falhou" background="#fee2e2" color="#991b1b" />
                    )}
                  </td>
                  <td style={histTd}>
                    {run.created}/{run.updated}/{run.references}
                  </td>
                  <td style={histTd}>{run.errors}</td>
                  <td style={histTd}>{run.sites}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {syncRuns.data.some((r) => r.message) && (
            <ul style={{ fontSize: 12, color: "#991b1b", paddingLeft: 18, marginTop: 8 }}>
              {syncRuns.data
                .filter((r) => r.message)
                .slice(0, 3)
                .map((r) => (
                  <li key={r.id}>
                    {new Date(r.startedAtUtc).toLocaleString("pt-BR")}: {r.message}
                  </li>
                ))}
            </ul>
          )}
        </div>
      )}

      {config.data?.updatedAtUtc && (
        <p style={{ fontSize: 12, color: "#6b7280" }}>
          Última alteração da configuração:{" "}
          {new Date(config.data.updatedAtUtc).toLocaleString("pt-BR")}
        </p>
      )}
    </div>
  );
}

const histTh: CSSProperties = { padding: "6px 8px", borderBottom: "1px solid #e5e7eb" };
const histTd: CSSProperties = { padding: "6px 8px" };

const inputStyle: CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #d1d5db",
  borderRadius: 6,
  width: "100%",
};
