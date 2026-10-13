# Ficha Técnica: Access Point Wi-Fi

> **Status**: Rascunho (2026-10-13)
> **Contexto**: Especificação Operations Center - Quarto dispositivo a ser estudado
> **Referência**: Especificação do usuário (seção 2 - "Quais dispositivos devemos estudar?")

---

## Visão Geral

O Access Point Wi-Fi é o quarto caso de estudo porque:
- É crítico para conectividade de usuários (BYOD, laptops, dispositivos móveis)
- Requer monitoramento de clientes, canais, interferência e sinal
- Problemas de Wi-Fi impactam diretamente a experiência do usuário
- A maioria dos APs modernos oferece APIs de controller (UniFi, Ruckus, Aruba, etc.)

**Áreas de foco** (baseadas na especificação):
- A. Identificação e configuração
- B. Estado e disponibilidade
- C. Clientes conectados
- D. SSIDs e canais
- E. Rádio e sinal
- F. Tráfego e alimentação PoE
- G. Histórico e prevenção

---

## A. Identificação e Configuração

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Nome | Nome do AP | ✅ `Device.Name` | — |
| Fabricante | Vendor (Ubiquiti, Ruckus, Aruba, TP-Link, etc.) | ✅ `Device.Vendor` | — |
| Modelo | Modelo específico | ✅ `Device.Model` | — |
| Número de série | Serial number | ✅ `Device.SerialNumber` | — |
| Firmware | Versão do firmware | ❌ Não persistido | **NOVO** |
| IP de gestão | Endereço IP de acesso | ✅ `Device.Ip` | — |
| Localização | Local físico (sala, andar) | ✅ `Device.LocationId` → `Location` | — |
| Mac Address | MAC do AP | ✅ `NetworkInterface.MacAddress` | — |
| Controller | Controller associado (se aplicável) | ⚠️ Parcial (UniFi integration F18) | — |
| Tempo de atividade (Uptime) | Tempo desde o último boot | ⚠️ Só via SNMP sob demanda | Persistir no Device |
| Referência Zabbix | Host ID no Zabbix | ✅ `ExternalDeviceReference` (Source=Zabbix) | — |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | OID / Endpoint | Disponibilidade |
|------|-------|---------------|-----------------|
| Nome (sysName) | SNMP | `1.3.6.1.2.1.1.5.0` | Universal (MIB-II) |
| Firmware versão | SNMP | `1.3.6.1.2.1.1.1.0` (sysDescr) | Universal (contém versão) |
| Uptime (sysUpTime) | SNMP | `1.3.6.1.2.1.1.3.0` | Universal (MIB-II) |
| MAC address | SNMP | `1.3.6.1.2.1.2.2.1.6.<ifIndex>` | Universal (ifPhysAddress) |
| Controller | API do controller | Vendor-specific | Requer integração |

### Fonte de cada dado

| Dado | Fonte Primária | Fonte Secundária | Observação |
|------|----------------|------------------|------------|
| Nome, IP, Localização | **InfraControl** (Device) | — | System of Record |
| Firmware, Uptime | **SNMP** (sob demanda) | Zabbix (histórico) | Persistir no Device |
| Controller | **API do controller** (F18 para UniFi) | — | Vendor-specific |
| Referência Zabbix | **Zabbix sync** (F29-1) | — | ExternalDeviceReference |

### Campos do cadastro

| Campo | Obrigatório? | Padrão | Validação |
|-------|--------------|--------|-----------|
| Nome | ✅ Sim | — | Único global, case-insensitive |
| Tipo | ✅ Sim | AccessPoint | Enum fixo |
| Fabricante | ⚠️ Opcional | — | Dropdown de vendors conhecidos |
| Modelo | ⚠️ Opcional | — | Texto livre |
| Número de série | ❌ Opcional | — | Texto livre |
| IP de gestão | ✅ Sim | — | IP válido, único global (conflito detector F29-4) |
| Localização | ✅ Sim | — | FK para Location |
| Comunidade SNMP | ⚠️ Opcional | — | Config de integração (por device ou global) |

---

## B. Estado e Disponibilidade

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Online/Offline | Estado de conectividade | ✅ Ping check (DeviceProfileDefaults) | — |
| Estado do AP | Ready/Upgrading/Isolated | ❌ Não modelado | **NOVO** |
| Estado do rádio | Enabled/Disabled | ❌ Não modelado | **NOVO** |
| Alimentação PoE | Status PoE | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**SNMP por fabricante**:

