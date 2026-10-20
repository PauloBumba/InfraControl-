/**
 * Tema claro/escuro (pedido do usuário 2026-10-19 — "alterar a cor" =
 * alternar tema claro/escuro no dropdown de perfil).
 *
 * A escolha é uma decisão do NAVEGADOR, não da conta: fica no
 * `localStorage` (chave `infracontrol.theme`) e é aplicada no boot via
 * `initTheme()` antes do React renderizar, para não piscar claro-escuro.
 *
 * O efeito prático é o atributo `data-theme="dark"` no `<html>`: o
 * `[data-theme="dark"]` no styles.css redeclara as variáveis `--uni-*`,
 * então shell, cards, menus popup e páginas que usam as variáveis viram
 * escuros juntos. Páginas com cores fixas embutidas (landing pública,
 * gráficos Chart.js) não acompanham — pendência conhecida, fora do
 * escopo desta peça.
 */

export type Theme = "light" | "dark";

export const THEME_STORAGE_KEY = "infracontrol.theme";

/** Lê a preferência gravada; sem gravação (ou storage bloqueado) = claro. */
export function readStoredTheme(): Theme {
  try {
    return localStorage.getItem(THEME_STORAGE_KEY) === "dark" ? "dark" : "light";
  } catch {
    // localStorage indisponível (modo privado restrito) — segue no padrão.
    return "light";
  }
}

/** Aplica o tema no documento (atributo + `color-scheme` dos controles nativos). */
export function applyTheme(theme: Theme): void {
  const root = document.documentElement;
  if (theme === "dark") root.setAttribute("data-theme", "dark");
  else root.removeAttribute("data-theme");
  root.style.colorScheme = theme;
}

/**
 * Alterna, persiste e aplica. Devolve o tema resultante para o chamador
 * (componente) atualizar o próprio estado — a fonte da verdade do label do
 * menu ("Tema escuro" ↔ "Tema claro") é o estado do componente.
 */
export function toggleTheme(current: Theme): Theme {
  const next: Theme = current === "dark" ? "light" : "dark";
  try {
    localStorage.setItem(THEME_STORAGE_KEY, next);
  } catch {
    // Sem persistência o toggle ainda funciona na sessão corrente.
  }
  applyTheme(next);
  return next;
}

/** No boot: aplica a preferência gravada antes do primeiro render. */
export function initTheme(): void {
  applyTheme(readStoredTheme());
}
