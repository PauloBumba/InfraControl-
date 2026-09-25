# Remote Access, Observability e Security

## Remote Access

```text
React
→ ASP.NET Core
→ Remote Access Manager
→ Provider
→ SSH / RDP / Agent
```

Permissões (matriz em F11-T004):

- device.remote.read (= `RemoteSessionsRead`; Admin/User/Viewer)
- device.ssh.connect (= `RemoteSshConnect`; Admin/User)
- device.rdp.connect (= `RemoteRdpConnect`; Admin/User)
- credential.use (= `CredentialUse`; Admin-only, consumo em F11-T006)
- credential.manage (= `CredentialManage`; Admin-only, consumo em F11-T006)

`device.remote.session` (lista original) é redundante com
ssh/rdp.connect e não entrou no catálogo.

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

- Fluxo (SSH F11-T002 e RDP F11-T003 entregues):

```text
React → ASP.NET Core → Provider SSH (F11-T002) / RDP (F11-T003) → RemoteSession
                                                    └→ GET histórico (F11-T001)
```

- `RemoteSession` (Domain/RemoteAccess): `Open/Close/Fail` com invariantes
  em pt-BR; nunca contém credencial.
- Permissão `Permission.RemoteSessionsRead` (= `device.remote.read`):
  Admin/User/Viewer (matriz F11-T004).
- `GET /api/v1/inventory/devices/{id}/remote/sessions`
  (`[Authorize(Roles="Admin,User,Viewer")]`):
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
        DELETE close → CloseRemoteSessionCommand (renomeado em F11-T003,
                       era CloseSshSessionCommand — é protocolo-agnóstico)
                       → session.Close() + dispose conexão
```

- `IRemoteProvider`/`IRemoteCredentialSource`/`IRemoteConnectionRegistry`
  (Application/RemoteAccess/Sessions/Ports) — RDP entra em F11-T003 pelo
  mesmo port.
- Permissão `Permission.RemoteSshConnect` (= `device.ssh.connect`): Admin
  e User; Viewer não (matriz F11-T004).
- `POST/DELETE /api/v1/inventory/devices/{id}/remote/sessions`
  (`[Authorize(Roles="Admin,User")]`): sem terminal interativo — só conexão
  autenticada registrada na sessão.
- Credencial provisória via `RemoteAccess:Ssh` (config/env, **vazia no
  git**); sem credencial, `InvalidOperationException` → 409 pt-BR sem
  criar sessão. F11-T006 substitui por Credential Store.
- Falha de conexão grava `RemoteSession` `Failed` com `FailureReason`
  (≤ 500, senha mascarada) e devolve 409 — tentativa fica auditável.
- Nenhum teste abre rede: integration usa `FakeSshRemoteProvider`.

### F11-T003 — RDP Provider (probe TCP/X.224)

- Semântica decidida pelo usuário: **probe de transporte** — TCP até a
  porta RDP + negociação X.224 (CR/CC + RDP Negotiation, MS-RDPBCGR) para
  confirmar que o endpoint fala RDP. **Sem autenticação** (CredSSP/NLA fica
  para task futura); o socket fica registrado na sessão até o Close.
- Fluxo:

```text
React → POST .../remote/sessions/rdp → OpenRdpSessionCommand
                                      → IRemoteProvider.ConnectAsync (X.224 probe)
                                      → RemoteSession (Active) + socket registrado
        DELETE .../remote/sessions/{id} → CloseRemoteSessionCommand
                                      → session.Close() + dispose socket
