export function formatDate(date: Date): string {
  return new Intl.DateTimeFormat("pt-BR").format(date);
}

export function formatDateTime(date: Date): string {
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(date);
}

/**
 * F30-T001 S5a — velocidade da porta do switch (SwitchPortDto chega com o
 * enum serializado como texto: "Speed1G"). O usuário vê o token da ficha
 * (docs/42 §B): "1G". Não observado ⇒ "—" (nunca 0, nunca vazio); valor
 * desconhecido pro front ⇒ cru (nada escondido).
 */
export function formatPortSpeed(speed: string | null | undefined): string {
  if (!speed) return "—";
  return speed.startsWith("Speed") ? speed.slice("Speed".length) : speed;
}

/**
 * F30-T001 S5a — duplex da porta ("Full"/"Half"/"Auto"). Não observado ⇒ "—"
 * (campo ausente no controller nunca vira meio-duplex na tela).
 */
export function formatPortDuplex(duplex: string | null | undefined): string {
  return duplex || "—";
}

/**
 * F30-T001 S5b — uptime do device (`uptimeSeconds` de
 * `GET /inventory/devices/{id}/system-info`). Escala: "1d 1h" ≥ 1 dia, "1h 1m"
 * ≥ 1 hora, "1m" ≥ 1 minuto, "42s" no resto. Sem leitura ou dado absurdo
 * (negativo/NaN) ⇒ "—" — uptime nunca vira 0s inventado.
 */
export function formatUptime(seconds: number | null | undefined): string {
  if (seconds == null || !Number.isFinite(seconds) || seconds < 0) return "—";
  const total = Math.floor(seconds);
  if (total >= 86400) {
    const days = Math.floor(total / 86400);
    const hours = Math.floor((total % 86400) / 3600);
    return hours > 0 ? `${days}d ${hours}h` : `${days}d`;
  }
  if (total >= 3600) {
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }
  if (total >= 60) return `${Math.floor(total / 60)}m`;
  return `${total}s`;
}
