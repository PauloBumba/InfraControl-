# Ficha Técnica: Routers e Firewalls

> **Status**: Rascunho (2026-10-13)
> **Contexto**: Especificação Operations Center - Sétimo dispositivo a ser estudado
> **Referência**: Especificação do usuário (seção 2 - "Quais dispositivos devemos estudar?")

---

## Visão Geral

Routers e firewalls são o sétimo caso de estudo porque:
- São críticos para conectividade e segurança de rede
- Requerem monitoramento de interfaces, tráfego, rotas, sessões, VPNs
- Problemas de router/firewall impactam toda a rede
- A maioria oferece SNMP e APIs (Cisco, MikroTik, Fortinet, Palo Alto, etc.)

**Áreas de foco** (baseadas na especificação):
- A. Identificação e configuração
- B. Interfaces e tráfego
- C. Rotas e tabelas de roteamento
- D. Sessões e conexões
- E. VPNs
- F. Logs e regras
- G. Saúde do equipamento
- H. Histórico e prevenção

---

## A. Identificação e Configuração

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Nome | Nome do router/firewall | ✅ `Device.Name` | — |
| Fabricante | Vendor (Cisco, MikroTik, Fortinet, Palo Alto, etc.) | ✅ `Device.Vendor` | — |
| Modelo | Modelo específico | ✅ `Device.Model` | — |
| Número de série | Serial number | ✅ `Device.SerialNumber` | — |
| Firmware | Versão do firmware/OS | ❌ Não persistido | **NOVO** |
| IP de gestão | Endereço IP de acesso | ✅ `Device.Ip` | — |
| Localização | Local físico (sala, rack) | ✅ `Device.LocationId` → `Location` | — |
| Rack | Rack onde está instalado | ✅ `Device.RackId` → `Rack` | — |
| Tipo | Router/Firewall | ✅ `Device.DeviceType` | — |
| Tempo de atividade (Uptime) | Tempo desde o último boot | ⚠️ Só via SNMP sob demanda | Persistir no Device |
| Referência Zabbix | Host ID no Zabbix | ✅ `ExternalDeviceReference` (Source=Zabbix) | — |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | OID / Endpoint | Disponibilidade |
|------|-------|---------------|-----------------|
| Nome (sysName) | SNMP | `1.3.6.1.2.1.1.5.0` | Universal (MIB-II) |
| Firmware versão | SNMP | `1.3.6.1.2.1.1.1.0` (sysDescr) | Universal (contém versão) |
| Uptime (sysUpTime) | SNMP | `1.3.6.1.2.1.1.3.0` | Universal (MIB-II) |
| Serial | SNMP | Vendor-specific | Varia por fabricante |

### Fonte de cada dado

| Dado | Fonte Primária | Fonte Secundária | Observação |
|------|----------------|------------------|------------|
| Nome, IP, Localização, Rack | **InfraControl** (Device) | — | System of Record |
| Firmware, Uptime | **SNMP** (sob demanda) | Zabbix (histórico) | Persistir no Device |
| Referência Zabbix | **Zabbix sync** (F29-1) | — | ExternalDeviceReference |

### Campos do cadastro

| Campo | Obrigatório? | Padrão | Validação |
|-------|--------------|--------|-----------|
| Nome | ✅ Sim | — | Único global, case-insensitive |
| Tipo | ✅ Sim | Router/Firewall | Enum fixo |
| Fabricante | ⚠️ Opcional | — | Dropdown de vendors conhecidos |
| Modelo | ⚠️ Opcional | — | Texto livre |
| Número de série | ❌ Opcional | — | Texto livre |
| IP de gestão | ✅ Sim | — | IP válido, único global (conflito detector F29-4) |
| Localização | ✅ Sim | — | FK para Location |
| Rack | ❌ Opcional | — | FK para Rack (exige localização) |
| Comunidade SNMP | ⚠️ Opcional | — | Config de integração (por device ou global) |

---

## B. Interfaces e Tráfego

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Interfaces | Lista de interfaces | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |
| Tráfego entrada | Bytes/s | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |
| Tráfego saída | Bytes/s | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |
| Erros | Erros de rede | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |
| Estado da interface | Up/Down | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |

### Dados que o equipamento realmente disponibiliza

**Universal (MIB-II / ifTable/ifXTable)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| ifDescr | `1.3.6.1.2.1.2.2.1.2.<ifIndex>` | Descrição da interface |
| ifOperStatus | `1.3.6.1.2.1.2.2.1.8.<ifIndex>` | Status (up/down) |
| ifHCInOctets | `1.3.6.1.2.1.31.1.1.1.6.<ifIndex>` | Tráfego entrada (64-bit) |
| ifHCOutOctets | `1.3.6.1.2.1.31.1.1.1.10.<ifIndex>` | Tráfego saída (64-bit) |
| ifInErrors | `1.3.6.1.2.1.2.2.1.14.<ifIndex>` | Erros entrada |
| ifOutErrors | `1.3.6.1.2.1.2.2.1.20.<ifIndex>` | Erros saída |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Interfaces, tráfego, erros | **SNMP** (ifTable/ifXTable) | Já implementado em F29-3 |

