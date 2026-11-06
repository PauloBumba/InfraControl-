# 64. Auditoria Zabbix — preparação para produção (hospital)

> **Missão do usuário (2026-11-01):** auditoria técnica completa da instalação Zabbix, investigação
> grupo por grupo e host por host, correção dos problemas encontrados e preparação para produção.
> **Escopo:** somente Zabbix (sem alterações em InfraControl, switches, VLANs, roteadores, firewalls
> ou configurações SNMP dos equipamentos sem autorização).
> **Método:** inventário → auditoria por grupo → correções seguras → validação → relatório por grupo.
> Este documento é o **checklist vivo** da auditoria (docs/37 continua sendo o checklist da F27).

---

## Aviso importante de versão

A missão cita **Zabbix 7.4**, mas a instalação real é **Zabbix 8.0.0rc2** (imagens `alpine-trunk`,
revisão 65e673a, build 2026-10-16; web no ar desde 2026-10-19). Todos os achados e comandos abaixo
valem para esta versão. Se a intenção era 7.4 estável, isso é uma decisão de deploy separada.

---

## 1. Estado geral da instalação (descoberta 2026-11-01)

| Componente | Versão/Estado |
| --- | --- |
| Zabbix Server | 8.0.0rc2 (revision 65e673a) — container `infracontrol-zabbix-server`, healthy |
| Zabbix Web (frontend) | 8.0 (nginx-pgsql alpine-trunk) — porta 8086, HTTP 200 |
| Zabbix Agent 2 | 8.0.0rc2 (go1.26.9) — container dedicado (porta 10053); único host com agent = `Zabbix server` |
| Banco de dados | PostgreSQL (container `infracontrol-zabbix-db`), **6,9 GB**, history 6,28 M linhas, trends 184 k |
| Proxies | nenhum cadastrado (server único, sem HA real — `HANODENAME` definido, modo standalone) |
| Fila (queue) | **OK** — `zabbix[queue]`=0–1, `queue,10m`=0, preprocessing=0, LLD=0, discovery=0 |
| Housekeeping | history 31d, trends 365d, eventos de trigger 365d, audit 31d, sessões 31d |
| Usuários | **apenas 2**: `Admin` (Super Admin) e `guest` — sem conta operacional (ver §8) |
| SNMP global | 131 interfaces SNMP (17 v1 + 114 v2c), **todas com comunidade `public`** (ver §8) |
| Actions | 7 actions; **apenas `InfraControl - triggers` (id 7) habilitada**, com 2 operações (Problem + Recovery → webhooks 105/106) |
| Media types | `InfraControl - Problem/Recovery` (105/106) habilitados; Email/SMS/Gmail/etc padrão desabilitados |
| Antifraud/Backup | exportação de configuração não realizada nesta data — **primeira pendência de §7** |

### Saúde interna (itens do próprio server, valores de 2026-11-01 00:36 UTC)

| Métrica | Valor | Avaliação |
| --- | --- | --- |
| Config cache (rcache) | **73,9 %** | ⚠️ alto para 152 hosts — acompanhar; subir `CacheSize` se passar de 85 % |
| Value cache | 31,9 % | OK (hits 539/s, misses 0) |
| History write cache | 0 % | OK |
| History index cache | 22,0 % | OK |
| Trend write cache | 16,0 % | OK |
| Valores processados | ~71 vps (64 uint + 6,6 float) | OK |
| **icmp pinger busy** | **100 %** | 🔴 **gargalo confirmado** — há problema "Utilization of icmp pinger processes over 75%" ativo (Zabbix server, sev Média) |
| snmp poller | 2,8 % | OK |
| poller agent | 0,08 % | OK |
| history syncer | 2,7 % | OK |
| unreachable poller | 0 % | OK |
| DB | 6,9 GB / 6,28 M valores | monitorar crescimento (trends 365d) |

### Sintomas observados nos logs do server

- **Flapping de `printer.alert.severity[1.x]`**: ~5 impressoras alternando
  `became not supported (No Such Instance)` ↔ `became supported` a cada ~5 min — item LLD
  criado para índice de alerta inexistente no equipamento (ver grupo Impressoras).
- Erros SNMP pontuais (`first network error, wait for 15 seconds`) em AP e impressora — normais e
  auto-recuperáveis.
