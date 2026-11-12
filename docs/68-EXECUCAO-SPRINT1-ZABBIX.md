# 68 — EXECUCAO SPRINT 1 (P0): BACKUP, SMOKE TEST, CORRECOES DE ALERTA, VERSIONAMENTO

> Status: **executado em 04–06/11/2026 — checkpoint** (decisoes pendentes listadas na secao 7)
> Plano: `docs/65-PRODUCAO-CONTINUA-ZABBIX.md` (Sprint 1)
> Correcoes de alerta aprovadas pelo responsavel em 04/11/2026 (4 itens)

## 1. Backup diario funcional (Item 1 do Sprint 1)

| Evidencia | Resultado |
| --- | --- |
| Execucao completa 04/11 17:22–17:44 | `backups/zabbix/db_20261104_172216.sql.gz` = **0,75 GB**, exit 0 |
| Export de configuracao | `backups/zabbix/config_<ts>/` = hosts.json + templates.json + media-types.json + actions.json |
| Tarefa agendada | Task Scheduler **"InfraControl Zabbix Backup"** — diaria **02:00**, `State=Ready`, proximo exec 07/11 02:00 |
| Retencao | 7 dias (limpa dumps e diretorios `config_*` antigos) |

Scripts (novos neste repo):

- `scripts/zabbix/backup-zabbix.ps1` — login Bearer via `curl.exe`; export em
  **lotes de 50** com mesclagem; `pg_dump` dentro do container (gzip) +
  `docker cp`; retencao 7 dias; modo `-Versioned`.
- `scripts/zabbix/run-backup.cmd` — wrapper da tarefa agendada.
- `scripts/zabbix/smoke-test.ps1` — **6/6 OK** (API login, /ping, action 7,
  media types 105/106, queue=0, pinger busy < 75%).

### Problemas reais encontrados e resolvidos no caminho

1. **HTTP 500 no export combinado** = `memory_limit` de 128 MB do PHP estourava
   em `gatherItems()` (392 templates). Solucao: lotes de 50 + mescla (sem tocar no container).
2. **`Invoke-RestMethod` (PS 5.1) trava/da 500** — todo request grosso via `curl.exe` com payload em arquivo UTF-8 sem BOM.
3. **Bug `$dbName` vs `$DbName`** — PowerShell e case-insensitive: a variavel local
   sobrescrevia o param `$DbName` e o `pg_dump` recebia o nome do arquivo como
   database. Corrigido (`$tmpDumpName`).
4. **Backup de 02/11 era invalido** (`config_20261102_205037.json` = 142 bytes de
   erro de API): nunca existiu rollback. Agora existe.
5. **schtasks trava com "powershell" no `/tr`** (reproduzido: `cmd` OK,
   `powershell` pendura mesmo com caminho completo e 5 min). Solucao: wrapper `.cmd`.

## 2. Correcoes de alertas (4 itens aprovados)

Resultado: **176 → 152 problemas ativos** (base 181 em 04/11).

### 2.1 Wi-Fi dos APs — limiar (CORRIGIDO)

- Gatilhos `Taxa de erros alta` usam `{$IF.ERRORS.WARN:"{#IFNAME}"}`; o fallback
  base `{$IF.ERRORS.WARN}=2` (template `Network Generic Device by SNMP` 10226)
  valia para as interfaces wifi dos **29 APs**.
- Metrica = erros/seg (historico 0–1 em AP saudavel; 4–21 nos APs problemáticos).
- **Criados macros de contexto no template** (nao mexe nas interfaces com fio):
  `{$IF.ERRORS.WARN:"wifi0"}`, `"wifi1ap5"`, `"wifi1ap6"` = **20** (ids 9402–9404).
- Efeito: **35 → 0** problemas `Taxa de erros alta`.

### 2.2 Severidade de alerta de impressora — gatilho (CORRIGIDO)

- Causa: prototipo `Alerta nao critico ou severidade nao mapeada: {#SNMPVALUE}`
  com `expr = item<>1 and item<>3` — dispara para **4 (warning, valor MAPEADO
  pelo RFC 3805)**, que ja tem gatilho proprio de toner/suprimento.
- Fonte unica: prototipo **38062** no template `IC Impressora SNMP` (10887);
  37 copias vinculadas nos hosts + instancias LLD (flags=4, **nao editaveis
  direto**: "Cannot update expression for a discovered trigger").
- Expressao nova (estilo atual, enum RFC 3805 = {1 other, 3 critical, 4/5 warning}):

  ```
  last(/IC Impressora SNMP/printer.alert.severity[{#SNMPINDEX}])<>1 and
  ... <>3 and ... <>4 and ... <>5
  ```

- `triggerprototype.update` na fonte **propagou sozinho** para as 37 copias
  (verificado: 37/37 com 4 clausulas); LLD sincronizou as instancias.