---

## C. Rotas e Tabelas de Roteamento

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Tabela de roteamento | Rotas configuradas | ❌ Não modelado | **NOVO** |
| Rota padrão | Default gateway | ❌ Não modelado | **NOVO** |
| Rotas estáticas | Rotas manuais | ❌ Não modelado | **NOVO** |
| Rotas dinâmicas | OSPF/BGP/etc. | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**SNMP (IP-FORWARD-MIB)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| ipCidrRouteDest | `1.3.6.1.2.1.4.24.4.1.1.<dest><mask><tos><nextHop>` | Destino da rota |
| ipCidrRouteNextHop | `1.3.6.1.2.1.4.24.4.1.2.<dest><mask><tos><nextHop>` | Próximo salto |
| ipCidrRouteType | `1.3.6.1.2.1.4.24.4.1.4.<dest><mask><tos><nextHop>` | Tipo (remote/local) |
| ipCidrRouteProto | `1.3.6.1.2.1.4.24.4.1.5.<dest><mask><tos><nextHop>` | Protocolo (ospf/bgp/static) |

**Vendor-specific (Cisco)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| cIpRouteTable | `1.3.6.1.4.1.9.9.424.1.1` | Tabela de rotas Cisco |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Tabela de roteamento | **SNMP** (IP-FORWARD-MIB) | Universal |
| Rotas dinâmicas | **SNMP** (vendor-specific) | Vendor-specific |

---

## D. Sessões e Conexões

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Número de sessões | Total de conexões ativas | ❌ Não modelado | **NOVO** |
| Sessões por protocolo | TCP/UDP/ICMP | ❌ Não modelado | **NOVO** |
| Top sessões por IP | IPs com mais conexões | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**SNMP (TCP-MIB)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| tcpConnState | `1.3.6.1.2.1.6.13.1.1.<localAddr><localPort><remoteAddr><remotePort>` | Estado da conexão TCP |

**Vendor-specific (Cisco)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| cseFlowSessionTable | `1.3.6.1.4.1.9.9.484.1.1` | Tabela de sessões Cisco |

**Vendor-specific (MikroTik)**:

| Dado | API | Descrição |
|------|-----|-----------|
| IP connections | `/ip/firewall/connection` | Conexões ativas |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Sessões TCP | **SNMP** (TCP-MIB) | Universal |
| Sessões detalhadas | **API** (vendor-specific) | Vendor-specific |

---

## E. VPNs

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Túneis VPN | Lista de túneis configurados | ❌ Não modelado | **NOVO** |
| Estado do túnel | Up/Down | ❌ Não modelado | **NOVO** |
| Tráfego do túnel | Bytes/s por túnel | ❌ Não modelado | **NOVO** |
| Sessões VPN | Usuários conectados | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**Vendor-specific (Cisco)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| cipSecTunnelTable | `1.3.6.1.4.1.9.9.171.1.2` | Tabela de túneis IPSec Cisco |
| cipSecTunnelStatus | `1.3.6.1.4.1.9.9.171.1.2.1.1` | Status do túnel |

**Vendor-specific (MikroTik)**:

| Dado | API | Descrição |
|------|-----|-----------|
| VPN tunnels | `/interface/vpn` | Túneis configurados |
| VPN users | `/ppp/secret` | Usuários VPN |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Túneis VPN, estado, tráfego | **API** (vendor-specific) | Vendor-specific |

---

## F. Logs e Regras

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Logs de firewall | Logs de aceitação/rejeição | ❌ Não modelado | **NOVO** |
| Regras ativas | Lista de regras configuradas | ❌ Não modelado | **NOVO** |
| Regras bloqueando | Regras com hits altos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | Disponibilidade |
|------|-------|-----------------|
| Logs de firewall | Syslog | Requer integração Syslog |
| Regras ativas | API do firewall | Vendor-specific |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Logs de firewall | **Syslog** | Fora escopo inicial - requer integração custom |
| Regras ativas | **API do firewall** | Vendor-specific |

---

## G. Saúde do Equipamento

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| CPU | Utilização % | ⚠️ Parcial (SnmpSystem check genérico) | Provider específico |
| Memória | Livre/Total % | ⚠️ Parcial (SnmpSystem check genérico) | Provider específico |
| Temperatura | Temperatura dos componentes | ❌ Não modelado | **NOVO** |
| Ventoinhas (Fans) | Status (normal/warning/critical) | ❌ Não modelado | **NOVO** |
| Fontes de alimentação (PSUs) | Status (OK/Fail/Redundant) | ❌ Não modelado | **NOVO** |
| Uptime | Tempo desde boot | ⚠️ Só SNMP sob demanda | Persistir no Device |

