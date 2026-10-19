# Acesso remoto (Guacamole) — operação

Escopo: operação do gateway de acesso remoto (Fase 34 — ADR-060).
Arquitetura e decisões: `docs/adr/ADR-060-ACESSO-REMOTO-GATEWAY.md`.
Inventário e políticas do hospital: `F34-T000` (fora do repo, pendente).

## Subir / desligar o gateway

```bash
docker compose --profile remote up -d      # init → db → guacd → guacamole
docker compose --profile remote stop guacamole guacd guacamole-db
```

Nenhuma porta publicada no host: acesso só via nginx HTTPS
(`/guacamole/`, porta `NGINX_HTTPS_PORT` do `.env` = 8445 no dev).

## Kill switch — desativar TODO o acesso remoto em um passo

```bash
docker compose --profile remote stop guacamole guacd guacamole-db
```

Executado com sucesso em ambiente de teste (F34-T003, 2026-10-18):
HTTPS `/guacamole/` devolve **504** (nginx sem upstream) e religar com
`docker compose --profile remote up -d` devolve **200**. A plataforma
nunca depende do profile `remote` (opt-in, mesmo padrão do `ai`).

## Autenticação e MFA (decisão D2)

- Operador autentica na **plataforma** (MFA TOTP — ADR-019/F1-T016a);
  o gateway recebe header `X-Remote-User` saneado (o nginx limpa o
  header de toda requisição externa — anti-spoofing, docs Guacamole).
- O enforcement do SSO na criação de sessão do gateway é `F34-T007`;
  até lá o login do gateway é local.
- `guacadmin` = conta local de **break-glass** do gateway (não é o
  operador). A senha default (`guacadmin`) foi rotacionada no
  F34-T003 (2026-10-18): `PUT /api/session/data/postgresql/users/
  guacadmin/password` → 204, login antigo rejeitado. Senha nova em
  `.env` (`GUACAMOLE_ADMIN_PASSWORD`, gitignored) — nunca no compose
  nem no git.

## Backup e restore

Banco do gateway (`guacamole-db`) entra no backup do `BackupWorker`
(`Backup:AdditionalTargets`, prefixo `guacamole`). Round-trip de
restore **testado** em 2026-10-18: ver
`docs/operations/BACKUP-RESTORE.md` §"Restauração do banco do
gateway".

## Pendências antes do piloto (F34-T004/T005)

- firewall de **egress do guacd no HOST**: restringir destinos
  permitidos (LAN autorizada) — não expressável em compose;
- `docker-compose.production.yml` sem profile `remote` (deploy real);
- inventário Windows/Linux e políticas (`F34-T000` — usuário).
