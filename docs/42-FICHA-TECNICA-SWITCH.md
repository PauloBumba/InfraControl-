# Ficha Técnica: Switch

> **Status**: Rascunho (2026-10-13)
> **Contexto**: Especificação Operations Center - Primeiro dispositivo a ser estudado
> **Referência**: Especificação do usuário (seção 3 - "Vamos estudar primeiro o switch")
> **Ambiente real**: UniFi (Ubiquiti), Intelbras, HP, Aruba, TP-Link

---

## Visão Geral

O switch é o primeiro caso de estudo porque:
- Liga vários equipamentos e fornece informação importante para descobrir a topologia
- É crítico para diagnóstico de problemas de rede
- Já existe infraestrutura parcial implementada (SNMP básico, topologia LLDP/CDP)

**Ambiente de fabricantes** (multivendor):
- **UniFi (Ubiquiti)**: Dados via controller API (F18) + SNMP limitado
- **Intelbras**: SNMP com OIDs proprietários (enterprise `13464` / `26138`)
- **HP**: SNMP com OIDs HPE/Aruba (enterprise `11`)
- **Aruba**: SNMP com OIDs AOS-CX (enterprise `47196`)
- **TP-Link**: SNMP com OIDs TPLINK-SYSMONITOR (enterprise `11863`)

**Áreas de foco** (conforme especificação):
- A. Identificação e configuração
- B. Portas e conectividade
- C. Saúde do equipamento
- D. Topologia e equipamentos ligados
- E. Gestão e acesso administrativo
- F. Histórico, alertas e prevenção

---

## A. Identificação e Configuração

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Nome | Nome do switch (hostname) | ✅ `Device.Name` | — |
| Fabricante | Vendor (Cisco, HP, Dell, etc.) | ✅ `Device.Vendor` | — |
| Modelo | Modelo específico | ✅ `Device.Model` | — |
| Número de série | Serial number | ✅ `Device.SerialNumber` | — |
| Firmware | Versão do firmware/OS | ❌ Não persistido | **NOVO** |
| IP de gestão | Endereço IP de acesso | ✅ `Device.Ip` | — |
| Localização | Local físico (sala, rack) | ✅ `Device.LocationId` → `Location` | — |
| Rack | Rack onde está instalado | ✅ `Device.RackId` → `Rack` | — |
| Tempo de atividade (Uptime) | Tempo desde o último boot | ⚠️ Só via SNMP sob demanda | Persistir no Device |
| Versão suportada | Capacidades do modelo | ❌ Não modelado | Futuro |
| Referência Zabbix | Host ID no Zabbix | ✅ `ExternalDeviceReference` (Source=Zabbix) | — |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | OID / Endpoint | Disponibilidade |
|------|-------|---------------|-----------------|
| Nome (sysName) | SNMP | `1.3.6.1.2.1.1.5.0` | Universal (MIB-II) |
| Uptime (sysUpTime) | SNMP | `1.3.6.1.2.1.1.3.0` | Universal (MIB-II) |
| Firmware versão | SNMP | `1.3.6.1.2.1.1.1.0` (sysDescr) | Universal (contém versão) |
| Serial | SNMP | Vendor-specific (ex: Cisco `1.3.6.1.2.1.47.1.1.1.1.11`) | Varia por fabricante |
| CPU utilization | SNMP | Cisco: `1.3.6.1.4.1.9.2.1.56.0`<br>HP: `1.3.6.1.4.1.11.2.14.11.5.1.9.6.1.0` | Vendor-specific |
| Memória livre | SNMP | Cisco: `1.3.6.1.4.1.9.2.1.8.0`<br>HP: `1.3.6.1.4.1.11.2.14.11.5.1.1.2.1.1.1.6.1` | Vendor-specific |

### Fonte de cada dado

| Dado | Fonte Primária | Fonte Secundária | Observação |
|------|----------------|------------------|------------|
| Nome, IP, Localização, Rack | **InfraControl** (Device) | — | System of Record |
| Firmware, Uptime | **SNMP** (sob demanda) | Zabbix (histórico) | Persistir no Device |
| CPU, Memória | **SNMP** (sob demanda) | Zabbix (histórico) | Check novo `SnmpSystemHealth` |
| Referência Zabbix | **Zabbix sync** (F29-1) | — | ExternalDeviceReference |

### Campos do cadastro