### Dados que o equipamento realmente disponibiliza

**SNMP por fabricante**:

| Dado | Cisco | MikroTik | Fortinet | Palo Alto |
|------|-------|----------|----------|-----------|
| CPU % | Vendor-specific | `1.3.6.1.2.1.25.3.3.1.2` | Vendor-specific | Vendor-specific |
| Memória % | Vendor-specific | `1.3.6.1.2.1.25.2.3.1.5` | Vendor-specific | Vendor-specific |
| Temperatura | Vendor-specific | Vendor-specific | Vendor-specific | Vendor-specific |
| Fans | Vendor-specific | Vendor-specific | Vendor-specific | Vendor-specific |
| PSUs | Vendor-specific | Vendor-specific | Vendor-specific | Vendor-specific |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| CPU, Memória | **SNMP** (vendor-specific) | Provider custom por vendor |
| Temperatura, Fans, PSUs | **SNMP** (vendor-specific) | Provider custom por vendor |

---

## H. Histórico e Prevenção

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Tendência de tráfego | Tráfego ao longo do tempo | ❌ Não modelado | **NOVO** (prevenção) |
| Sessões crescendo | Número de sessões aumentando | ❌ Não detectado | **NOVO** (prevenção) |
| VPN falhando | Túneis caindo | ❌ Não detectado | **NOVO** (prevenção) |
| CPU elevada sustained | CPU > limiar por tempo | ❌ Não detectado | **NOVO** (prevenção) |

### Modelo de Manutenção Preventiva (proposto)

| Problema | Sinal | Antecipação | Ação |
|----------|-------|-------------|------|
| Saturação de interface | Tráfego próximo à capacidade | Média de tráfego > 80% da velocidade | Planejar upgrade/agregação |
| Sessões crescendo | Número de sessões aumentando | Sessões > limiar por X minutos | Investigar DDoS, normalizar tráfego |
| VPN falhando | Túneis caindo recorrentemente | > X falhas em Y minutos | Investigar rede, credenciais, ISP |
| CPU elevada sustained | CPU > 80% por 10 minutos | CPU tendência ascendente | Investigar tráfego, processos, regras |

---

## Métricas que Precisam de Histórico

| Métrica | Período de retenção | Resolução | Justificativa |
|---------|-------------------|-----------|---------------|
| Tráfego de interface (in/out) | 90 dias | 5 minutos | Análise de capacidade |
| CPU/Memória | 30 dias | 1 minuto | Detecção de sobrecarga |
| Sessões | 30 dias | 5 minutos | Detecção de anomalias |
| Estado de túnel VPN | 30 dias | Evento | Análise de disponibilidade |
| Uptime | Permanente | Evento (boot) | Detecção de reboots |

**Nota**: O Zabbix pode manter histórico numérico. O InfraControl deve:
- Consultar o Zabbix para histórico detalhado (via API)
- Persistir apenas eventos agregados e tendências no PostgreSQL (REGRA 8 de ADR-028)

---

## Condições de Alerta

### Alertas reativos (existentes)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Router/Firewall não responde a ping | Critical | ✅ Ping check |
| Interface down | Warning | ✅ ifOperStatus via SNMP |

### Alertas preventivos (novos)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| CPU > 90% por 10 minutos | Critical | ❌ Novo |
| CPU > 80% por 5 minutos | Warning | ❌ Novo |
| Memória > 90% | Critical | ❌ Novo |
| Memória > 80% | Warning | ❌ Novo |
| Tráfego > 80% da capacidade | Warning | ❌ Novo |
| Tráfego > 90% da capacidade | Critical | ❌ Novo |
| Sessões > limiar | Warning | ❌ Novo |
| Túnel VPN down | Critical | ❌ Novo |
| Túnel VPN flapping | Warning | ❌ Novo |

---

## Operações Administrativas Permitidas

### Escopo inicial (somente monitoramento)

| Operação | Fonte | Implementação | Status |
|----------|-------|----------------|--------|
| Consultar estado atual | SNMP | Provider novo | ⚠️ A implementar |
| Consultar rotas | SNMP/API | Provider novo | ⚠️ A implementar |
| Consultar sessões | SNMP/API | Provider novo | ⚠️ A implementar |
| Consultar histórico de métricas | Zabbix API | Query Zabbix para histórico | ⚠️ Parcial |

### Operações futuras (fora escopo inicial)