- Efeito: **7 → 0** problemas de severidade; gatilho `Alerta critico (=3)` intacto.
- Atencao: o numero entre chaves `{id}` nas expressoes legadas e resíduo — a
  avaliacao usa o vinculo `selectItems` (confirmado: braces `meraki`/`hyperv`/
  `mariadb` com vinculo correto de impressora, sem erro de trigger).

### 2.3 Itens `printer.alert.severity` invalidos — MONITORADO (sem acao)

- Flapping **assentou**: em 06/11, **0 itens unsupported** de 23; 1 ja estava
  desabilitado (`85903` TOMOGRAFIA_A3, noSuchName) e 1 (`84732`) tambem;
  1 sem coleta por host fora do ar (`84600` AUDITORIA_PS).
- Os IDs mudam porque o LLD `printer.alerts.discovery` tem **`lifetime=1h`**
  (item perdido e apagado em 1h e recriado habilitado) — desabilitar instancia
  nao dura se o indice sumir. Acao: monitorar (docs/66 item 6); se flapping
  voltar, ajustar `lifetime` ou desabilitar a regra LLD nos hosts afetados.

### 2.4 Investigacao ICMP/agent (SOMENTE LEITURA — 14 hosts reais)

Confirmado de 2 fontes (Windows + container do Zabbix) com controle positivo
(`192.168.22.18` ping OK — rede acessivel, os 14 estao mesmo inacessiveis):

| Sev | Host | IP | Observacao |
| --- | --- | --- | --- |
| 4 | Farmacia HSSM | 192.168.50.101 | |
| 4 | AP_Setor1 | 192.168.10.237 | |
| 4 | AP_OPME | 192.168.1.5 | |
| 4 | U6 LR | 172.16.17.53 | |
| 4 | U6_LR | 172.16.17.56 | |
| 4 | USW-Pro-24-PoE-2 | 172.16.17.54 | switch |
| 4 | HSDS_CAM_ENTRADA_ALMOX | 192.168.26.59 | |
| 4 | HSDS_CAM_CANAL_10 | 192.168.26.58 | |
| 4 | HSDS_CAM_CANAL_18 | 192.168.26.198 | |
| 4 | HSDS_CAM_DESCANSO_PS | 192.168.26.14 | |
| 4 | HSDS_CAM_CONTABILIDADE | 192.168.26.11 | |
| 4 | HSDS_HP_AUDITORIA_PS | 192.168.22.105 | |
| 3 | IEAS-Impressoes-Servidor | — | agente Windows |
| 3 | HSDS_SUPTI666 | — | agente Windows (pendencia conhecida) |

**Acao humana necessaria** (alimentacao/energia/configuracao fisica) — fora do
alcance do monitoramento.

## 3. Smoke test

`scripts/zabbix/smoke-test.ps1` — **6/6** em 04/11 (reexecutar apos qualquer
mudanca; e o gate de saude do Sprint 1).

## 4. Versionamento da config (Parte C)

- `zabbix-config/export/{hosts,templates,media-types}.json` + `zabbix-config/actions.json`
- Decisao do responsavel (06/11): versionar **todos os 392 templates** (135 MB)
  mesmo com apenas 9 em uso.
- Varredura de segredos: ver `zabbix-config/README.md` — achado unico = 131
  comunidades SNMP (96x `public` + 35x `hsds`), decidido commitar como esta
  (repo privado; troca de community exige autorizacao, fora do Sprint 1).

## 5. Artefatos com problema (limpeza pendente)

- `backups/zabbix/db_20261104_165543.sql.gz` (20 bytes — dump abortado) e os
  `config_20261104_*` incompletos: a retencao de 7 dias vai remove-los ate 11/11.
- Arquivo `nul` na raiz (criado por redirect equivocado em sessao anterior) —
  remover com `del \\?\...` no Windows.

## 6. Evidencias de monitoramento (pos-correcoes, 06/11)

| Metrica | Valor |
| --- | --- |
| Problemas ativos | **152** (de 181 base) |
| Wi-Fi taxa de erros | **0** (era 35) |
| Severidade impressora | **0** (era 7) |
| ICMP/agent down | 14 (reais, secao 2.4) |
| Suprimento/toner | 21 (legitimos — troca real) |
| Câmeras absent | ~40 (monitorar; ver docs/37 fase 5) |
| Fila / pinger | queue=0, busy < 75% |

## 7. Pendencias para producao (Sprint 2+, ver docs/65)

1. **Upgrade da versao** — segue `alpine-trunk` 8.0.0rc2 (decisao de adiar).
2. **Troca das comunidades SNMP** `public`/`hsds` — exige autorizacao.
3. **Secretos** — token/senha no `.env`, sem vault.
4. **`docker-compose.production.yml` nao cobre o Zabbix** (so `zabbix-db`).
5. **CI/CD + rollback automatizado** (Sprint 2), **HA com proxy** (Sprint 5).
6. Verificar as 14 UPS/sinaleiras do `docs/64` §4 (baseline desatualizado).