| Campo | Obrigatório? | Padrão | Validação |
|-------|--------------|--------|-----------|
| Nome | ✅ Sim | — | Único global, case-insensitive |
| Tipo | ✅ Sim | Switch | Enum fixo |
| Fabricante | ⚠️ Opcional | — | Dropdown de vendors conhecidos |
| Modelo | ⚠️ Opcional | — | Texto livre |
| Número de série | ❌ Opcional | — | Texto livre |
| IP de gestão | ✅ Sim | — | IP válido, único global (conflito detector F29-4) |
| Localização | ✅ Sim | — | FK para Location |
| Rack | ❌ Opcional | — | FK para Rack (exige localização) |
| Comunidade SNMP | ⚠️ Opcional | — | Config de integração (por device ou global) |

---

## B. Portas e Conectividade

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Número de portas | Quantidade total | ⚠️ Derivado de `SwitchPort` | — |
| Estado da porta | Up/Down/Unknown | ✅ `SwitchPort.LinkUp?` (nullable) | Melhorar (enum explícito) |
| Velocidade | 10M/100M/1G/10G | ❌ Não persistido | **NOVO** |
| Duplex | Full/Half/Auto | ❌ Não persistido | **NOVO** |
| Tráfego entrada | Bytes/s | ✅ SNMP ifHCInOctets (F29-3) | Sob demanda |
| Tráfego saída | Bytes/s | ✅ SNMP ifHCOutOctets (F29-3) | Sob demanda |
| Erros entrada | Contador | ✅ SNMP ifInErrors (F29-3) | Sob demanda |
| Erros saída | Contador | ✅ SNMP ifOutErrors (F29-3) | Sob demanda |
| Descartes | Contador | ❌ Não coletado | Future |
| Flaps | Oscilações up/down | ❌ Não detectado | **NOVO** (prevenção) |
| VLAN | Lista de VLANs | ✅ `SwitchPortVlan` | — |
| Trunk/Access | Modo da porta | ❌ Não modelado | **NOVO** |
| Agregações (LACP) | Port-channels | ❌ Não modelado | Future |
| PoE | Status PoE | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | OID / Endpoint | Disponibilidade |
|------|-------|---------------|-----------------|
| Estado porta (ifOperStatus) | SNMP | `1.3.6.1.2.1.2.2.1.8.<ifIndex>` | Universal (MIB-II) |
| Velocidade (ifSpeed) | SNMP | `1.3.6.1.2.1.2.2.1.5.<ifIndex>` | Universal (MIB-II) |
| Duplex (ifDuplex) | SNMP | `1.3.6.1.2.1.31.1.1.1.7.<ifIndex>` | ifXTable (RFC 2863) |
| Tráfego entrada (ifHCInOctets) | SNMP | `1.3.6.1.2.1.31.1.1.1.6.<ifIndex>` | ifXTable (F29-3 já coleta) |
| Tráfego saída (ifHCOutOctets) | SNMP | `1.3.6.1.2.1.31.1.1.1.10.<ifIndex>` | ifXTable (F29-3 já coleta) |
| Erros entrada (ifInErrors) | SNMP | `1.3.6.1.2.1.2.2.1.14.<ifIndex>` | ifTable (F29-3 já coleta) |
| Erros saída (ifOutErrors) | SNMP | `1.3.6.1.2.1.2.2.1.20.<ifIndex>` | ifTable (F29-3 já coleta) |
| Descartes (ifInDiscards/ifOutDiscards) | SNMP | `1.3.6.1.2.1.2.2.1.13/19.<ifIndex>` | ifTable (não coletado) |
| VLAN nativa | SNMP | Vendor-specific (Cisco: `1.3.6.1.4.1.9.9.68.1.2.2.1.2`) | Vendor-specific |
| Trunk/Access | SNMP | Vendor-specific | Vendor-specific |
| PoE status | SNMP | Vendor-specific (Cisco: `1.3.6.1.4.1.9.9.402.1.1`) | Vendor-specific |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Nome da porta, Descrição | **InfraControl** (`SwitchPort`) | Cadastrado manualmente ou importado |
| Estado porta (up/down) | **SNMP** (ifOperStatus) | Coletado sob demanda via `SnmpInterfacesMonitoringProvider` |
| Velocidade, Duplex | **SNMP** (ifSpeed/ifDuplex) | Provider novo `SnmpPortDetails` |
| Tráfego, Erros | **SNMP** (ifHC/ifXTable) | Já implementado em F29-3 |
| VLANs | **InfraControl** (`SwitchPortVlan`) | Cadastrado manualmente |
| Trunk/Access, PoE | **SNMP** (vendor-specific) | Provider custom por vendor |