| Operação | Justificativa | Pré-requisitos |
|----------|---------------|----------------|
| Reiniciar interface | Operação crítica | SSH/power control |
| Limpar sessões | Operação crítica | SSH/API |
| Alterar rota | Operação crítica | SSH/API |

---

## Tratamento de Dados Indisponíveis, Erros e Credenciais

### Dados indisponíveis

| Situação | Comportamento | Mensagem ao usuário |
|-----------|---------------|---------------------|
| Router/Firewall offline | Estado = Unknown, Cache último valor conhecido | "Router offline. Última atualização: [timestamp]" |
| API indisponível | Estado = Degraded, Cache último valor conhecido | "API indisponível. Dados em cache." |
| SNMP community incorreta | Erro de autenticação, Log warn | "Erro de autenticação SNMP. Verifique community string." |
| OID não suportado | Campo = null, Log info | "Métrica não disponível neste modelo." |

### Erros

| Tipo de erro | Tratamento | Registro |
|--------------|------------|----------|
| Falha de rede | Retentativa Polly (circuit breaker) | Serilog com `DeviceId`, `Provider` |
| Resposta inválida | Validar schema, descartar entrada | Serilog warn |
| Timeout | Configurar timeout adequado (3s padrão) | Serilog info |

### Credenciais

| Credencial | Armazenamento | Acesso |
|------------|----------------|---------|
| SNMP community | Environment variable (por device ou global) | `ICredentialProtector` (F11-T006) |
| API (vendor) | Environment variable | Redação total em DTOs |

---

## Estratégia de Implementação

### Abordagem por vendor

**1. SNMP universal - Prioridade Alta**:
- ifTable/ifXTable para tráfego
- IP-FORWARD-MIB para rotas
- TCP-MIB para sessões
- Funciona para maioria dos fabricantes

**2. Vendor-specific APIs - Prioridade Média**:
- MikroTik API (documentada, relativamente padrão)
- Cisco API (vendor-specific)
- Fortinet API (vendor-specific)
- Palo Alto API (vendor-specific)
- Fallback seguro: se vendor não suportado, usar SNMP universal

### Prioridade de Implementação

| Ordem | Item | Justificativa |
|-------|------|---------------|
| 1 | **SnmpRouterProvider** (tráfego, rotas, sessões) | SNMP universal |
| 2 | **MikroTikRouterProvider** (rotas, sessões, VPNs) | MikroTik API documentada |
| 3 | **SnmpRouterHealthProvider** (CPU, memória) | Vendor-specific OIDs |
| 4 | **Modelo de histórico/prevenção** | Limiar, tendência, saturação |
| 5 | **Vendor-specific APIs** (Cisco, Fortinet, Palo Alto) | Apenas se necessário |

---

## Estratégia de Teste

### Ambiente de homologação

| Requisito | Implementação |
|-----------|---------------|
| Router/Firewall real para testes | Router/Firewall em laboratório (não produção) |
| Comunidade SNMP liberada | Read-only community configurada |
| API habilitada | API ativada no dispositivo |
| Acesso Zabbix | Host criado no Zabbix de teste |
| Dados variados | Gerar tráfego, testar rotas, VPNs |

### Testes automatizados

| Tipo | Escopo |
|------|--------|
| Unit | Providers SNMP/API (SnmpRouterProvider, MikroTikRouterProvider) |
| Integration | Repository + Query com PostgreSQL |
| E2E | Ponta-a-ponta com router/firewall real (gate de infra) |
| Mock | Simular respostas SNMP/API para cenários de erro |

---

## Próximos Passos (Recomendação)

1. **Prioridade imediata**:
   - Criar `SnmpRouterProvider` (tráfego, rotas via IP-FORWARD-MIB, sessões via TCP-MIB)
   - Criar `MikroTikRouterProvider` (rotas, sessões, VPNs via API)
   - Criar `SnmpRouterHealthProvider` (CPU, memória via vendor-specific OIDs)

2. **Prioridade média**:
   - Implementar modelo de histórico (tendência de tráfego, sessões)
   - Implementar modelo de prevenção (limiar, saturação, VPN falhando)
   - Adicionar `Device.Criticality` para routers/firewalls

3. **Prioridade baixa**:
   - Vendor-specific APIs (Cisco, Fortinet, Palo Alto)
   - Integração Syslog (logs de firewall)
   - Monitoramento de regras de firewall

---

## Referências

- IP-FORWARD-MIB: `1.3.6.1.2.1.4`
- TCP-MIB: `1.3.6.1.2.1.6`
- ADR-028: Arquitetura da Camada de Integrações
- ADR-048: Identidade Única de Device, Zabbix×UniFi via API
- F2-T008: DeviceProfile
- MikroTik API Documentation: https://wiki.mikrotik.com/wiki/API
- Cisco SNMP OID Reference: https://www.cisco.com/
