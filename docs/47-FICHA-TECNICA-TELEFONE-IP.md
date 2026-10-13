# Ficha Técnica: Telefone IP

> **Status**: Rascunho (2026-10-13)
> **Contexto**: Especificação Operations Center - Sexto dispositivo a ser estudado
> **Referência**: Especificação do usuário (seção 2 - "Quais dispositivos devemos estudar?")

---

## Visão Geral

O Telefone IP é o sexto caso de estudo porque:
- É crítico para comunicação de voz (VoIP)
- Requer monitoramento de registro SIP, qualidade de chamada e estado
- Problemas de telefone impactam diretamente a comunicação
- A maioria dos telefones IP oferece APIs (Yealink, Cisco, Polycom, Grandstream, etc.)

**Áreas de foco** (baseadas na especificação):
- A. Identificação e configuração
- B. Estado e disponibilidade
- C. Registro SIP
- D. Qualidade de chamada
- E. Rede e alimentação PoE
- F. Histórico e prevenção

---

## A. Identificação e Configuração

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Nome | Nome do telefone | ✅ `Device.Name` | — |
| Fabricante | Vendor (Yealink, Cisco, Polycom, Grandstream, etc.) | ✅ `Device.Vendor` | — |
| Modelo | Modelo específico | ✅ `Device.Model` | — |
| Número de série | Serial number | ✅ `Device.SerialNumber` | — |
| Firmware | Versão do firmware | ❌ Não persistido | **NOVO** |
| IP de gestão | Endereço IP de acesso | ✅ `Device.Ip` | — |
| Mac Address | MAC do telefone | ✅ `NetworkInterface.MacAddress` | — |
| Extension | Número da extensão | ❌ Não modelado | **NOVO** |
| Servidor SIP | PBX/SIP server | ❌ Não modelado | **NOVO** |
| Localização | Local físico (sala, andar) | ✅ `Device.LocationId` → `Location` | — |
| Tempo de atividade (Uptime) | Tempo desde o último boot | ⚠️ Só via SNMP sob demanda | Persistir no Device |
| Referência Zabbix | Host ID no Zabbix | ✅ `ExternalDeviceReference` (Source=Zabbix) | — |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | OID / Endpoint | Disponibilidade |
|------|-------|---------------|-----------------|
| Nome (sysName) | SNMP | `1.3.6.1.2.1.1.5.0` | Universal (MIB-II) |
| Firmware versão | SNMP | `1.3.6.1.2.1.1.1.0` (sysDescr) | Universal (contém versão) |
| Uptime (sysUpTime) | SNMP | `1.3.6.1.2.1.1.3.0` | Universal (MIB-II) |
| MAC address | SNMP | `1.3.6.1.2.1.2.2.1.6.<ifIndex>` | Universal (ifPhysAddress) |
| Extension | API do telefone | Vendor-specific | Requer integração |
| Servidor SIP | API do telefone | Vendor-specific | Requer integração |

### Fonte de cada dado

| Dado | Fonte Primária | Fonte Secundária | Observação |
|------|----------------|------------------|------------|
| Nome, IP, Localização | **InfraControl** (Device) | — | System of Record |
| Firmware, Uptime | **SNMP** (sob demanda) | Zabbix (histórico) | Persistir no Device |
| Extension, Servidor SIP | **API do telefone** | — | Vendor-specific |
| Referência Zabbix | **Zabbix sync** (F29-1) | — | ExternalDeviceReference |

### Campos do cadastro

| Campo | Obrigatório? | Padrão | Validação |
|-------|--------------|--------|-----------|
| Nome | ✅ Sim | — | Único global, case-insensitive |
| Tipo | ✅ Sim | Phone | Enum fixo |
| Fabricante | ⚠️ Opcional | — | Dropdown de vendors conhecidos |
| Modelo | ⚠️ Opcional | — | Texto livre |
| Número de série | ❌ Opcional | — | Texto livre |
| IP de gestão | ✅ Sim | — | IP válido, único global (conflito detector F29-4) |
| Localização | ✅ Sim | — | FK para Location |
| Extension | ❌ Opcional | — | Texto livre (número da extensão) |
| Servidor SIP | ❌ Opcional | — | Texto livre (IP/hostname do PBX) |
| Comunidade SNMP | ⚠️ Opcional | — | Config de integração (por device ou global) |

