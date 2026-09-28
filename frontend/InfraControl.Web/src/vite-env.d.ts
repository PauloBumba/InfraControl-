/// <reference types="vite/client" />

interface ImportMetaEnv {
  /**
   * Client ID público do Google Identity Services (F1-T014).
   * Vazio = o botão do Google não aparece no login.
   * ATENÇÃO: qualquer VITE_* é embutido no bundle — nunca ponha segredos aqui.
   */
  readonly VITE_GOOGLE_CLIENT_ID?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
