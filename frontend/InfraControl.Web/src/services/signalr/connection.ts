// SignalR connection builder
import * as signalR from "@microsoft/signalr";
import { XSRF_HEADER, antiforgeryToken } from "../api/client";

export function createHubConnection(hubUrl: string): signalR.HubConnection {
  // O negotiate é um POST autenticado pelo cookie de sessão, então passa pelo
  // AntiforgeryCookieMiddleware (ADR-017): sem o header X-XSRF-TOKEN a API
  // responde 400 e a conexão nunca sobe. O token é o mesmo emitido pelo
  // GET /auth/me no boot — sem ele (sessão ainda não aberta) conecta sem o
  // header e o backend responde com o erro padrão em vez de derrubar o build.
  const token = antiforgeryToken();

  return new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, token ? { headers: { [XSRF_HEADER]: token } } : undefined)
    .withAutomaticReconnect()
    .build();
}