---

## B. Estado e Disponibilidade

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Online/Offline | Estado de conectividade | ✅ Ping check (DeviceProfileDefaults) | — |
| Estado do telefone | Idle/Ringing/In Call/Offline | ❌ Não modelado | **NOVO** |
| Estado da alimentação PoE | Status PoE | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**SNMP por fabricante**:

| Dado | Yealink | Cisco | Polycom | Grandstream |
|------|---------|-------|---------|-------------|
| Phone status | Vendor-specific | Vendor-specific | Vendor-specific | Vendor-specific |
| PoE status | Vendor-specific | Vendor-specific | Vendor-specific | Vendor-specific |

**Universal (ifTable)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| ifOperStatus | `1.3.6.1.2.1.2.2.1.8.<ifIndex>` | Status da interface (up/down) |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Online/Offline | **Ping** (existente) | DeviceProfileDefaults |
| Estado do telefone (Idle/Ringing/In Call) | **API do telefone** | Vendor-specific |
| Status da interface | **SNMP** (ifOperStatus) | Universal |
| PoE status | **API do telefone** | Vendor-specific |

---

## C. Registro SIP

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Status do registro SIP | Registered/Unregistered | ❌ Não modelado | **NOVO** |
| Servidor SIP | PBX/SIP server | ❌ Não modelado | **NOVO** |
| Extensão | Número da extensão | ❌ Não modelado | **NOVO** |
| Codec preferido | G.711, G.722, G.729, etc. | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**API por fabricante**:

| Dado | Yealink | Cisco | Polycom | Grandstream |
|------|---------|-------|---------|-------------|
| Registration status | `/api/phone/line` | Vendor-specific | Vendor-specific | `/api/call_status` |
| SIP server | `/api/phone/line` | Vendor-specific | Vendor-specific | `/api/call_status` |
| Extension | `/api/phone/line` | Vendor-specific | Vendor-specific | `/api/call_status` |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Status do registro SIP | **API do telefone** | Vendor-specific |
| Servidor SIP, Extension | **API do telefone** | Vendor-specific |

---

## D. Qualidade de Chamada

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| MOS (Mean Opinion Score) | Qualidade da chamada (1-5) | ❌ Não modelado | **NOVO** |
| Jitter | Variação de latência (ms) | ❌ Não modelado | **NOVO** |
| Latência (RTT) | Round-trip time (ms) | ❌ Não modelado | **NOVO** |
| Perda de pacotes | Packet loss % | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**API por fabricante**:

| Dado | Yealink | Cisco | Polycom | Grandstream |
|------|---------|-------|---------|-------------|
| MOS | `/api/phone/call/quality` | Vendor-specific | Vendor-specific | `/api/call_quality` |
| Jitter | `/api/phone/call/quality` | Vendor-specific | Vendor-specific | `/api/call_quality` |
| Latência | `/api/phone/call/quality` | Vendor-specific | Vendor-specific | `/api/call_quality` |
| Packet loss | `/api/phone/call/quality` | Vendor-specific | Vendor-specific | `/api/call_quality` |

**Nota**: A maioria dessas métricas só está disponível durante uma chamada ativa.

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| MOS, Jitter, Latência, Packet loss | **API do telefone** | Vendor-specific (apenas durante chamada) |

---

## E. Rede e Alimentação PoE

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

| Dado | Yealink | Cisco | Polycom | Grandstream |
|------|---------|-------|---------|-------------|
| PoE status | `/api/phone/status` | Vendor-specific | Vendor-specific | `/api/poe_status` |
| PoE power | `/api/phone/status` | Vendor-specific | Vendor-specific | `/api/poe_status` |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Tráfego | **SNMP** (ifTable/ifXTable) | Já implementado em F29-3 |
| PoE status/potência | **API do telefone** | Vendor-specific |