### Campos do cadastro (SwitchPort)

| Campo | Obrigatório? | Padrão | Validação |
|-------|--------------|--------|-----------|
| DeviceId | ✅ Sim | — | FK para Device (tipo Switch) |
| Nome | ✅ Sim | — | Único por switch, case-insensitive |
| Descrição | ❌ Opcional | — | Texto livre ≤500 |
| LinkUp | ❌ Opcional | null | Enum (Up/Down/Unknown - derivado SNMP) |
| Velocidade | ❌ Opcional | — | Enum (10M/100M/1G/10G/40G/100G/Auto) |
| Duplex | ❌ Opcional | — | Enum (Full/Half/Auto) |
| PoEEnabled | ❌ Opcional | false | Boolean |
| PoEPowerBudget | ❌ Opcional | — | Watts (int) |

---

## C. Saúde do Equipamento

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| CPU | Utilização % | ⚠️ Parcial (SnmpSystem check genérico) | Provider específico com limiares |
| Memória | Livre/Total % | ⚠️ Parcial (SnmpSystem check genérico) | Provider específico com limiares |
| Temperatura | Temperatura dos componentes | ❌ Não modelado | **NOVO** |
| Ventoinhas (Fans) | Status (normal/warning/critical) | ❌ Não modelado | **NOVO** |
| Fontes de alimentação (PSUs) | Status (OK/Fail/Redundant) | ❌ Não modelado | **NOVO** |
| Estado do hardware | Alarmes internos | ❌ Não modelado | Future |
| Uptime | Tempo desde boot | ⚠️ Só SNMP sob demanda | Persistir no Device |
| Reinicializações | Contagem de reboots | ❌ Não modelado | **NOVO** (prevenção) |

### Dados que o equipamento realmente disponibiliza

**SNMP por fabricante** (OIDs levantados para o ambiente real):

| Dado | UniFi (Ubiquiti) | Intelbras | HP | Aruba | TP-Link |
|------|------------------|-----------|----|-------|---------|
| CPU % | `1.3.6.1.4.1.10002.1.1.1.4.2.1.3.1` (1m avg)<br>`1.3.6.1.4.1.10002.1.1.1.4.2.1.3.2` (5m avg)<br>`1.3.6.1.4.1.10002.1.1.1.4.2.1.3.3` (15m avg) | `1.3.6.1.4.1.13464.1.14.2.1.3.1.0` (OLT)<br>`1.3.6.1.4.1.26138.3.600.9.1.1.3.33.1` (Série 3000) | `1.3.6.1.4.1.11.2.14.11.5.1.9.6.1.0` | `1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.3` (instant)<br>`1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.10` (1m avg)<br>`1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.11` (5m avg) | `1.3.6.1.4.1.11863.6.4.1.1.1.1.2` (5s)<br>`1.3.6.1.4.1.11863.6.4.1.1.1.1.3` (1m)<br>`1.3.6.1.4.1.11863.6.4.1.1.1.1.4` (5m) |
| Memória % | `1.3.6.1.4.1.10002.1.1.1.1.2.0` (free)<br>`1.3.6.1.4.1.10002.1.1.1.1.1.0` (total) | `1.3.6.1.4.1.13464.1.14.2.1.3.2.0` (OLT)<br>`1.3.6.1.4.1.26138.3.600.10.1.1.10.33` (Série 3000) | `1.3.6.1.4.1.11.2.14.11.5.1.1.2.1.1.1.6.1` | `1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.4` | `1.3.6.1.4.1.11863.6.4.1.2.1.1.2` |
| Temperatura | `1.3.6.1.4.1.4413.1.1.43.1.8.1.5.1.0` (sensor 1)<br>`1.3.6.1.4.1.4413.1.1.43.1.8.1.5.1.1` (sensor 2) | `1.3.6.1.4.1.13464.1.14.2.1.3.3.0` (chipset Switch)<br>`1.3.6.1.4.1.26138.3.600.2.1.1.20.33` (Série 3000) | `1.3.6.1.4.1.11.2.14.11.1.2.8.1.1.9.0` | — (via MIBs de hardware) | `1.3.6.1.4.1.11863.6.4.1.3` (OctetString) |
| Fans | — (via controller API) | — | `1.3.6.1.4.1.11.2.14.11.1.2.6.1.4.1` | — (via MIBs de hardware) | — |
| PSUs | — (via controller API) | — | `1.3.6.1.4.1.11.2.14.11.1.2.6.1.4.2/3` | — (via MIBs de hardware) | — |
| Uptime | `1.3.6.1.2.1.1.3.0` (MIB-II) | `1.3.6.1.2.1.1.3.0` (MIB-II) | `1.3.6.1.2.1.1.3.0` (MIB-II) | `1.3.6.1.2.1.1.3.0` (MIB-II) | `1.3.6.1.2.1.1.3.0` (MIB-II) |

