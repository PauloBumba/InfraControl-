/// <reference types="vite/client" />

interface ImportMetaEnv {
  /**
   * Client ID público do Google Identity Services (F1-T014).
   * Vazio = o botão do Google não aparece no login.
   * ATENÇÃO: qualquer VITE_* é embutido no bundle — nunca ponha segredos aqui.
   */
  readonly VITE_GOOGLE_CLIENT_ID?: string;

  /**
   * Base do fetch do app (services/api/client.ts).
   * Vazio/ausente = "/api/v1" (mesma origem atrás do nginx; ADR-022).
   * Só defina outra origem se o CORS estiver liberado no backend.
   */
  readonly VITE_API_BASE_URL?: string;

  /**
   * URL da plataforma de logs (Seq) usada pelo botão da LogsPage.
   * Em produção é o host+porta publicados pelo nginx (SEQ_UI_PORT).
   */
  readonly VITE_LOGS_URL?: string;

  /** Alvo do proxy do Vite em dev (vite.config.ts); default https://localhost:7237. */
  readonly VITE_DEV_API_TARGET?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