---

## F. Histórico e Prevenção

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Tendência de qualidade | MOS ao longo do tempo | ❌ Não modelado | **NOVO** (prevenção) |
| Perda de pacotes crescente | Packet loss aumentando | ❌ Não detectado | **NOVO** (prevenção) |
| Registro SIP falhando | Unregistered recorrente | ❌ Não detectado | **NOVO** (prevenção) |
| Chamadas com baixa qualidade | MOS < limiar | ❌ Não detectado | **NOVO** (prevenção) |

### Modelo de Manutenção Preventiva (proposto)

| Problema | Sinal | Antecipação | Ação |
|----------|-------|-------------|------|
| Qualidade degradando | MOS < 3.5 por X chamadas | MOS caindo ao longo do tempo | Investigar rede, jitter, latência |
| Perda de pacotes alta | Packet loss > 5% | Packet loss crescente | Investigar congestionamento, cabo |
| Registro SIP falhando | Unregistered por X minutos | Falha de registro recorrente | Investigar PBX, rede, credenciais |
| PoE instável | PoE flutuando | PoE status mudando frequentemente | Investigar switch, alimentação |

---

## Métricas que Precisam de Histórico

| Métrica | Período de retenção | Resolução | Justificativa |
|---------|-------------------|-----------|---------------|
| Status do registro SIP | 30 dias | Evento | Análise de disponibilidade |
| MOS | 30 dias | Evento (chamada) | Análise de qualidade |
| Jitter | 30 dias | Evento (chamada) | Análise de qualidade |
| Latência | 30 dias | Evento (chamada) | Análise de qualidade |
| Packet loss | 30 dias | Evento (chamada) | Análise de qualidade |
| Uptime | Permanente | Evento (boot) | Detecção de reboots |

**Nota**: O Zabbix pode manter histórico numérico. O InfraControl deve:
- Consultar o Zabbix para histórico detalhado (via API)
- Persistir apenas eventos agregados e tendências no PostgreSQL (REGRA 8 de ADR-028)

---

## Condições de Alerta

### Alertas reativos (existentes)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Telefone não responde a ping | Critical | ✅ Ping check |
| Interface down | Warning | ✅ ifOperStatus via SNMP |

### Alertas preventivos (novos)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Registro SIP falhando (> X minutos) | Critical | ❌ Novo |
| MOS < 3.0 | Critical | ❌ Novo |
| MOS < 3.5 | Warning | ❌ Novo |
| Jitter > 50ms | Warning | ❌ Novo |
| Latência > 150ms | Warning | ❌ Novo |
| Packet loss > 5% | Warning | ❌ Novo |
| Packet loss > 10% | Critical | ❌ Novo |
| PoE falhando | Critical | ❌ Novo |

---

## Operações Administrativas Permitidas

### Escopo inicial (somente monitoramento)

| Operação | Fonte | Implementação | Status |
|----------|-------|----------------|--------|
| Consultar estado atual | SNMP/API | Provider novo | ⚠️ A implementar |
| Consultar registro SIP | API do telefone | Provider novo | ⚠️ A implementar |
| Consultar qualidade de chamada | API do telefone | Provider novo | ⚠️ A implementar |
| Consultar histórico de métricas | Zabbix API | Query Zabbix para histórico | ⚠️ Parcial |

### Operações futuras (fora escopo inicial)

| Operação | Justificativa | Pré-requisitos |
|----------|---------------|----------------|
| Reiniciar telefone | Operação crítica | SSH/power control |
| Ajustar codec | Operação crítica | Integração API |
| Transferir chamada | Operação crítica | Integração API |

---

## Tratamento de Dados Indisponíveis, Erros e Credenciais

### Dados indisponíveis