- Trigger `"Alerta não crítico ou severidade não mapeada: 7"` foi **desabilitada**
  (`escalation canceled ... trigger disabled`) — manter decisão rastreada em §7.

---

## 2. Inventário completo de grupos (15 grupos, 152 hosts, 0 desativados)

| Grupo | Hosts | Templates | Interfaces | Itens (sup/unsup) | Última coleta |
| --- | --- | --- | --- | --- | --- |
| Access Points HSDS | 31 | 30× Network Generic Device by SNMP + 1× ICMP Ping | 30 SNMP + 1 agent(ICMP) | 4.104/0 | recente |
| Access Points HSSM | 12 | 12× ICMP Ping | 12 agent(ICMP) | 36/0 | recente |
| Access Points UniFi | 43 | 30× Network Generic Device by SNMP + 13× ICMP Ping | 30 SNMP + 13 agent(ICMP) | 4.140/0 | recente |
| Cameras HSDS | 43 | 40× ICMP Ping + 3× Network Generic Device by SNMP | 43 SNMP | 162/0 | recente |
| Computadores | 1 | 1× Windows by Zabbix agent | 1 agent (192.168.11.121:10060) | 199/**1** | ⚠️ 2026-10-26 (agent inacessível) |
| Impressoras | 37 | 36× Network Generic + 36× IC Impressora SNMP + 1× ICMP Ping | 37 SNMP | 1.055/0 | recente |
| Impressoras HSDS | 33 | 33× Network Generic + 33× IC Impressora SNMP | 33 SNMP | 984/0 | recente |
| Impressoras HSSM | 4 | 3× Network Generic + 3× IC Impressora SNMP + 1× ICMP Ping | 4 SNMP | 71/0 | recente |
| NVRs HSDS | 1 | Generic by SNMP + Intelbras Dahua NVR by SNMP (F27-T013) | 2 SNMP | 275/0 | recente |
| Servidores | 1 | Windows by Zabbix agent | 1 agent (zabbix.brdrive.cloud:10051) | 111/0 | 🔴 **sem dados nunca** |
| Switches de Mesa | 4 | 4× ICMP Ping | 4 agent(ICMP) | 12/0 | recente |
| Switches HP | 10 | 10× HP Comware HH3C by SNMP | 10 SNMP | 3.040/0 | recente |
| Switches Intelbras | 5 | 5× Network Generic Device by SNMP | 5 SNMP | 1.076/0 | recente |
| Switches UniFi | 6 | 5× Network Generic by SNMP + 1× ICMP Ping | 5 SNMP + 1 agent(ICMP) | 1.171/0 | recente |
| Zabbix servers | 1 | Linux by Zabbix agent + Zabbix server health | 1 agent | 158/**12** | recente |

- Nota: hosts de impressoras aparecem nos grupos `Impressoras` (geral) **e** `Impressoras HSDS/HSSM`
  (por unidade) — sem duplicação de cadastro (sem IPs duplicados; verificado).
- **Total de itens suportados ≈ 20,8 mil; unsupported = 33** (detalhe em §4).

### Disponibilidade da interface principal (flags do Zabbix)

| Estado | Grupos/hosts |
| --- | --- |
| Unavailable (2) | `HSDS_SUPTI666` (agent), `IEAS-Impressoes-Servidor` (agent), 1 AP HSDS, 1 AP UniFi, 4 impressoras |
| Unknown (0) | todos os hosts ICMP-only (comportamento normal para simple check) e 12 APs HSSM |
| Available (1) | demais hosts SNMP/agent |

---

## 3. Problemas ativos (198 — captura 2026-11-01)

| Severidade | Qtd |
| --- | --- |
| Desastre | 0 |
| Alta | 17 |
| Média | 44 |
| Aviso | 121 |
| Informação | 16 |
| Reconhecidos | **0** |
| Suprimidos | **0** |

### Distribuição por causa (top)

| Qtd | Causa | Grupo/Host |
| --- | --- | --- |
| 64 | `Camera N absent` (triggers **desabilitadas** mas problemas ainda ativos) | NVRs HSDS |
| 27 | `Interface wifi0(): High error rate (>2 for 5m)` | APs (Wi-Fi) |
| 13 | ICMP ping indisponível (APs, impressoras e outros) | vários |
| 9 | Windows: serviços/bitlocker (HSDS_SUPTI666) | Computadores |
| 9 | Toner/drum Canon baixo ou crítico (5 %/10 %) | Impressoras |
| 8 | HP Comware: interfaces sem link / velocidade reduzida | Switches HP |
| 3 | HP Comware: temperatura acima do limiar (50 °C warning / 60 °C critical) | Switches HP |
| 2+ | InfraControl smoke + pinger 75 % (testes/server) | Zabbix server |

### Achado de processo: triggers desabilitadas com problema pendente

Existem **68 triggers desabilitadas ainda com `value=1`** (64 de câmera do NVR + 4 outras). Como
trigger desabilitada não evalua recuperação, os 64 problemas "Camera absent" ficam **permanentemente
abertos** na lista — poluem a visão operacional. Correção segura proposta em §7 (F).

---

## 4. Itens sem suporte (33 — estado `unsupported`)

| Host | Qtd | Causa | natureza |
| --- | --- | --- | --- |
| `Zabbix server` | 12 | processos internos não iniciados (ipmi, java, vmware, connector×2, report×2, snmp trapper) + `Number of installed packages` (Alpine não expõe) | **normal** — itens de features não usadas |
| `ap-192-168-11-167` | 18 | interfaces `br0`/`br0.13` (LLD ifTable) com `No Such Instance` | **itens órfãos** — interfaces não existem mais/foram renomeadas no AP |
| `HSDS_SUPTI666` | 2 (+1) | JSON path `Realtek USB GbE #3` sem correspondência + `Item does not allow parameters` | adaptador USB desconectado / item mal configurado |
| `prn-192-168-22-16` | 1 | `printer.alert.severity[1.1]` No Such Instance (flapping, ver §1) | índice de alerta inexistente |

---

## 5. Estado por grupo (progresso da auditoria)

| # | Grupo | Estado | Observação |
| --- | --- | --- | --- |
| 1 | Access Points HSDS | **AUDITADO (2026-11-01)** — ver §9 | 29 APROVADO, 3 APROVADO COM RESSALVAS, 0 reprovado |
| 2 | Access Points HSSM | PENDENTE | 12 hosts só ICMP (cobertura limitada por design) |
| 3 | Access Points UniFi | PENDENTE | 1 SNMP unavailable |
| 4 | Cameras HSDS | PENDENTE | 40/43 só ICMP |
| 5 | Computadores | PENDENTE | agent inacessível desde 26/10 |
| 6 | Impressoras (HSDS/HSSM) | PENDENTE | flapping alert.severity, toner crítico real |
| 7 | NVRs HSDS | PENDENTE | 64 problemas órfãos de triggers desabilitadas |
| 8 | Servidores | PENDENTE | **sem dados desde o cadastro** |
| 9 | Switches de Mesa | PENDENTE | 4 hosts ICMP-only |
| 10 | Switches HP | PENDENTE | 3 temperaturas + portas sem link |
| 11 | Switches Intelbras | PENDENTE | |
| 12 | Switches UniFi | PENDENTE | 1 ICMP-only |
| 13 | Zabbix servers | PENDENTE | 12 unsupported internos + pinger 100 % |

---

## 6. Pendências por prioridade

### 🔴 Crítico (impede produção)

1. **`IEAS-Impressoes-Servidor` nunca coletou dados** — interface agent `zabbix.brdrive.cloud:10051`
   retorna resposta vazia. Depende de informação/autorização do usuário (host externo? proxy?).
2. **`icmp pinger` a 100 % de ocupação** — risco de ping atrasado/falso negativo em toda a frota.
   Correção = aumentar `StartPingers` (requer aprovação: altera processo de produção).

### ⚠️ Alto (compromete confiabilidade)

3. **Comunidade SNMP `public` em 131 interfaces** — recomendação: comunidade read-only dedicada ou
   SNMPv3. **Exige autorização** (muda coleta em produção).
4. **64+ problemas órfãos** de triggers de câmera desabilitadas (§3).
5. **Flapping `printer.alert.severity`** — ~5 impressoras com item LLD inválido gerando churn.
6. **68 triggers desabilitadas** sem registro do porquê (4 fora das câmeras) — auditar e justificar.
7. **Sem backup/exportação da configuração do Zabbix** nesta data.

### 🟡 Médio (limita funcionalidades)

8. `HSDS_SUPTI666` (Computadores): agent inacessível (porta 10060) — última coleta 26/10; itens USB órfãos.
9. `ap-192-168-11-167`: 18 itens de interfaces SNMP órfãos (br0/br0.13).
10. Config cache 73,9 % — acompanhar; sem proxy/HA (single point of failure do server).
11. 0 problemas reconhecidos — nenhum processo de ack em uso.
12. Apenas 2 usuários (Admin/guest) — sem papel operacional (Operator) para notificações futuras.

### 🔵 Baixo (melhoria)

13. Templates padrão Zabbix 8.0 inteiros instalados (392) — a maioria não usada; não remover sem autorização.
14. Tendência de crescimento do banco (6,9 GB) — revisar retenção se crescimento acelerar.

---

## 7. Plano de correção (método grupo a grupo)

| ID | Correção | Risco | Reversível | Status |
| --- | --- | --- | --- | --- |
| A | Exportar configuração completa (backup via `configuration.export`) antes de alterações | nenhum | n/a | PENDENTE |
| B | Group 1 (APs HSDS): remover/disable itens LLD órfãos do `ap-192-168-11-167` após confirmar via SNMP walk que br0/br0.13 não existem | baixo | sim (disable) | PENDENTE |
| C | Investigar interface unavailable dos 2 APs SNMP (retry vs community) | baixo | n/a | PENDENTE |
| D | Impressoras: identificar índice `printer.alert.severity` inválido por host e desabilitar item afetado (não o template) | baixo | sim | PENDENTE |
| E | NVR: resolver os 64 problemas órfãos (trigger desabilitada com problema ativo) — via reabilitar+recuperar ou supressão documentada; **não** apagar eventos | baixo | sim | PENDENTE |
| F | Auditar as 4 triggers desabilitadas fora das câmeras; justificar ou reabilitar | baixo | sim | PENDENTE |
| G | `StartPingers` (aprovação) + avaliar `StartSNMPPollers` | **médio** — production | sim | AGUARDA APROVAÇÃO |
| H | SNMP community dedicada (aprovação) | **médio** — production | sim | AGUARDA APROVAÇÃO |
| I | `HSDS_SUPTI666` + `IEAS-Impressoes-Servidor` (dependem de acesso/informação) | n/a | n/a | AGUARDA USUÁRIO |

Critério de host **APROVADO**: cadastrado, interface correta, método de coleta funcionando,
template compatível, itens essenciais com dados, sem erros críticos sem tratar, triggers coerentes,
última coleta validada. Estados: APROVADO / APROVADO COM RESSALVAS / PENDENTE / REPROVADO.

---

## 8. Integração com InfraControl (verificação, sem alterações)

- Webhook action 7 **ativa** e com operações Problem (105) + Recovery (106) — confirma F16/docs/27.
- Grupos e hosts usam nomes estáveis (`prn-…`, `sw-…`, `ap-…`) — adequados para a sincronização F27.
- Severidades nativas (Info/Aviso/Média/Alta) já mapeadas no catálogo do InfraControl.
- Nenhuma mudança de estrutura será feita sem avaliar impacto na integração (regra da missão §15).

---

## Evidências

- Coleta via API JSON-RPC (`user.login` Admin) + consultas diretas no PostgreSQL `zabbix`
  (2026-11-01). Artefatos brutos (JSON de hosts/grupos/problemas/itens) em
  `%TEMP%\opencode\` da sessão — não versionados (contêm topologia real do hospital).
- Nenhuma credencial gravada neste documento; nenhum host apagado; nenhuma alteração executada até aqui
  (descoberta somente leitura).

<!--
Checklist de progresso (atualizar a cada grupo):
- [ ] Grupo 1 Access Points HSDS — auditoria + correções + validação
- [ ] Grupo 2 Access Points HSSM
- [ ] Grupo 3 Access Points UniFi
- [ ] Grupo 4 Cameras HSDS
- [ ] Grupo 5 Computadores
- [ ] Grupo 6 Impressoras HSDS
- [ ] Grupo 7 Impressoras HSSM
- [ ] Grupo 8 NVRs HSDS
- [ ] Grupo 9 Servidores
- [ ] Grupo 10 Switches de Mesa
- [ ] Grupo 11 Switches HP
- [ ] Grupo 12 Switches Intelbras
- [ ] Grupo 13 Switches UniFi
- [ ] Grupo 14 Zabbix servers
- [ ] Relatório final de produção
-->
