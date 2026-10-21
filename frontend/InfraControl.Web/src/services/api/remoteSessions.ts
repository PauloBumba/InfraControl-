import { api } from "./client";

/**
 * F17-T012 — contrato real de
 * `/api/v1/inventory/devices/{deviceId}/remote/sessions` (F11, ADR-013).
 *
 * Sessões são **por device** — não existe listagem global (o protótipo
 * tinha tabelas globais de ativas/histórico; saíram). Só leitura +
 * abrir/encerrar: o DTO nunca expõe credenciais (docs/10).
 *
 * Serialização: `protocol`/`status` são enums com `ToString()` no DTO →
 * PascalCase (`Ssh`/`Rdp`, `Active`/`Closed`/`Failed`).
 *
 * Falha de conexão (SSH com credencial, RDP com probe) joga
 * `InvalidOperationException` → **409** com a mensagem pt-BR do handler
 * ("Falha ao abrir sessão SSH em '{device}': {motivo}"); device sem IP →
 * 400 pt-BR; device inexistente → 404.
 */
export interface RemoteSessionItem {
  id: string;
  userId: string;
  protocol: "Ssh" | "Rdp";
  status: "Active" | "Closed" | "Failed";
  failureReason: string | null;
  startedAt: string;
  endedAt: string | null;
}

export interface DeviceSessions {
  deviceId: string;
  deviceName: string;
  sessions: RemoteSessionItem[];
}

/** Sessões do device, mais recentes primeiro (Read; `limit` 1..500). */
export function listDeviceSessions(
  deviceId: string,
  limit = 50,
): Promise<DeviceSessions | null> {
  return api.get<DeviceSessions>(
    `/inventory/devices/${deviceId}/remote/sessions?limit=${limit}`,
  );
}

/** Abre sessão SSH (Operate, credenciais do device) → 201; falha → 409 pt-BR. */
export function openSshSession(deviceId: string): Promise<RemoteSessionItem | null> {
  return api.post<RemoteSessionItem>(`/inventory/devices/${deviceId}/remote/sessions`);
}

/**
 * Abre probe RDP (Operate, TCP/X.224 sem autenticação — NLA/CredSSP é
 * F11-T006+) → 201; endpoint que não fala RDP → 409 pt-BR.
 */
export function openRdpSession(deviceId: string): Promise<RemoteSessionItem | null> {
  return api.post<RemoteSessionItem>(`/inventory/devices/${deviceId}/remote/sessions/rdp`);
}

/** Encerra (Operate, protocolo-agnóstico) → 200; 404 se não existe ou é de outro device. */
export function closeSession(
  deviceId: string,
  sessionId: string,
): Promise<RemoteSessionItem | null> {
  return api.delete<RemoteSessionItem>(
    `/inventory/devices/${deviceId}/remote/sessions/${sessionId}`,
  );
}

/** Sessão mintada no gateway (F34-T007, ADR-060) — o token é de uso único
 * na URL `{path}?token=…` (janela nova); o navegador nunca vê credencial. */
export interface RemoteGatewaySession {
  sessionId: string;
  protocol: "Ssh" | "Rdp";
  token: string;
  path: string;
}

/**
 * Abre sessão pelo gateway Guacamole (Operate) → 201. Falhas pt-BR do
 * backend: device sem IP/SO → 400; sem credencial ou gateway fora → 409.
 */
export function openGatewaySession(
  deviceId: string,
): Promise<RemoteGatewaySession | null> {
  return api.post<RemoteGatewaySession>(
    `/inventory/devices/${deviceId}/remote/sessions/gateway`,
  );
}

/** Estado da credencial por device (F11-T006; Administer) — senha nunca aparece. */
export interface DeviceCredentialState {
  deviceId: string;
  username: string;
  hasCredential: boolean;
  rotatedAt: string | null;
  updatedAt: string;
}

/** Lê o estado (username/flags) — Administer; sem credencial → fallback global. */
export function getDeviceCredential(
  deviceId: string,
): Promise<DeviceCredentialState | null> {
  return api.get<DeviceCredentialState>(
    `/inventory/devices/${deviceId}/remote/credential`,
  );
}

/** Cadastra/rotaciona a credencial do device (Administer) — senha só no corpo. */
export function putDeviceCredential(
  deviceId: string,
  username: string,
  password: string,
): Promise<DeviceCredentialState | null> {
  return api.put<DeviceCredentialState>(
    `/inventory/devices/${deviceId}/remote/credential`,
    { username, password },
  );
}

/** Remove a credencial — o acesso volta ao fallback global (Administer). */
export function deleteDeviceCredential(deviceId: string): Promise<unknown> {
  return api.delete(`/inventory/devices/${deviceId}/remote/credential`);
}