```

- `Permission.RemoteRdpConnect` (= `device.rdp.connect`): Admin e User;
  Viewer não (matriz F11-T004).
- `OpenRdpSessionCommand` **não consulta** `IRemoteCredentialSource` (probe
  não usa credencial); `CloseRemoteSessionCommand` substitui
  `CloseSshSessionCommand` — era protocolo-agnóstico com um único DELETE;
  exige `RemoteSshConnect` + `RemoteRdpConnect` (Admin/User — matriz
  F11-T004).
- `RdpRemoteProvider`: falha vira `RemoteConnectResult` pt-BR sem exceção
  vazando (porta fechada/timeout/tempo esgotado, Neg Failure mapeado —
  ex.: código 4 → "servidor exige autenticação híbrida (NLA)"), resposta
  que não é TPKT → "Endpoint não respondeu como RDP". Config
  `RemoteAccess:Rdp` (`Port`=3389, `TimeoutSeconds`) — sem segredos.
- Falha do probe grava `RemoteSession` `Failed` com `FailureReason`
  (≤ 500) e devolve 409 — tentativa fica auditável (igual a F11-T002).
- Nenhum teste abre rede: unit usa `FakeRdpServer` (`TcpListener` em
  loopback) e integration usa `FakeRdpRemoteProvider`.

### F11-T004 — Matriz de permissões remote (2026-09-25)

- Decisão do usuário: **"Viewer lê, User lê+conecta"** —
  - Viewer: `RemoteSessionsRead` (histórico de sessões);
  - User: permissões do Viewer + `RemoteSshConnect` + `RemoteRdpConnect`;
  - Admin: catálogo completo (herda por `Enum.GetValues`).
- `CredentialUse`/`CredentialManage` entraram no enum `Permission`
  (Admin-only, prontos para F11-T006 Credential Store) — **nenhum command
  os exige hoje**; o consumo real começa quando existir credencial
  armazenada.
- `RemoteSessionsController` sincronizado com a matriz: GET
  `Admin,User,Viewer`; POST / POST rdp / DELETE `Admin,User`.
- `CloseRemoteSessionCommand` continua com RequiredPermissions AND
  (`RemoteSshConnect` + `RemoteRdpConnect`) — com a matriz, User fecha
  sessões dos dois protocolos; Viewer é negado.
- Evidência: `PermissionTests` (matriz por role incluindo os 2 novos
  Credential*), integration Viewer lê histórico (novo), User abre→fecha
  SSH (novo), sem permissão → `UnauthorizedAccessException` pt-BR
  (negative trocou `UseRole(Viewer)` por `UseNoPermissions()` — hoje
  **toda role** tem `RemoteSessionsRead`).

### F11-T005 — Audit de sessões remote (2026-09-25)

- Integração com o audit existente (F1-T010): o `AuditBehavior` já gravava
  uma entrada por request (action = nome do comando, outcome,
  correlationId) — mas **sem ator** e com resource fixo
  "ApplicationRequest".
- `ActorId` agora vem de `ICurrentUserContext.UserName` (decisão do
  usuário: login legível na UI de Audit — antes era sempre null e a UI
  mostrava "sistema"); vale para **todos** os requests, não só remote.
- Novo marker `IAuditableRequest.AuditResource` (BuildingBlocks, ao lado
  de `IAuthorizedRequest`): `AuditBehavior` usa o resource do marker e
  mantém `"ApplicationRequest"` como fallback — uma entrada por request,
  sem duplicar linhas.
- Os 4 requests remote (`OpenSshSessionCommand`/`OpenRdpSessionCommand`/
  `CloseRemoteSessionCommand`/`GetDeviceSessionsQuery`) declaram
  `AuditResource = "RemoteSession"`; a action continua sendo o nome do
  comando — filtro e busca da UI continuam funcionando.
- Falha de conexão/sessão: handler persiste `RemoteSession` `Failed` e o
  `AuditBehavior` grava a entrada `Failed` (error = tipo da exceção) fora
  do caminho do handler — tentativa fica auditável mesmo quando o comando
  lança exceção.
- Evidência: 3 testes em `AuditBehaviorTests` (atores + resource
  genérico, resource "RemoteSession" via marker, path Failed com
  rethrow) + integration Admin open→close grava as duas entries
  `RemoteSession` com `ActorId` "integration-test".
