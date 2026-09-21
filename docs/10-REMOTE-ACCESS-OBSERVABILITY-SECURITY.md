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
