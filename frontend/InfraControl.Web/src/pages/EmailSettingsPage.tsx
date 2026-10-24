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
  getEmailConfig,
  saveEmailConfig,
  testEmail,
  type EmailSettingsPayload,
  type EmailTestResult,
} from "@/services/api/email";

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
 * F41-T003 — Administração → Integrações → Email (SMTP).
 * Configuração persistida no InfraControl (senha write-only, AES-GCM no
 * servidor), teste de envio (nunca derruba a tela) e estado do último
 * teste. A partir do momento em que o admin salva e o teste passa, os
 * gatilhos de e-mail (convite, recuperar senha, notificações) passam a
 * enviar de verdade. Escrita atrás de `Can level="administer"`; o backend
 * continua sendo a autoridade (policy `Administer`).
 */
export function EmailSettingsPage() {
  const queryClient = useQueryClient();

  const config = useQuery({
    queryKey: ["email-config"],
    queryFn: getEmailConfig,
  });

  const [host, setHost] = useState("");
  const [port, setPort] = useState(587);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [from, setFrom] = useState("");
  const [fromName, setFromName] = useState("");
  const [useSsl, setUseSsl] = useState(true);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<EmailTestResult | null>(null);

  useEffect(() => {
    if (!config.data) return;
    setHost(config.data.host);
    setPort(config.data.port);
    setUsername(config.data.username);
    setFrom(config.data.from);
    setFromName(config.data.fromName ?? "");
    setUseSsl(config.data.useSsl);
  }, [config.data]);

  const save = useMutation({
    mutationFn: (payload: EmailSettingsPayload) => saveEmailConfig(payload),
    onSuccess: (saved) => {
      setSaveError(null);
      setNotice("Configuração de e-mail salva.");
      setPassword("");
      queryClient.setQueryData(["email-config"], saved);
    },
    onError: (error) => setSaveError(mutationErrorMessage(error as Error)),
  });

  const test = useMutation({
    mutationFn: testEmail,
    onSuccess: (result) => {
      setTestResult(result);
      void queryClient.invalidateQueries({ queryKey: ["email-config"] });
    },
    onError: (error) =>
      setTestResult({
        ok: false,
        error: mutationErrorMessage(error as Error),
      }),
  });

  function submit(event: React.FormEvent) {
    event.preventDefault();
    setNotice(null);
    save.mutate({
      host: host.trim(),
      port,
      username: username.trim(),
      password: password.trim() === "" ? null : password,
      from: from.trim(),
      fromName: fromName.trim() === "" ? null : fromName.trim(),
      useSsl,
      notificationsEnabled: config.data?.notificationsEnabled ?? null,
    });
  }

  if (config.isLoading) return <LoadingBlock />;

  const settings = config.data;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="E-mail (SMTP)"
        subtitle="Administração → Integrações · canal de e-mail para convites, recuperação de senha e notificações"
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
          <div style={{ fontSize: 13, fontWeight: 600 }}>Servidor SMTP</div>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
              gap: 12,
            }}
          >
            <FormField label="Host SMTP (ex.: smtp.gmail.com)">
              <input
                value={host}
                onChange={(event) => setHost(event.target.value)}
                placeholder="smtp.example.com"
                style={inputStyle}
                autoComplete="off"
              />
            </FormField>
            <FormField label="Porta">
              <input
                type="number"
                value={port}
                onChange={(event) => setPort(Number(event.target.value))}
                min={1}
                max={65535}
                style={inputStyle}
              />
            </FormField>
            <FormField label="Usuário (opcional)">
              <input
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                placeholder="smtp-user"
                style={inputStyle}
                autoComplete="off"
              />
            </FormField>
            <FormField
              label={
                settings?.passwordSet
                  ? "Senha (deixe em branco para manter a atual)"
                  : "Senha SMTP"
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
            <FormField label="Remetente (From)">
              <input
                value={from}
                onChange={(event) => setFrom(event.target.value)}
                placeholder="noreply@example.com"
                style={inputStyle}
                autoComplete="off"
              />
            </FormField>
            <FormField label="Nome do remetente (opcional)">
              <input
                value={fromName}
                onChange={(event) => setFromName(event.target.value)}
                placeholder="InfraControl"
                style={inputStyle}
                autoComplete="off"
              />
            </FormField>
          </div>

          <label
            style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13, cursor: "pointer" }}
          >
            <input
              type="checkbox"
              checked={useSsl}
              onChange={(event) => setUseSsl(event.target.checked)}
            />
            Usar TLS/SSL (STARTTLS)
          </label>

          <div className="flex align-items-center gap-2 flex-wrap">
            <Button
              type="submit"
              variant="primary"
              disabled={save.isPending || host.trim() === "" || from.trim() === ""}
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
              {test.isPending ? "Testando…" : "Testar envio"}
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
          <div style={{ fontSize: 12, color: "#64748b" }}>Enviando e-mail de teste…</div>
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
              ? "E-mail de teste enviado com sucesso — confira a caixa de entrada do remetente."
              : `Falha: ${testResult.error ?? "erro desconhecido"}`}
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
            <div style={{ color: "#64748b" }}>Notificações por e-mail</div>
            <div style={{ fontWeight: 500 }}>
              {settings?.notificationsEnabled == null
                ? "Padrão do fanout"
                : settings.notificationsEnabled
                  ? "Ativadas"
                  : "Desativadas"}
            </div>
          </div>
        </div>
        {settings?.lastTestError && (
          <div style={{ marginTop: 10, fontSize: 12, color: "#b91c1c" }}>
            Último erro: {settings.lastTestError}
          </div>
        )}
      </div>
    </div>
  );
}
