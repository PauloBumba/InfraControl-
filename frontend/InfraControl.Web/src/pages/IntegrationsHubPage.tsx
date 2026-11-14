import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { getUnifiConfig } from "@/services/api/unifi";
import { getZabbixConfig } from "@/services/api/zabbix";
import { getWhatsAppSettings } from "@/services/api/whatsapp";
import { getEmailConfig } from "@/services/api/email";

/**
 * Hub de Integrações (F54-extra): `/integrations` era a página de config
 * do UniFi — agora é o índice das 4 integrações, cada uma com status
 * real (fonte banco/env + flag de credencial) e link para a página de
 * configuração que já existia. Falha isolada por card.
 */

interface IntegrationCard {
  key: string;
  title: string;
  description: string;
  configureTo: string;
  extraLink?: { to: string; label: string };
}

const CARDS: IntegrationCard[] = [
  {
    key: "unifi",
    title: "UniFi Network",
    description: "Controlador UniFi — inventário de devices, sites, clientes ao vivo e topologia.",
    configureTo: "/integrations/unifi",
    extraLink: { to: "/unifi/dashboard", label: "Dashboard UniFi" },
  },
  {
    key: "zabbix",
    title: "Zabbix",
    description: "Monitoramento — hosts, problemas, histórico de métricas e disponibilidade.",
    configureTo: "/integrations/zabbix",
    extraLink: { to: "/zabbix", label: "Visão geral Zabbix" },
  },
  {
    key: "whatsapp",
    title: "WhatsApp",
    description: "Notificações via instância WhatsApp (Evolution API) — QR/pairing e estado da conexão.",
    configureTo: "/integrations/whatsapp",
  },
  {
    key: "email",
    title: "Email / SMTP",
    description: "Notificações por e-mail — servidor SMTP, remetente e teste de envio.",
    configureTo: "/integrations/email",
  },
];

type Status =
  | { kind: "loading" }
  | { kind: "ok"; label: string; detail?: string }
  | { kind: "unconfigured" }
  | { kind: "error" };

function sourceLabel(source: string | undefined): string {
  return source === "database" ? "banco" : source === "environment" ? "env" : (source ?? "—");
}

export function IntegrationsHubPage() {
  const [statuses, setStatuses] = useState<Record<string, Status>>({});

  useEffect(() => {
    let alive = true;
    const load = async (key: string, fn: () => Promise<unknown>) => {
      try {
        const cfg = (await fn()) as Record<string, unknown>;
        if (!alive) return;
        const hasCredential = Boolean(
          cfg.passwordSet || cfg.apiKeySet || cfg.host,
        );
        setStatuses((prev) => ({
          ...prev,
          [key]: hasCredential
            ? {
                kind: "ok",
                label: `configurada (${sourceLabel(cfg.source as string | undefined)})`,
                detail: cfg.lastTestOk === false ? "último teste falhou" : undefined,
              }
            : { kind: "unconfigured" },
        }));
      } catch {
        if (alive) setStatuses((prev) => ({ ...prev, [key]: { kind: "error" } }));
      }
    };
    void load("unifi", getUnifiConfig);
    void load("zabbix", getZabbixConfig);
    void load("whatsapp", getWhatsAppSettings);
    void load("email", getEmailConfig);
    return () => {
      alive = false;
    };
  }, []);

  const chipFor = (status: Status | undefined) => {
    if (!status || status.kind === "loading")
      return <StatusChip label="…" background="#f1f5f9" color="#64748b" fontSize={9} />;
    if (status.kind === "ok")
      return (
        <StatusChip
          label={status.label}
          background="#f0fdf4"
          color="#15803d"
          fontSize={9}
          title={status.detail}
        />
      );
    if (status.kind === "unconfigured")
      return <StatusChip label="não configurada" background="#fef3c7" color="#b45309" fontSize={9} />;
    return <StatusChip label="status indisponível" background="#f1f5f9" color="#64748b" fontSize={9} />;
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Integrações"
        subtitle="Conexões com sistemas externos — status e configuração"
      />
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))",
          gap: 12,
        }}
      >
        {CARDS.map((card) => {
          const status = statuses[card.key];
          return (
            <section key={card.key} className="uni-panel" style={{ padding: 16 }}>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  gap: 8,
                  marginBottom: 8,
                }}
              >
                <h2 style={{ fontSize: 14, fontWeight: 700, margin: 0 }}>{card.title}</h2>
                {status?.kind === "loading" || !status ? (
                  <LoadingBlock style={{ padding: 4 }} />
                ) : (
                  chipFor(status)
                )}
              </div>
              <p style={{ fontSize: 12, color: "#64748b", margin: "0 0 12px" }}>
                {card.description}
              </p>
              <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                <Link className="uni-dash-view-all" to={card.configureTo}>
                  Configurar
                </Link>
                {card.extraLink && (
                  <Link className="uni-dash-view-all" to={card.extraLink.to}>
                    {card.extraLink.label}
                  </Link>
                )}
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
}