| Dado | UniFi (Ubiquiti) | Ruckus | Aruba | TP-Link |
|------|------------------|--------|-------|---------|
| AP status | Via controller API | Vendor-specific | Vendor-specific | Vendor-specific |
| Radio status | Via controller API | Vendor-specific | Vendor-specific | Vendor-specific |
| PoE status | Via controller API | Vendor-specific | Vendor-specific | Vendor-specific |

**Universal (ifTable)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| ifOperStatus | `1.3.6.1.2.1.2.2.1.8.<ifIndex>` | Status da interface (up/down) |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Online/Offline | **Ping** (existente) | DeviceProfileDefaults |
| Estado do AP, rádio, PoE | **API do controller** | Vendor-specific (UniFi via F18) |
| Status da interface | **SNMP** (ifOperStatus) | Universal |

---

## C. Clientes Conectados

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Número de clientes | Total de clientes conectados | ⚠️ Parcial (UniFi integration F18) | — |
| Lista de clientes | MAC, IP, SSID, RSSI | ⚠️ Parcial (UniFi integration F18) | — |
| Clientes por rádio | 2.4GHz vs 5GHz | ❌ Não modelado | **NOVO** |
| Clientes por SSID | Distribuição por rede | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**UniFi Controller API** (já implementado em F18):

| Dado | Endpoint | Descrição |
|------|----------|-----------|
| Clientes ativos | `/api/s/<site>/stat/sta` | Lista de clientes conectados |
| Clientes por AP | `/api/s/<site>/stat/ap/<id>` | Clientes específicos do AP |

**SNMP (limitado)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| dot11NumberOfMobileStations | `1.3.6.1.2.1.80.1.1.3.1.<ifIndex>` | Número de estações móveis (IEEE 802.11 MIB) |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Número de clientes | **API do controller** (F18) | UniFi |
| Lista de clientes | **API do controller** (F18) | UniFi |
| Clientes por rádio/SSID | **API do controller** | Vendor-specific |

---

## D. SSIDs e Canais

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| SSIDs configurados | Lista de redes Wi-Fi | ❌ Não modelado | **NOVO** |
| Canais em uso | Canais 2.4GHz/5GHz | ❌ Não modelado | **NOVO** |
| Largura de canal | 20MHz/40MHz/80MHz | ❌ Não modelado | **NOVO** |
| BSSID | MAC de cada SSID | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**UniFi Controller API**:

| Dado | Endpoint | Descrição |
|------|----------|-----------|
| SSIDs | `/api/s/<site>/rest/wlanconf` | Configuração de WLANs |
| Canais | `/api/s/<site>/stat/ap/<id>` | Canal por rádio |

**SNMP (IEEE 802.11 MIB)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| dot11DesiredSSID | `1.3.6.1.2.1.80.1.1.3.2.<ifIndex>` | SSID desejado |
| dot11CurrentChannel | `1.3.6.1.2.1.80.1.1.3.4.<ifIndex>` | Canal atual |
| dot11CurrentTxPowerLevel | `1.3.6.1.2.1.80.1.1.3.5.<ifIndex>` | Potência de transmissão |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| SSIDs, canais, largura | **API do controller** | Vendor-specific (UniFi via F18) |
| BSSID | **SNMP** (ifPhysAddress) | Universal |

---

## E. Rádio e Sinal

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Utilização de rádio | % de utilização do canal | ❌ Não modelado | **NOVO** |
| Interferência | Nível de interferência | ❌ Não modelado | **NOVO** |
| Sinal médio dos clientes | RSSI médio | ❌ Não modelado | **NOVO** |
| Ruído de fundo | Noise floor | ❌ Não modelado | **NOVO** |
| Taxa de erro | CRC/FCS errors | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**UniFi Controller API**:

| Dado | Endpoint | Descrição |
|------|----------|-----------|
| Utilização de rádio | `/api/s/<site>/stat/ap/<id>` | % de utilização por rádio |
| Sinal dos clientes | `/api/s/<site>/stat/sta` | RSSI por cliente |

