import { useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { formatDateTime } from "@/utils/format";
import {
  getScheduledReport,
  upsertScheduledReport,
  type ScheduledReport,
} from "@/services/api/reports";

const WEEKDAYS = [
  "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado",
];

/**
 * Agenda do relatório geral por e-mail (Admin): o admin define a lista de
 * destinatários e a periodicidade; o ScheduledReportWorker envia o
 * relatório a TODOS os e-mails configurados no horário de Brasília agendado.
 */
export function ScheduledReportCard() {
  const [schedule, setSchedule] = useState<ScheduledReport | null>(null);
  const [recipientsText, setRecipientsText] = useState("");
  const [frequency, setFrequency] = useState<"Daily" | "Weekly" | "Monthly">("Daily");
  const [sendTime, setSendTime] = useState("08:00");
  const [dayOfWeek, setDayOfWeek] = useState(1);
  const [dayOfMonth, setDayOfMonth] = useState(1);
  const [isActive, setIsActive] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getScheduledReport()
      .then((data) => {
        if (cancelled) return;
        setSchedule(data);
        setRecipientsText(data.recipients.join("\n"));
        setFrequency(data.frequency);
        setSendTime(data.sendTimeUtc ?? "08:00");
        setDayOfWeek(data.dayOfWeek ?? 1);
        setDayOfMonth(data.dayOfMonth ?? 1);
        setIsActive(data.isActive);
      })
      .catch((caught) => {
        if (!cancelled)
          setError(caught instanceof Error ? caught.message : "Não foi possível carregar a agenda.");
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const save = async () => {
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      const recipients = recipientsText
        .split(/[\n,;]+/)
        .map((s) => s.trim())
        .filter(Boolean);
      const saved = await upsertScheduledReport({
        recipients,
        frequency,
        sendTimeUtc: sendTime,
        dayOfWeek: frequency === "Weekly" ? dayOfWeek : null,
        dayOfMonth: frequency === "Monthly" ? dayOfMonth : null,
        isActive,
      });
      if (saved) {
        setSchedule(saved);
        setNotice("Agenda salva. O próximo envio está programado conforme a configuração.");
      }
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível salvar a agenda.");
    } finally {
      setSaving(false);
    }
  };

  if (loading) return null;

  const inputStyle: React.CSSProperties = {
    padding: "6px 8px",
    fontSize: 13,
    border: "1px solid #e5e7eb",
    borderRadius: 6,
  };
  const labelStyle: React.CSSProperties = {
    display: "flex", flexDirection: "column", gap: 4,
    fontSize: 12, color: "#475569",
  };

  return (
    <div
      style={{
        background: "white",
        border: "1px solid #e5e7eb",
        borderRadius: 8,
        padding: 14,
        marginBottom: 12,
      }}
    >
      <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 4 }}>
        Relatório geral por e-mail
        <span style={{ fontWeight: 400, color: "#94a3b8", marginLeft: 8, fontSize: 12 }}>
          agenda definida pelo admin — todos os destinatários recebem
        </span>
      </div>

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}
      {error && <ErrorBanner message={error} />}
      {schedule?.lastError && (
        <ErrorBanner message={`Último envio com falha: ${schedule.lastError}`} />
      )}

      <div
        style={{
          display: "flex", gap: 12, flexWrap: "wrap", alignItems: "flex-end",
          marginTop: 10,
        }}
      >
        <label style={{ ...labelStyle, flex: "1 1 260px" }}>
          Destinatários (um e-mail por linha)
          <textarea
            value={recipientsText}
            onChange={(e) => setRecipientsText(e.target.value)}
            rows={3}
            placeholder={"admin@empresa.com\nrede@empresa.com"}
            style={{ ...inputStyle, fontFamily: "inherit", resize: "vertical" }}
          />
        </label>

        <label style={labelStyle}>
          Frequência
          <select
            value={frequency}
            onChange={(e) => setFrequency(e.target.value as "Daily" | "Weekly" | "Monthly")}
            style={inputStyle}
          >
            <option value="Daily">Diário</option>
            <option value="Weekly">Semanal</option>
            <option value="Monthly">Mensal</option>
          </select>
        </label>

        {frequency === "Weekly" && (
          <label style={labelStyle}>
            Dia da semana
            <select
              value={dayOfWeek}
              onChange={(e) => setDayOfWeek(Number(e.target.value))}
              style={inputStyle}
            >
              {WEEKDAYS.map((name, i) => (
                <option key={i} value={i}>{name}</option>
              ))}
            </select>
          </label>
        )}

        {frequency === "Monthly" && (
          <label style={labelStyle}>
            Dia do mês
            <input
              type="number"
              min={1}
              max={31}
              value={dayOfMonth}
              onChange={(e) => setDayOfMonth(Math.min(31, Math.max(1, Number(e.target.value) || 1)))}
              style={inputStyle}
            />
          </label>
        )}

        <label style={labelStyle}>
          Horário (Brasília)
          <input
            type="time"
            value={sendTime}
            onChange={(e) => setSendTime(e.target.value)}
            style={inputStyle}
          />
        </label>

        <label style={{ ...labelStyle, flexDirection: "row", alignItems: "center", gap: 6 }}>
          <input
            type="checkbox"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
          />
          Ativo
        </label>

        <Button onClick={() => void save()} disabled={saving}>
          <i className="pi pi-save" /> {saving ? "Salvando…" : "Salvar agenda"}
        </Button>
      </div>

      {schedule && (schedule.nextRunAtUtc || schedule.lastSentAtUtc) && (
        <div style={{ marginTop: 10, fontSize: 12, color: "#64748b" }}>
          {schedule.nextRunAtUtc && (
            <span>Próximo envio: {formatDateTime(new Date(schedule.nextRunAtUtc))} (Brasília)</span>
          )}
          {schedule.lastSentAtUtc && (
            <span style={{ marginLeft: 16 }}>
              Último envio: {formatDateTime(new Date(schedule.lastSentAtUtc))} (Brasília)
            </span>
          )}
        </div>
      )}
    </div>
  );
}