**Fontes alternativas por fabricante**:

| Fabricante | Fonte primária | Fonte secundária | Observação |
|------------|----------------|------------------|------------|
| **UniFi** | Controller API (F18) | SNMP limitado | Dados de saúde via controller (telemetria de sites/switches) |
| **Intelbras** | SNMP | — | OIDs variam por linha (OLT vs Série 3000) |
| **HP** | SNMP | — | MIBs HPE comuns a switches gerenciáveis |
| **Aruba** | SNMP | — | MIBs AOS-CX específicas (ARUBAWIRED-SYSTEMINFO-MIB) |
| **TP-Link** | SNMP | — | MIB TPLINK-SYSMONITOR (T1600/T2600 series) |

### Fonte de cada dado

| Dado | Fonte Primária | Fonte Secundária | Observação |
|------|----------------|------------------|------------|
| CPU, Memória | **SNMP** (provider custom) | Zabbix (histórico) | Novo provider `SnmpSystemHealth` |
| Temperatura, Fans, PSUs | **SNMP** (provider custom) | Zabbix (histórico) | Novo provider `SnmpHardwareHealth` |
| Uptime | **SNMP** (sysUpTime) | — | Persistir em `Device.Uptime` |
| Reinicializações | **Detecção** (uptime reset) | — | Evento de domínio quando uptime < anterior |

### Modelo de domínio (proposto)

```csharp
// Extension para Device (somente quando DeviceType == Switch)
public class SwitchHealth
{
    public Guid DeviceId { get; }
    public DateTime CapturedAt { get; }
    public decimal? CpuPercent { get; }
    public decimal? MemoryFreePercent { get; }
    public decimal? TemperatureCelsius { get; }
    public string? FanStatus { get; } // normal/warning/critical
    public string? PsuStatus { get; } // ok/fail/redundant
    public TimeSpan? Uptime { get; }
}
```

---

## D. Topologia e Equipamentos Ligados

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| LLDP neighbors | Vizinhos descobertos | ✅ F29-5 (ghost nodes) | — |
| MAC table | Endereços MAC por porta | ✅ Discovery (ArpEntry/MacTableEntry) | — |
| VLANs | Configuração de VLAN | ✅ Vlan entity + SwitchPortVlan | — |
| Portas inter-switch | Conexões entre switches | ✅ Connection entity (ingestão LLDP) | — |
| Correlação DHCP | Endereços IP por porta | ✅ Discovery (DhcpLease) | — |
| Correlação ARP | IP→MAC | ✅ Discovery (ArpEntry) | — |
| Controladores | UniFi controller | ✅ UniFi integration | — |

**Conclusão**: Esta área está **quando completa**. F29-5 implementou ghost nodes para vizinhos não gerenciados, e a ingestão automática de conexões via LLDP/CDP está funcionando.

---

## E. Gestão e Acesso Administrativo

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Acesso à interface Web | URL de acesso | ❌ Não modelado | Future |
| SSH | Acesso SSH autorizado | ⚠️ Parcial (F11 Credential Store) | **Escopo: credenciais configuradas na plataforma** |
| Versão da configuração | Hash/config backup | ❌ Não modelado | Future |
| Data da última alteração | Última mudança de config | ❌ Não modelado | Future |
| Registo de ações administrativas | Auditoria de config | ❌ Não modelado | Future |

### Decisão de escopo

Conforme especificação atualizada:
> "Acesso remoto deve funcionar para computador, switch e servidor. Credenciais configuradas na plataforma para acesso via SSH."