**SNMP (IEEE 802.11 MIB)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| dot11RSSI | `1.3.6.1.2.1.80.1.1.3.6.<ifIndex>` | RSSI |
| dot11PrivacyOptionImplemented | `1.3.6.1.2.1.80.1.1.3.7.<ifIndex>` | WEP/WPA implementado |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Utilização de rádio, sinal | **API do controller** | Vendor-specific (UniFi via F18) |
| Interferência, ruído | **API do controller** | Vendor-specific (quando disponível) |

---

## F. Tráfego e Alimentação PoE

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Tráfego entrada | Bytes/s | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |
| Tráfego saída | Bytes/s | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |
| Alimentação PoE | Status PoE | ❌ Não modelado | **NOVO** |
| Potência PoE | Watts consumidos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**Universal (ifTable/ifXTable)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| ifHCInOctets | `1.3.6.1.2.1.31.1.1.1.6.<ifIndex>` | Tráfego entrada (64-bit) |
| ifHCOutOctets | `1.3.6.1.2.1.31.1.1.1.10.<ifIndex>` | Tráfego saída (64-bit) |

**PoE (vendor-specific)**:

| Dado | UniFi | HP | Aruba |
|------|-------|----|-------|
| PoE status | Via controller API | Vendor-specific | Vendor-specific |
| PoE power | Via controller API | Vendor-specific | Vendor-specific |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Tráfego | **SNMP** (ifTable/ifXTable) | Já implementado em F29-3 |
| PoE status/potência | **API do controller** | Vendor-specific (UniFi via F18) |

---

## G. Histórico e Prevenção

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Tendência de utilização | Rádio ao longo do tempo | ❌ Não modelado | **NOVO** (prevenção) |
| Degradação de sinal | Sinal caindo ao longo do tempo | ❌ Não detectado | **NOVO** (prevenção) |
| Aumento de clientes | Clientes crescendo | ❌ Não detectado | **NOVO** (prevenção) |
| Interferência crescente | Interferência aumentando | ❌ Não detectado | **NOVO** (prevenção) |

### Modelo de Manutenção Preventiva (proposto)

| Problema | Sinal | Antecipação | Ação |
|----------|-------|-------------|------|
| Saturação de rádio | Utilização > 80% | Rádio > 70% e tendência ascendente | Adicionar AP, ajustar canais |
| Interferência alta | Interferência > limiar | Interferência crescente por X minutos | Mudar canal, investigar fontes |
| Sinal degradando | RSSI médio caindo | Tendência descendente de RSSI | Investigar obstáculos, reposicionar AP |
| Muitos clientes no mesmo canal | Densidade alta por canal | Clientes por canal > Y | Balancear carga, ajustar canais |

---

## Métricas que Precisam de Histórico

| Métrica | Período de retenção | Resolução | Justificativa |
|---------|-------------------|-----------|---------------|
| Utilização de rádio | 30 dias | 5 minutos | Detecção de saturação |
| Número de clientes | 30 dias | 5 minutos | Análise de capacidade |
| Sinal médio (RSSI) | 30 dias | 5 minutos | Detecção de degradação |
| Tráfego | 90 dias | 5 minutos | Análise de capacidade |
| Uptime | Permanente | Evento (boot) | Detecção de reboots |

**Nota**: O Zabbix pode manter histórico numérico. O InfraControl deve:
- Consultar o Zabbix para histórico detalhado (via API)
- Persistir apenas eventos agregados e tendências no PostgreSQL (REGRA 8 de ADR-028)

---

## Condições de Alerta

### Alertas reativos (existentes)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| AP não responde a ping | Critical | ✅ Ping check |
| Interface down | Warning | ✅ ifOperStatus via SNMP |

### Alertas preventivos (novos)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Utilização de rádio > 80% | Warning | ❌ Novo |
| Utilização de rádio > 90% | Critical | ❌ Novo |
| Clientes > limiar (ex: 50) | Warning | ❌ Novo |
| Sinal médio < limiar (ex: -70 dBm) | Warning | ❌ Novo |
| Interferência alta | Warning | ❌ Novo |
| PoE falhando | Critical | ❌ Novo |

---

## Operações Administrativas Permitidas

### Escopo inicial (somente monitoramento)

