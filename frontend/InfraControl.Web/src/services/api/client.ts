import type { ApiResponse } from "@/types/api";
import { store } from "@/app/store";
import { signedOut } from "@/app/store/slices/authSlice";

/**
 * Cliente HTTP único do dashboard (F14-T008).
 *
 * Regras de segurança aplicadas aqui:
 *  - a sessão é o cookie HttpOnly do backend (`credentials: "include"`); o
 *    JavaScript nunca enxerga access token nem refresh token;
 *  - **401 é global**: qualquer chamada que receba 401 derruba o estado local
 *    (`signedOut`), então o SessionGate volta a renderizar o LoginPage na hora,
 *    sem esperar o próximo boot. O backend continua sendo sendo a autoridade —
 *    isto é consistência de UI, não autorização;
 *  - 403 NÃO desloga: falta de permissão é caso de tela/erro, não de sessão;
 *  - **400 de antiforgery não desloga** (F1-T015/ADR-017): é token faltando,
 *    não sessão morta;
 *  - falha de transporte não vira logout (senão uma queda de rede jogaria o
 *    usuário para fora do app) e a mensagem não carrega URL nem credencial;
 *  - nenhum header, body, id_token ou token é logado.
 */

export class ApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isForbidden(): boolean {
    return this.status === 403;
  }

  /** 0 = falha de transporte (não houve resposta do servidor). */
  get isNetworkError(): boolean {
    return this.status === 0;
  }
}

export const GENERIC_ERROR = "Não foi possível falar com o servidor. Tente novamente.";
export const UNAUTHORIZED_ERROR = "E-mail ou senha inválidos.";

type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

const BASE_URL = import.meta.env.VITE_API_BASE_URL || "/api/v1";

/** F1-T015/ADR-017: o backend exige o token de antiforgery em toda escrita. */
const XSRF_COOKIE = "XSRF-TOKEN";
const XSRF_HEADER = "X-XSRF-TOKEN";

/**
 * Lê o cookie de antiforgery. O backend o emite em `GET /auth/me` (o boot do
 * dashboard), sem HttpOnly justamente para o JS conseguir ler — é o que fecha o
 * double-submit: um site cross-site não lê o cookie, então não monta o header.
 *
 * O valor NÃO é logado nem entra em mensagem de erro: é credencial de sessão.
 */
function antiforgeryToken(): string | undefined {
  const prefix = `${XSRF_COOKIE}=`;
  const found = document.cookie.split("; ").find((entry) => entry.startsWith(prefix));
  return found ? decodeURIComponent(found.slice(prefix.length)) : undefined;
}

const WRITE_METHODS: ReadonlySet<Method> = new Set(["POST", "PUT", "PATCH", "DELETE"]);

interface RequestOptions {
  /**
   * Chamadas que respondem 401 legitimamente durante a tela de login: o `GET
   * /auth/me` do boot (o chamador quer saber se é anônimo) e o próprio login
   * recusado (o erro da tela vem do chamador, não do interceptor global).
   */
  anonymousExpected?: boolean;
}

async function request<T>(path: string, method: Method, body?: unknown, options?: RequestOptions): Promise<T | null> {
  let response: Response;

  const headers: Record<string, string> = {};
  if (body !== undefined) headers["Content-Type"] = "application/json";

  // escrita autenticada: o header é obrigatório. Sem token a API responde 400
  // ("A requisição não pôde ser validada") e a ação é perdida — por isso o
  // token é lido aqui e não em cada call site.
  if (WRITE_METHODS.has(method)) {
    const token = antiforgeryToken();
    if (token) headers[XSRF_HEADER] = token;
  }

  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      credentials: "include",
      headers: Object.keys(headers).length ? headers : undefined,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    // Falha de transporte: sem status e sem vazar URL/credencial na mensagem.
    throw new ApiError(GENERIC_ERROR, 0);
  }

  if (response.status === 401 && !options?.anonymousExpected) {
    // Sessão expirada/revogada: limpa o estado global e deixa o SessionGate
    // trocar a tela. Idempotente e barato, mesmo com requisições simultâneas.
    store.dispatch(signedOut());
  }

  if (response.status === 204) return null;

  const payload = (await response.json().catch(() => null)) as ApiResponse<T> | null;

  if (!response.ok || !payload?.isSuccess) {
    const message = payload?.errors?.[0] ?? (response.status === 401 ? UNAUTHORIZED_ERROR : GENERIC_ERROR);
    throw new ApiError(message, response.status);
  }

  return payload.data;
}

export const api = {
  get: <T>(path: string, options?: RequestOptions) => request<T>(path, "GET", undefined, options),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) => request<T>(path, "POST", body, options),
  put: <T>(path: string, body?: unknown) => request<T>(path, "PUT", body),
  patch: <T>(path: string, body?: unknown) => request<T>(path, "PATCH", body),
  delete: <T>(path: string) => request<T>(path, "DELETE"),
};