**Escopo atual**:
- Credenciais SSH podem ser configuradas na plataforma via **Credential Store** (F11-T006)
- Credenciais são armazenadas de forma segura via `ICredentialProtector`
- Acesso SSH autorizado: a plataforma fornece credenciais para acesso remoto (não terminal integrado)
- Monitoramento via SNMP/Zabbix continua sendo a fonte principal de dados
- Terminal SSH integrado fica para fase futura

---

## F. Histórico, Alertas e Prevenção

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Histórico de incidentes | Incidentes relacionados ao switch | ✅ Alert/Incident existem | — |
| Tendências de utilização | Tráfego ao longo do tempo | ❌ Não modelado | **NOVO** (histórico temporal) |
| Erros crescentes | Erros de porta aumentando | ❌ Não detectado | **NOVO** (prevenção) |
| Reinicializações | Contagem de reboots | ❌ Não modelado | **NOVO** (prevenção) |
| Alterações de estado | Mudanças de configuração | ❌ Não modelado | Future |
| Alertas recorrentes | Mesmo alerta repetindo | ❌ Não detectado | **NOVO** (prevenção) |

### Modelo de Manutenção Preventiva (proposto)

Conforme especificação, a lógica preventiva deve considerar:

| Critério | Implementação proposta | Estado |
|----------|------------------------|--------|
| **Limiar** | `ThresholdRule` (tipo de métrica, operador, valor) | ❌ Não existe |
| **Duração** | `DurationRule` (tempo mínimo acima do limiar) | ❌ Não existe |
| **Tendência** | `TrendRule` (derivada, inclinação) | ❌ Não existe |
| **Recorrência** | `RecurrenceRule` (frequência do evento) | ❌ Não existe |
| **Impacto** | `Device.Criticality` (High/Medium/Low) | ❌ Não existe |
| **Confiança** | `DataFreshnessRule` (dados recentes?) | ❌ Não existe |

### Modelo de domínio (proposto)

```csharp
public enum PreventiveAction
{
    InvestigateCable,
    InvestigateTransceiver,
    InvestigateEquipment,
    InspectHardware,
    ScheduleMaintenance,
    None
}

public class PreventiveRule
{
    public string Name { get; }
    public string Metric { get; } // ex: "port_errors_rate"
    public string Operator { get; } // "gt", "lt", "increasing"
    public decimal Threshold { get; }
    public TimeSpan Duration { get; }
    public PreventiveAction SuggestedAction { get; }
}

public class DeviceCriticality
{
    public Guid DeviceId { get; }
    public CriticalityLevel Level { get; } // High/Medium/Low
    public string Justification { get; }
}
```

### Exemplos de regras preventivas (da especificação)

| Sinal observado | Ação preventiva | Regra proposta |
|-----------------|----------------|----------------|
| Erros numa porta aumentam continuamente | Investigar cabo, transceiver ou equipamento ligado | Tendência ascendente de `ifInErrors/ifOutErrors` |
| CPU elevada durante vários períodos | Correlacionar com tráfego, eventos e comportamento do equipamento | Limiar > 80% por 10 minutos |
| Porta oscila entre ligada e desligada | Investigar flaps e gerar incidente se exceder limiar | Flap count > X em Y minutos |
| Reinicializações frequentes | Investigar falha de hardware ou power | Reboot count > X em Y dias |

---

## Métricas que Precisam de Histórico

| Métrica | Período de retenção | Resolução | Justificativa |
|---------|-------------------|-----------|---------------|
| Tráfego de porta (in/out) | 90 dias | 5 minutos | Análise de capacidade, detecção de anomalias |
| Erros de porta | 90 dias | 5 minutos | Tendência de degradação |
| CPU/Memória | 30 dias | 1 minuto | Detecção de sobrecarga |
| Temperatura | 30 dias | 1 minuto | Prevenção de superaquecimento |
| Uptime | Permanente | Evento (boot) | Detecção de reboots |
| Estado de porta (up/down) | 90 dias | Evento | Análise de disponibilidade |

**Nota**: O Zabbix já mantém histórico numérico pesado. O InfraControl deve:
- Consultar o Zabbix para histórico detalhado (via API)
- Persistir apenas eventos agregados e tendências no PostgreSQL (REGRA 8 de ADR-028)

---

## Condições de Alerta