| Operação | Fonte | Implementação | Status |
|----------|-------|----------------|--------|
| Consultar estado atual | SNMP/API | Provider novo | ⚠️ A implementar |
| Consultar clientes conectados | API do controller (F18) | UniFi integration | ✅ Implementado |
| Consultar histórico de métricas | Zabbix API | Query Zabbix para histórico | ⚠️ Parcial |

### Operações futuras (fora escopo inicial)

| Operação | Justificativa | Pré-requisitos |
|----------|---------------|----------------|
| Reiniciar AP | Operação crítica | SSH/power control |
| Ajustar canal/SSID | Operação crítica | Integração controller |
| Bloquear cliente | Operação crítica | Integração controller |

---

## Tratamento de Dados Indisponíveis, Erros e Credenciais

### Dados indisponíveis

| Situação | Comportamento | Mensagem ao usuário |
|-----------|---------------|---------------------|
| AP offline | Estado = Unknown, Cache último valor conhecido | "AP offline. Última atualização: [timestamp]" |
| Controller indisponível | Estado = Degraded, Cache último valor conhecido | "Controller indisponível. Dados em cache." |
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
| Controller API (UniFi) | Environment variable | Redação total em DTOs |

---

## Estratégia de Implementação

### Abordagem por vendor

**1. UniFi (Ubiquiti) - Prioridade Alta**:
- Reutilizar integração existente (F18)
- Extender para métricas de rádio, utilização, sinal
- API já disponível e documentada

**2. SNMP universal - Prioridade Média**:
- IEEE 802.11 MIB para SSIDs, canais, rádio
- ifTable/ifXTable para tráfego
- Funciona para maioria dos fabricantes

**3. Vendor-specific opcional**:
- Ruckus, Aruba, TP-Link via API própria
- Fallback seguro: se vendor não suportado, usar SNMP universal

### Prioridade de Implementação

| Ordem | Item | Justificativa |
|-------|------|---------------|
| 1 | **Extender UniFi integration** (rádio, utilização, sinal) | Reutilizar código existente (F18) |
| 2 | **SnmpRadioProvider** (IEEE 802.11 MIB) | Universal SSIDs, canais |
| 3 | **SnmpApHealthProvider** (estado do AP, rádio) | SNMP ifOperStatus |
| 4 | **Modelo de histórico/prevenção** | Limiar, tendência, saturação |
| 5 | **Vendor-specific APIs** (Ruckus, Aruba, TP-Link) | Apenas se necessário |

---

## Estratégia de Teste

### Ambiente de homologação

| Requisito | Implementação |
|-----------|---------------|
| AP real para testes | AP em laboratório (não produção) |
| Controller UniFi | Controller de teste (F18 já configurado) |
| Comunidade SNMP liberada | Read-only community configurada |
| Acesso Zabbix | Host criado no Zabbix de teste |
| Dados variados | Conectar dispositivos, gerar tráfego, mover clientes |

### Testes automatizados

| Tipo | Escopo |
|------|--------|
| Unit | Providers SNMP (SnmpRadioProvider, novos providers) |
| Integration | Repository + Query com PostgreSQL |
| E2E | Ponta-a-ponta com AP real (gate de infra) |
| Mock | Simular respostas SNMP/API para cenários de erro |

---

## Próximos Passos (Recomendação)

1. **Prioridade imediata**:
   - Extender UniFi integration para métricas de rádio, utilização, sinal
   - Criar `SnmpRadioProvider` (IEEE 802.11 MIB - SSIDs, canais)
   - Criar `SnmpApHealthProvider` (estado do AP, rádio)

2. **Prioridade média**:
   - Implementar modelo de histórico (tendência de utilização, sinal)
   - Implementar modelo de prevenção (limiar, saturação, interferência)
   - Adicionar `Device.Criticality` para APs

3. **Prioridade baixa**:
   - Vendor-specific APIs (Ruckus, Aruba, TP-Link)
   - Monitoramento de interferência detalhado
   - Operações administrativas (reboot, ajuste de canal)

---

## Referências

- IEEE 802.11 MIB: `1.3.6.1.2.1.80`
- ADR-028: Arquitetura da Camada de Integrações
- ADR-048: Identidade Única de Device, Zabbix×UniFi via API
- F2-T008: DeviceProfile
- F18-T001: Integração UniFi SDN
- UniFi API Documentation: https://ui.com/developer