| Situação | Comportamento | Mensagem ao usuário |
|-----------|---------------|---------------------|
| Telefone offline | Estado = Unknown, Cache último valor conhecido | "Telefone offline. Última atualização: [timestamp]" |
| API indisponível | Estado = Degraded, Cache último valor conhecido | "API indisponível. Dados em cache." |
| SNMP community incorreta | Erro de autenticação, Log warn | "Erro de autenticação SNMP. Verifique community string." |
| API não suportado | Campo = null, Log info | "API não disponível neste modelo." |

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
- ifOperStatus para estado da interface
- Funciona para maioria dos fabricantes

**2. Vendor-specific APIs - Prioridade Média**:
- Yealink API (documentada, relativamente padrão)
- Cisco API (vendor-specific)
- Polycom API (vendor-specific)
- Grandstream API (documentada)
- Fallback seguro: se vendor não suportado, usar SNMP universal

### Modelo de domínio (proposto)

```csharp
// Extension para Device (somente quando DeviceType == Phone)
public class PhoneSipRegistration
{
    public Guid DeviceId { get; }
    public DateTime CapturedAt { get; }
    public string Extension { get; }
    public string SipServer { get; }
    public SipRegistrationStatus Status { get; } // Registered/Unregistered
    public string Codec { get; }
}

public class PhoneCallQuality
{
    public Guid DeviceId { get; }
    public DateTime CapturedAt { get; }
    public decimal? Mos { get; }
    public decimal? JitterMs { get; }
    public decimal? LatencyMs { get; }
    public decimal? PacketLossPercent { get; }
}
```

### Prioridade de Implementação

| Ordem | Item | Justificativa |
|-------|------|---------------|
| 1 | **SnmpPhoneProvider** (tráfego, estado interface) | SNMP universal |
| 2 | **YealinkPhoneProvider** (registro SIP, qualidade de chamada) | Yealink API documentada |
| 3 | **Modelo de domínio** (PhoneSipRegistration, PhoneCallQuality) | Persistir dados |
| 4 | **Modelo de histórico/prevenção** | Limiar, tendência, qualidade |
| 5 | **Vendor-specific APIs** (Cisco, Polycom, Grandstream) | Apenas se necessário |

---

## Estratégia de Teste

### Ambiente de homologação

| Requisito | Implementação |
|-----------|---------------|
| Telefone IP real para testes | Telefone em laboratório (não produção) |
| API habilitada | API ativada no dispositivo |
| Comunidade SNMP liberada | Read-only community configurada |
| Acesso Zabbix | Host criado no Zabbix de teste |
| Dados variados | Fazer chamadas, testar registro SIP, variar qualidade |

### Testes automatizados

| Tipo | Escopo |
|------|--------|
| Unit | Providers SNMP/API (SnmpPhoneProvider, YealinkPhoneProvider) |
| Integration | Repository + Query com PostgreSQL |
| E2E | Ponta-a-ponta com telefone real (gate de infra) |
| Mock | Simular respostas SNMP/API para cenários de erro |

---

## Próximos Passos (Recomendação)

1. **Prioridade imediata**:
   - Criar `SnmpPhoneProvider` (tráfego, estado interface)
   - Criar `YealinkPhoneProvider` (registro SIP, qualidade de chamada)
   - Criar modelo de domínio (PhoneSipRegistration, PhoneCallQuality)

2. **Prioridade média**:
   - Implementar modelo de histórico (tendência de qualidade)
   - Implementar modelo de prevenção (limiar, MOS baixo, packet loss)
   - Adicionar `Device.Criticality` para telefones

3. **Prioridade baixa**:
   - Vendor-specific APIs (Cisco, Polycom, Grandstream)
   - Monitoramento de qualidade em tempo real
   - Operações administrativas (reboot, ajuste de codec)

---

## Referências

- ADR-028: Arquitetura da Camada de Integrações
- ADR-048: Identidade Única de Device, Zabbix×UniFi via API
- F2-T008: DeviceProfile
- Yealink API Documentation: https://www.yealink.com/
- Grandstream API Documentation: https://www.grandstream.com/
