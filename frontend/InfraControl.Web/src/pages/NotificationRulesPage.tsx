import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PageHeader } from "@/components/ui/PageHeader";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { Button } from "@/components/ui/Button";
import { mutationErrorMessage } from "@/hooks/queries";
import {
  listNotificationRules,
  saveNotificationRules,
  type NotificationEventRule,
  type SaveNotificationEventRulePayload,
} from "@/services/api/notificationRules";

const CHANNELS = ["Email", "SignalR", "Telegram", "WhatsApp", "Webhook"] as const;

function categoryLabel(category: string): string {
  switch (category) {
    case "Identity": return "Identidade";
    case "Operational": return "Operacional";
    case "PDI": return "PDI";
    case "Tasks": return "Tarefas";
    default: return category;
  }
}

function channelsLabel(channels: string[]): string {
  return channels.length === 0
    ? "Fanout global"
    : channels.join(" + ");
}

/**
 * F42-T003 — Administração → Notificações → Regras por evento.
 * Cada linha do catálogo pode ser silenciada (Enabled=false) ou ter
 * canais próprios (substituem o fanout global). Linha sem canais =
 * fanout global (comportamento padrão). Escrita atrás de
 * `Can level="administer"`; o backend é a autoridade (policy Administer).
 */
export function NotificationRulesPage() {
  const queryClient = useQueryClient();

  const rulesQuery = useQuery({
    queryKey: ["notification-rules"],
    queryFn: listNotificationRules,
  });

  const [draft, setDraft] = useState<NotificationEventRule[]>([]);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    if (rulesQuery.data) setDraft(rulesQuery.data);
  }, [rulesQuery.data]);

  const save = useMutation({
    mutationFn: (payload: SaveNotificationEventRulePayload[]) =>
      saveNotificationRules(payload),
    onSuccess: (saved) => {
      setSaveError(null);
      setNotice("Regras de notificação salvas.");
      queryClient.setQueryData(["notification-rules"], saved);
      setDraft(saved);
    },
    onError: (error) => setSaveError(mutationErrorMessage(error as Error)),
  });

  function toggleEnabled(index: number) {
    setDraft((prev) =>
      prev.map((rule, i) =>
        i === index ? { ...rule, enabled: !rule.enabled } : rule
      )
    );
  }

  function toggleChannel(index: number, channel: string) {
    setDraft((prev) =>
      prev.map((rule, i) => {
        if (i !== index) return rule;
        const channels = rule.channels.includes(channel)
          ? rule.channels.filter((c) => c !== channel)
          : [...rule.channels, channel];
        return { ...rule, channels };
      })
    );
  }

  function submit() {
    setNotice(null);
    const payload: SaveNotificationEventRulePayload[] = draft.map((rule) => ({
      eventType: rule.eventType,
      enabled: rule.enabled,
      channels: rule.channels.length > 0 ? rule.channels : null,
    }));
    save.mutate(payload);
  }

  if (rulesQuery.isLoading) return <LoadingBlock />;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Regras de notificação"
        subtitle="Administração → Notificações · silencie eventos ou escolha canais por tipo de evento"
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {(rulesQuery.error || saveError) && (
        <ErrorBanner
          message={
            saveError ??
            mutationErrorMessage(rulesQuery.error as Error | null) ??
            "Não foi possível falar com o servidor. Tente novamente."
          }
          onRetry={() => {
            setSaveError(null);
            void rulesQuery.refetch();
          }}
        />
      )}

      <div className="uni-panel" style={{ padding: 16 }}>
        <div style={{ fontSize: 13, color: "#6b7280", marginBottom: 12 }}>
          Eventos sem regra usam o fanout global (configurado em Integrações →
          Canais). Desmarcar todos os canais = fanout global. Desligar o evento
          = nenhuma notificação é gerada.
        </div>

        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
          <thead>
            <tr style={{ borderBottom: "1px solid #e5e7eb", textAlign: "left" }}>
              <th style={{ padding: "8px 6px", width: 70 }}>Ativo</th>
              <th style={{ padding: "8px 6px" }}>Evento</th>
              <th style={{ padding: "8px 6px", width: 90 }}>Categoria</th>
              <th style={{ padding: "8px 6px", width: 320 }}>Canais</th>
              <th style={{ padding: "8px 6px", width: 130 }}>Modo</th>
            </tr>
          </thead>
          <tbody>
            {draft.map((rule, index) => (
              <tr
                key={rule.eventType}
                style={{ borderBottom: "1px solid #f3f4f6" }}
              >
                <td style={{ padding: "8px 6px" }}>
                  <input
                    type="checkbox"
                    checked={rule.enabled}
                    onChange={() => toggleEnabled(index)}
                    aria-label={`Ativar ${rule.eventType}`}
                  />
                </td>
                <td style={{ padding: "8px 6px" }}>
                  <div style={{ fontWeight: 600 }}>{rule.eventType}</div>
                  <div style={{ color: "#6b7280", fontSize: 12 }}>
                    {rule.description}
                  </div>
                </td>
                <td style={{ padding: "8px 6px" }}>
                  <span
                    style={{
                      display: "inline-block",
                      padding: "2px 8px",
                      borderRadius: 999,
                      fontSize: 11,
                      fontWeight: 600,
                      background:
                        rule.category === "Operational" ? "#fee2e2" :
                        rule.category === "Tasks" ? "#dbeafe" :
                        "#dcfce7",
                      color:
                        rule.category === "Operational" ? "#b91c1c" :
                        rule.category === "Tasks" ? "#1e40af" :
                        "#166534",
                    }}
                  >
                    {categoryLabel(rule.category)}
                  </span>
                </td>
                <td style={{ padding: "8px 6px" }}>
                  <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                    {CHANNELS.map((channel) => (
                      <label
                        key={channel}
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: 4,
                          fontSize: 12,
                          opacity: rule.enabled ? 1 : 0.5,
                        }}
                      >
                        <input
                          type="checkbox"
                          checked={rule.channels.includes(channel)}
                          disabled={!rule.enabled}
                          onChange={() => toggleChannel(index, channel)}
                          aria-label={`${channel} para ${rule.eventType}`}
                        />
                        {channel}
                      </label>
                    ))}
                  </div>
                </td>
                <td style={{ padding: "8px 6px" }}>
                  <span style={{ fontSize: 12, color: "#6b7280" }}>
                    {rule.enabled ? channelsLabel(rule.channels) : "Silenciado"}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <div style={{ marginTop: 16, display: "flex", justifyContent: "flex-end" }}>
          <Button onClick={submit} disabled={save.isPending}>
            {save.isPending ? "Salvando…" : "Salvar regras"}
          </Button>
        </div>
      </div>
    </div>
  );
}
