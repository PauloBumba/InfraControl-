import { useEffect } from "react";
import { getAccountSettings } from "@/services/api/settings";

/**
 * F44-T003 (prompt master §8): polling automático quando o usuário tem
 * `autoRefresh` ligado nas configurações (`GET /account/settings` — o
 * toggle já existia mas ninguém consumia). `autoRefresh: false` ou falha
 * ao ler as settings = sem polling (comportamento atual de load único).
 */
export function useAutoRefresh(
  load: () => void,
  intervalMs = 30_000,
): void {
  useEffect(() => {
    let timer: ReturnType<typeof setInterval> | null = null;
    let cancelled = false;

    void (async () => {
      try {
        const settings = await getAccountSettings();
        if (cancelled || !settings.autoRefresh) return;
        timer = setInterval(load, intervalMs);
      } catch {
        // settings indisponíveis (sem sessão/erro) — mantém load único
      }
    })();

    return () => {
      cancelled = true;
      if (timer) clearInterval(timer);
    };
  }, [load, intervalMs]);
}