### Alertas reativos (existentes)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Device não responde a ping | Critical | ✅ Ping check |
| Interface down | Warning | ✅ ifOperStatus via SNMP |
| Erros de porta > limiar | Warning | ✅ ifInErrors/ifOutErrors via SNMP |

### Alertas preventivos (novos)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| CPU > 90% por 10 minutos | Warning | ❌ Novo |
| Memória livre < 10% por 5 minutos | Warning | ❌ Novo |
| Temperatura > limiar crítico | Critical | ❌ Novo |
| Fan em estado critical | Critical | ❌ Novo |
| PSU failed | Critical | ❌ Novo |
| Erros de porta crescendo (tendência) | Warning | ❌ Novo |
| Porta flapping (> X transições em Y minutos) | Warning | ❌ Novo |
| Reboot não planejado | Critical | ❌ Novo |

---

## Problemas que Podemos Antecipar

| Problema | Sinal | Antecipação | Ação |
|----------|-------|-------------|------|
| Cabo danificado | Erros crescentes em uma porta | Tendência ascendente de ifInErrors/ifOutErrors | Investigar cabo, transceiver |
| Transceiver falhando | Erros CRC/FCS em porta | Aumento de erros com tráfego baixo | Substituir transceiver |
| Saturação de porta | Tráfego próximo à capacidade | Média de tráfego > 80% da velocidade | Planejar upgrade/agregação |
| Sobrecarga de CPU | CPU elevada sustained | CPU > 80% por 10 minutos | Correlacionar com tráfego, investigar processos |
| Falha de PSU | PSU status failed | SNMP PSU state = failed | Substituir PSU, verificar redundância |
| Superaquecimento | Temperatura > limiar | Temp > limiar warning por 5 minutos | Verificar climatização, ventoinhas |
| Flap de porta | Oscilação up/down | > 5 transições em 5 minutos | Investigar cabo, equipamento ligado |
| Reboot recorrente | Múltiplos reboots | > 2 reboots em 7 dias | Investigar power, hardware, bugs |

---

## Operações Administrativas Permitidas

### Escopo inicial (somente monitoramento)

| Operação | Fonte | Implementação | Status |
|----------|-------|----------------|--------|
| Consultar estado atual | SNMP/Zabbix | `SnmpInterfacesMonitoringProvider` | ✅ Implementado |
| Consultar histórico de métricas | Zabbix API | Query Zabbix para histórico | ⚠️ Parcial |
| Gerar relatório de saúde | Agregação | Dashboard + DeviceDetailPage | ⚠️ Parcial |
| Correlacionar com incidentes | Incident | Incident.DeviceId | ✅ Implementado |

### Operações futuras (fora escopo inicial)

| Operação | Justificativa | Pré-requisitos |
|----------|---------------|----------------|
| Acesso SSH autorizado | Especificação: "não precisamos de implementar logo no início" | F11 - Credential Store |
| Executar comando SNMP SET | Alteração de configuração | Permissões granulares, auditoria |
| Reboot seguro | Operação crítica | ADR específico, rollback |
| Backup de configuração | Preservação de estado | Integração vendor-specific |

---

## Tratamento de Dados Indisponíveis, Erros e Credenciais

### Dados indisponíveis

| Situação | Comportamento | Mensagem ao usuário |
|-----------|---------------|---------------------|
| Device offline | Estado = Unknown, Cache último valor conhecido (com timestamp) | "Device offline. Última atualização: [timestamp]" |
| SNMP community incorreta | Erro de autenticação, Log warn, Alert integrativo | "Erro de autenticação SNMP. Verifique community string." |
| OID não suportado | Campo = null, Log info | "Métrica não disponível neste modelo." |
| Timeout SNMP | Retentativa (Polly), após 3 falhas → Degraded | "Tempo esgotado ao consultar device." |

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
| Zabbix token | Environment variable | Redação total em DTOs |
| SSH (futuro) | Credential Store (F11-T006) | ICredentialProtector |

---

## Estratégia de Implementação Multivendor

### Abordagem de Providers SNMP

Devido à diversidade de fabricantes no ambiente real, a implementação deve seguir:

**1. Abstração por fabricante**:
- Criar uma interface `ISnmpHealthProvider` com métodos `GetCpuAsync`, `GetMemoryAsync`, `GetTemperatureAsync`
- Implementar providers específicos: `IntelbrasSnmpHealthProvider`, `HpSnmpHealthProvider`, `ArubaSnmpHealthProvider`, `TpLinkSnmpHealthProvider`
- UniFi usar a integração controller existente (F18) como fonte primária

**2. Factory pattern baseado em Device.Vendor**:
```csharp
public interface ISnmpHealthProviderFactory
{
    ISnmpHealthProvider? GetProvider(string vendor);
}

// Exemplo:
// vendor == "UniFi" → null (usa controller API)
// vendor == "Intelbras" → IntelbrasSnmpHealthProvider
// vendor == "HP" → HpSnmpHealthProvider
// vendor == "Aruba" → ArubaSnmpHealthProvider
// vendor == "TP-Link" → TpLinkSnmpHealthProvider
// vendor desconhecido → null (métricas indisponíveis)
```

**3. Fallback seguro**:
- Se o provider retornar null (vendor não suportado ou OID falhar), marcar métrica como unavailable
- Não inventar valores zero ou inventados
- Log info sobre vendor/sem suporte

**4. Configuração de OIDs**:
- Mapear OIDs por vendor em configuração (appsettings.json ou tabela de configuração)
- Permitir extensão sem código para novos vendors

### Prioridade de Implementação por Fabricante

| Ordem | Fabricante | Justificativa |
|-------|------------|---------------|
| 1 | **UniFi** | Integração controller já existe (F18) - menor esforço |
| 2 | **Intelbras** | OIDs bem documentados, presença no ambiente real |
| 3 | **HP** | MIBs comuns, vasta documentação |
| 4 | **TP-Link** | OIDs TPLINK-SYSMONITOR claros |
| 5 | **Aruba** | MIBs AOS-CX específicas, validação requer switch real |

---

## Estratégia de Teste

### Ambiente de homologação

| Requisito | Implementação |
|-----------|---------------|
| Switch real para testes | Switch gerenciado em laboratório (não produção) |
| Um switch de cada fabricante | UniFi, Intelbras, HP, Aruba, TP-Link |
| Comunidade SNMP liberada | Read-only community configurada |
| Acesso Zabbix | Host criado no Zabbix de teste |
| Dados variados | Gerar tráfego, flaps, variação de CPU para validar regras |

### Testes automatizados

| Tipo | Escopo |
|------|--------|
| Unit | Providers SNMP (`SnmpInterfacesMonitoringProvider`, novos providers) |
| Integration | Repository + Query com PostgreSQL |
| E2E | Ponta-a-ponta com switch real (gate de infra) |
| Mock | Simular respostas SNMP para cenários de erro |

### Mock data

Cenários a simular:
- Switch com todas as portas up
- Switch com porta down
- Switch com erro crescente em porta
- Switch com CPU elevada
- Switch com temperatura crítica
- Switch flapping

---

## Próximos Passos (Recomendação)

1. **Prioridade imediata**:
   - Implementar campos faltantes em `Device` (Firmware, Uptime)
   - Implementar campos faltantes em `SwitchPort` (Velocidade, Duplex, PoE)
   - Criar abstração `ISnmpHealthProvider` + factory
   - Implementar `UniFiHealthProvider` (reutilizar F18 controller API)
   - Implementar `IntelbrasSnmpHealthProvider` (primeiro vendor SNMP real)

2. **Prioridade média**:
   - Implementar `HpSnmpHealthProvider`
   - Implementar `TpLinkSnmpHealthProvider`
   - Implementar `ArubaSnmpHealthProvider`
   - Implementar modelo de limiar/duração/tendência (manutenção preventiva)
   - Adicionar `Device.Criticality`
   - Implementar detecção de flaps
   - Implementar detecção de reboots

3. **Prioridade baixa**:
   - Integração SSH (F11)
   - Backup de configuração
   - Histórico detalhado de alterações

---

## Referências

- ADR-028: Arquitetura da Camada de Integrações
- ADR-048: Identidade Única de Device, Zabbix×UniFi via API
- F29-3: SNMP mais fundo (interfaces)
- F29-5: Topologia ghost/porta-a-porta
- F2-T008: DeviceProfile
- RFC 2863: ifXTable
- Cisco SNMP OIDs: https://www.cisco.com/c/en/us/support/docs/smb/switches/
- HP ProCurve SNMP: https://helpdesk.paessler.com/
