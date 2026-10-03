/**
 * Identificador monoespaçado com clique-para-copiar (Fase B do levantamento
 * docs/28): padrão que nasceu inline na AuditPage (Correlation ID) e se repete
 * em Sessions/DeviceDetail. Auto-contido: guarda o estado de "copiado!" com
 * timeout de 1,5 s. `length` controla quantos caracteres são mostrados antes
 * do "…" (o valor completo fica no `title`). Valor nulo/vazio exibe "—".
 */
import { useState } from "react";

export function CopyableId({
  value,
  length = 12,
}: {
  value?: string | null;
  length?: number;
}) {
  const [copied, setCopied] = useState(false);

  if (!value) return <span style={{ color: "#94a3b8" }}>—</span>;

  return (
    <span
      style={{
        fontFamily: "monospace",
        fontSize: 11,
        color: "#94a3b8",
        cursor: "pointer",
        display: "inline-flex",
        alignItems: "center",
        gap: 6,
      }}
      title={`${value} — clique para copiar`}
      onClick={() => {
        void navigator.clipboard?.writeText(value).then(() => {
          setCopied(true);
          window.setTimeout(() => setCopied(false), 1500);
        });
      }}
    >
      <i
        className={`pi ${copied ? "pi-check" : "pi-copy"}`}
        style={{ fontSize: 10, color: copied ? "#16a34a" : undefined }}
      />
      {copied ? "copiado!" : `${value.slice(0, length)}…`}
    </span>
  );
}
