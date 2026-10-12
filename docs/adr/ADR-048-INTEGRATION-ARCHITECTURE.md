# ADR-048: Arquitetura de Integrações — Identidade Única de Device, Zabbix×UniFi via API e Somente-Leitura

- **Status**: aceito
- **Data**: 2026-10-11
- **Contexto**: F29 — evoluir a plataforma de operações de TI (inventário, topologia, observabilidade) a partir das 7 plataformas open source auditadas; prioridade em aprofundar integrações (UniFi/Zabbix/SNMP).

## Contexto

A auditoria (relatório F29) mostrou que o repo já está ~65% do caminho da especificação: inventário de devices, topologia LLDP/CDP, IPAM, SNMP (MIB-II), UniFi (17 arquivos) e Zabbix (F27). As lacunas restantes são de **integração e correlação**, não de construção nova.

As plataformas de referência estudadas são majoritariamente **PHP/Python/Node** (stack alienígena ao nosso .NET) e nem todas têm licença clara:

| Referência | Licença | Uso permitido |
|---|---|---|
| linuser/zabbix-network-topology | **AGPL-3.0** | Só conceitos (viral; proibido copiar código) |
| NetBox / Nautobot | Apache-2.0 | Conceitos de modelagem |
| Netipam / OpenIPAM | MIT | Conceitos de IPAM |
| unifi-topology / unifiZabbix / chises-zabbix-unifi | **sem LICENSE** | Só conceitos (regra 4: público ≠ open source) |

## Decisão

### 1. Identidade única de device via `ExternalDeviceReference` (espinha já existente)

- A correlação entre fontes usa `ExternalDeviceReference` (`Source`, `ExternalId`, `DeviceId`), unicidade física em `(Source, ExternalId)`, 1 Device → N referências.
- O enum `ExternalDeviceSource` **já contém** `UniFi = 1` e `Zabbix = 2` (F19-T006/F20) — não é preciso novo enum nem migração de schema.
- A resolução do Zabbix por IP/hostname permanece (regra 6 da revisão F19/F20/F21): a referência entra quando a fonte manda um **identificador estável** (hostid do Zabbix, MAC/Device ID do UniFi).
- **F29-1** torna isso explícito: o sync Zabbix passa a **gravar** a referência `Source=Zabbix, ExternalId=hostid` quando correlaciona host↔device, em vez de só re-derivar por IP a cada leitura. Não duplica device — funde a identidade.

### 2. Zabbix × UniFi via web API do controller (padrão chises), nunca SSH

- Para o Zabbix monitorar os devices UniFi, adota-se o padrão de **web API do controller com usuário read-only** (como `chises/zabbix-unifi`), atribuindo template ao host do controller.
- **Não** se usa o padrão `unifiZabbix` (SSH + `mca-dump`), que exige par de chaves SSH em cada device e é pesado/fragil (limite de 64k, dropbear, keys apagadas a cada firmware).
- A API do InfraControl continua sendo a agregadora de dados UniFi (já expõe `GetTopologyAsync`, `GetPortsSummaryAsync`, `ListSwitchPortsAsync`, `ListConnectedClientsAsync`); o Zabbix monitora o controller e o InfraControl correlaciona o resultado de volta ao device.

### 3. Estância Zabbix permanece somente-leitura

- O `ZabbixApiClient` **não** cria/edita/deleta hosts (`host.create/update/delete` ausentes por desenho, F27). Criar host de device UniFi no Zabbix seria mudança de arquitetura e exigiria ADR próprio.
- Portanto a integração Zabbix×UniFi é de **leitura/monitoração**, não de provisionamento: o InfraControl lê o que o Zabbix sabe e o Zabbix lê o controller via API.

### 4. Adaptação de conceitos, nunca cópia de código

- Dado o mix AGPL + "sem LICENSE", **nenhum código** das referências é copiado/derivado. Adaptam-se apenas **conceitos** (nó "ghost" de vizinho LLDP sem host, aresta porta-a-porta, detecção de conflito de IP, leases DHCP, estado observado×pretendido), implementados em C# no nosso domínio.
- Este ADR formaliza a regra jurídica que sustenta a F29 inteira.

## Consequências

- **Benefícios**:
  - Identidade única de device sem novo schema (reaproveita `ExternalDeviceReference`).
  - Zero risco de contaminação AGPL / violação de licença.
  - Integração Zabbix×UniFi sem SSH em 59 devices (só 1 usuário read-only no controller).
  - Estância somente-leitura preservada — menor superfície de ataque e sem drift de provisionamento.

- **Trade-offs**:
  - Zabbix monitora o **controller** (visão agregada), não cada device individualmente via SSH — métrica por device fino vem do nosso SNMP/UniFi, não do Zabbix.
  - Correlação host↔device por IP/hostname continua sendo heurística (ambiguidade se dois hosts tiverem o mesmo IP) — a referência `hostid` mitiga mas não elimina.
  - A contagem de "hosts Zabbix sem asset" só cai de verdade quando o Zabbix alcançar os devices na rede do hospital (gate de infra).

- **Gate de infraestrutura** (só o usuário resolve):
  - Zabbix implantado na rede do hospital (hoje fora de alcance: `192.168.10/11/125/127/50`).
  - Comunidade SNMP read-only liberada nos switches.

- **Relacionados**: ADR-023 (webhook Zabbix), ADR-028 (n8n notificação de saída), F27 (integração Zabbix), F29-1..F29-5 (execução).
