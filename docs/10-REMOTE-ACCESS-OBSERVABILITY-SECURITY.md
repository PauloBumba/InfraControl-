# Remote Access, Observability e Security

## Remote Access

```text
React
→ ASP.NET Core
→ Remote Access Manager
→ Provider
→ SSH / RDP / Agent
```

Permissões:

- device.remote.read
- device.ssh.connect
- device.rdp.connect
- device.remote.session
- credential.use
- credential.manage

Nunca expor credenciais ao frontend.

Registrar RemoteSession e auditoria.

Preferir VPN/rede interna.

## OpenTelemetry

Instrumentar:

- ASP.NET Core;
- PostgreSQL;
- Redis;
- RabbitMQ;
- HTTP;
- Workers;
- Discovery;
- Monitoring;
- Notification;
- Polly.

Coletar traces, metrics e logs.

## Segurança

- RBAC
- validação
- sanitização
- parameterized queries
- secrets management
- rate limiting
- auditoria
- HTTPS
- menor privilégio
- LGPD
- proteção contra prompt injection

## Implementação (2026-09-24)

### F11-T001 — RemoteSession (fundação)

- Fluxo parcial (F11-T002/T003 vão completar):

```text
React → ASP.NET Core → (Provider SSH/RDP — F11-T002/T003) → RemoteSession
                                                    └→ GET histórico (F11-T001)
```

- `RemoteSession` (Domain/RemoteAccess): `Open/Close/Fail` com invariantes
  em pt-BR; nunca contém credencial.
- Permissão `Permission.RemoteSessionsRead` (= `device.remote.read`):
  Admin herda; User/Viewer não recebem até a matriz de F11-T004.
- `GET /api/v1/inventory/devices/{id}/remote/sessions` (`[Authorize(Roles="Admin")]`):
  ordem mais-recente-primeiro; `RemoteSessionDto` sem campos de credencial
  (teste de reflexão).
- Tabela `remote_sessions` (FK device cascade, índice
  `(device_id, started_at)`), migração `AddRemoteSessions`.

### F11-T002 — SSH Provider

- Fluxo completado:

```text
React → POST open → OpenSshSessionCommand → IRemoteCredentialSource
                                       → IRemoteProvider.ConnectAsync (SSH.NET)
                                       → RemoteSession (Active) + conexão registrada
        DELETE close → CloseSshSessionCommand → session.Close() + dispose conexão
```

- `IRemoteProvider`/`IRemoteCredentialSource`/`IRemoteConnectionRegistry`
  (Application/RemoteAccess/Sessions/Ports) — RDP entra em F11-T003 pelo
  mesmo port.
- Permissão `Permission.RemoteSshConnect` (= `device.ssh.connect`): Admin
  herda; User/Viewer não recebem até a matriz de F11-T004.
- `POST/DELETE /api/v1/inventory/devices/{id}/remote/sessions`
  (`[Authorize(Roles="Admin")]`): sem terminal interativo — só conexão
  autenticada registrada na sessão.
- Credencial provisória via `RemoteAccess:Ssh` (config/env, **vazia no
  git**); sem credencial, `InvalidOperationException` → 409 pt-BR sem
  criar sessão. F11-T006 substitui por Credential Store.
- Falha de conexão grava `RemoteSession` `Failed` com `FailureReason`
  (≤ 500, senha mascarada) e devolve 409 — tentativa fica auditável.
- Nenhum teste abre rede: integration usa `FakeSshRemoteProvider`.
