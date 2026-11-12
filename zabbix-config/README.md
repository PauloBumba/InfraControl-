# zabbix-config — versionamento da configuracao do Zabbix

Export versionado da configuracao do Zabbix (Sprint 1, docs/65 secao 3.3).
Serve para auditoria de mudanca e rollback leve (o rollback completo de dados
e o dump SQL em `backups/zabbix/`, fora do Git).

## Arquivos

| Arquivo | Conteudo | Gerado por |
| --- | --- | --- |
| `export/hosts.json` | 152 hosts (interfaces, vinculos, macros de host) | `configuration.export` (lotes de 50) |
| `export/templates.json` | 392 templates (9 em uso + 383 stock da imagem) | idem |
| `export/media-types.json` | 42 midia types (email/webhook) | idem |
| `actions.json` | acoes de notificacao (action.get — API nao exporta actions) | idem |

## Regenerar

```powershell
.\scripts\zabbix\backup-zabbix.ps1 -Versioned
```

Requer a API do Zabbix acessivel (`http://localhost:8086`) — credenciais via
env `ZABBIX_API_USER` / `ZABBIX_API_PASSWORD` (padrao Admin/zabbix).

Gotchas da API 8.0 (confirmados ao vivo): auth so via header `Authorization:
Bearer`; `configuration.export` exige arrays e aceita somente
hosts/templates/mediaTypes; export grande estoura o `memory_limit` de 128 MB
do PHP do container — por isso o script exporta em lotes de 50 e mescla.

## Varredura de segredos (OBRIGATORIA antes de cada commit)

```powershell
# senhas literais (nao-macro) e tokens literais:
Select-String -Path zabbix-config\export\*.json,zabbix-config\actions.json `
  -Pattern '"password"\s*:\s*"(?!\{\$)[^"]+"','(?i)(api_?key|secret|token)["\s:=]+[A-Za-z0-9_\-\.\/\+]{20,}'
# comunidades SNMP:
Select-String -Path zabbix-config\export\hosts.json -Pattern '"community"\s*:\s*"([^"]*)"'
```

Critério de bloqueio: valor LITERAL de senha/token que não seja referência de
macro (`{$...}`) nem código de script. Codigo de script e nomes de parametro
(ex.: `AWS.params.SessionToken`) nao sao segredo.

### Resultado da varredura (06/11/2026)

- `media-types.json`: **limpo** (0 senhas; SMTPs sao placeholders; tokens sao script).
- `actions.json`: **limpo** (0 senhas/0 tokens literais).
- `templates.json`: 804 campos `password` = todos macro `{$...}` (0 literais);
  55 "token" = todos código de script (0 literais).
- `hosts.json`: **131 comunidades SNMP** — 96x `public` + 35x `hsds`.
  Credencial fraca (equivalente a senha de leitura). Decisao do usuario
  em 06/11/2026: commitar como esta (repo privado; `public` ja consta na
  auditoria docs/64). **Troca das comunidades exige autorizacao e esta fora
  do Sprint 1** (ver docs/65 — pendencia "SNMP community").

## Historico

- 06/11/2026 — primeira versao commitada ( Sprint 1 P0, docs/68 ).
