# 41 — Auditoria de Plataformas Open Source (base da F29)

- **Status**: concluída (somente leitura — nenhum código copiado)
- **Data**: 2026-10-11
- **Contexto**: F29 — evoluir a plataforma de operações de TI (inventário, topologia, observabilidade). Antes de qualquer código, estudar as plataformas de referência e fechar a questão de licença (regra 4 da especificação: público ≠ open source).

## 1. Premissa (regra que atravessa tudo)

O app é **.NET**; as referências são majoritariamente **PHP/Python/Node**. Portanto: **adaptar conceitos, nunca copiar código** — reforçado pelo mix de licenças abaixo. Formalizado no **ADR-048**.

## 2. Licenças (fechadas em 2026-10-11)

| # | Plataforma | Stack | Licença | Uso permitido |
|---|---|---|---|---|
| 3.1 | linuser/zabbix-network-topology | PHP (módulo Zabbix) + Cytoscape | **AGPL-3.0** ⚠️ | **Só conceitos** — viral; proibido copiar/derivar |
| 3.2 | ScottiBYTE/unifi-topology | Node.js 22 | **sem LICENSE** ⚠️ | Só conceitos (regra 4) |
| 3.3a | patricegautier/unifiZabbix | Templates Zabbix + SSH/mca-dump | **sem LICENSE** ⚠️ | Só conceitos; e não recomendado (SSH) |
| 3.3b | chises/zabbix-unifi | Template Zabbix via web API | **sem LICENSE** ⚠️ | Só conceitos; padrão adotado p/ F29-2 |
| 3.4 | Netipam (nodeplex) | Node/Docker | **MIT** ✅ | Conceitos de IPAM |
| 3.6 | DonkeyXBT/OpenIPAM | Browser/Flask | **MIT** ✅ | Conceitos (projeto imaturo: 0★, 2026) |
| 3.7a | netboxlabs/netbox | Python/Django | **Apache-2.0** ✅ (core) | Conceitos de modelagem (add-ons = Polyform Shield, não usar) |
| 3.7b | nautobot/nautobot | Python/Django | **Apache-2.0** ✅ | Conceitos de modelagem |

> **Nenhum código das referências foi copiado.** 4 dos 8 não têm licença permissiva clara (1 AGPL + 3 sem LICENSE) → só conceitos.

## 3. Conceitos adaptados (não código) → rumo F29

| Conceito | Origem | Vira (nosso .NET) | Fase |
|---|---|---|---|
| Identidade única device × fontes | NetBox/Nautobot modelagem | `ExternalDeviceReference` (já existe; Zabbix passa a gravar hostid) | F29-1 |
| Zabbix monitora UniFi sem SSH | chises/zabbix-unifi | Template Zabbix via web API do controller (read-only) | F29-2 |
| Métrica de interface (tráfego/erros) | templates switch genéricos | `ISnmpClient.WalkAsync` → ifHC octets/erros | F29-3 |
| Conflito de IP + histórico + leases DHCP | OpenIPAM/Netipam | Detecção de IP duplicado; leases UniFi → IPAM; timeline | F29-4 |
| Topologia "ghost" e porta-a-porta | zabbix-network-topology (AGPL, só conceito) | Nó tracejado p/ vizinho LLDP sem host; aresta porta-a-porta | F29-5 |

## 4. O que NÃO fazer

- Instalar NetBox/Nautobot/Netipam/OpenIPAM como serviços paralelos (a spec exige plataforma única).
- Copiar código AGPL ou de projeto sem LICENSE/imaturo.
- Usar o padrão `unifiZabbix` (SSH/mca-dump em 59 devices) quando há web API + SNMP.

## 5. Gate de infraestrutura (só o usuário)

- **Zabbix na rede do hospital** — hoje `192.168.10/11/125/127/50` fora de alcance da workstation e do Zabbix local → sem device real, F29-2/3/5 não produzem dado.
- **Comunidade SNMP read-only** liberada nos switches.

## 6. Lacuna fechada nesta auditoria

- Licenças das 3 plataformas que estavam "a confirmar" (unifi-topology, unifiZabbix, chises) → todas **sem LICENSE** (conceitos apenas). Pendência anterior resolvida.
