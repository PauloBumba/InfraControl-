/**
 * F50-T009 — e-mail de suporte opcional da instância (fonte única).
 *
 * Mesmo padrão opcional do `VITE_GOOGLE_CLIENT_ID`: sem valor no build a
 * página de contato e o rodapé dizem que a instância é self-hosted em vez
 * de exibir um endereço inventado.
 */
export function getSupportEmail(): string {
  return (import.meta.env.VITE_SUPPORT_EMAIL ?? "").trim();
}
