# Ficha Técnica: Câmeras IP e NVR

> **Status**: Rascunho (2026-10-13)
> **Contexto**: Especificação Operations Center - Quinto dispositivo a ser estudado
> **Referência**: Especificação do usuário (seção 2 - "Quais dispositivos devemos estudar?")

---

## Visão Geral

Câmeras IP e NVR são o quinto caso de estudo porque:
- São críticos para segurança física e monitoramento
- Requerem monitoramento de disponibilidade, gravação e armazenamento
- Problemas de câmera impactam diretamente a segurança
- A maioria das câmeras modernas oferece APIs (ONVIF, Hikvision, Dahua, Axis, etc.)

**Áreas de foco** (baseadas na especificação):
- A. Identificação e configuração
- B. Estado e disponibilidade
- C. Gravação e armazenamento
- D. Eventos e alertas
- E. Qualidade de vídeo
- F. Histórico e prevenção

---

## A. Identificação e Configuração

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Nome | Nome da câmera/NVR | ✅ `Device.Name` | — |
| Fabricante | Vendor (Hikvision, Dahua, Axis, etc.) | ✅ `Device.Vendor` | — |
| Modelo | Modelo específico | ✅ `Device.Model` | — |
| Número de série | Serial number | ✅ `Device.SerialNumber` | — |
| Firmware | Versão do firmware | ❌ Não persistido | **NOVO** |
| IP de gestão | Endereço IP de acesso | ✅ `Device.Ip` | — |
| Localização | Local físico (sala, andar) | ✅ `Device.LocationId` → `Location` | — |
| Mac Address | MAC da câmera/NVR | ✅ `NetworkInterface.MacAddress` | — |
| Tipo | Câmera/NVR | ✅ `Device.DeviceType` | — |
| NVR associado | NVR que grava esta câmera | ❌ Não modelado | **NOVO** |
| Tempo de atividade (Uptime) | Tempo desde o último boot | ⚠️ Só via SNMP sob demanda | Persistir no Device |
| Referência Zabbix | Host ID no Zabbix | ✅ `ExternalDeviceReference` (Source=Zabbix) | — |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | OID / Endpoint | Disponibilidade |
|------|-------|---------------|-----------------|
| Nome (sysName) | SNMP | `1.3.6.1.2.1.1.5.0` | Universal (MIB-II) |
| Firmware versão | SNMP | `1.3.6.1.2.1.1.1.0` (sysDescr) | Universal (contém versão) |
| Uptime (sysUpTime) | SNMP | `1.3.6.1.2.1.1.3.0` | Universal (MIB-II) |
| MAC address | SNMP | `1.3.6.1.2.1.2.2.1.6.<ifIndex>` | Universal (ifPhysAddress) |
| Estado de gravação | API do NVR/câmera | Vendor-specific | Requer integração |

### Fonte de cada dado

| Dado | Fonte Primária | Fonte Secundária | Observação |
|------|----------------|------------------|------------|
| Nome, IP, Localização | **InfraControl** (Device) | — | System of Record |
| Firmware, Uptime | **SNMP** (sob demanda) | Zabbix (histórico) | Persistir no Device |
| NVR associado | **InfraControl** (relação) | — | Nova entidade `CameraNvrMapping` |
| Estado de gravação | **API do NVR/câmera** | — | Vendor-specific |
| Referência Zabbix | **Zabbix sync** (F29-1) | — | ExternalDeviceReference |

### Campos do cadastro

| Campo | Obrigatório? | Padrão | Validação |
|-------|--------------|--------|-----------|
| Nome | ✅ Sim | — | Único global, case-insensitive |
| Tipo | ✅ Sim | Camera/NVR | Enum fixo |
| Fabricante | ⚠️ Opcional | — | Dropdown de vendors conhecidos |
| Modelo | ⚠️ Opcional | — | Texto livre |
| Número de série | ❌ Opcional | — | Texto livre |
| IP de gestão | ✅ Sim | — | IP válido, único global (conflito detector F29-4) |
| Localização | ✅ Sim | — | FK para Location |
| NVR associado | ❌ Opcional | — | FK para Device (tipo NVR) |
| Comunidade SNMP | ⚠️ Opcional | — | Config de integração (por device ou global) |

---

## B. Estado e Disponibilidade

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Online/Offline | Estado de conectividade | ✅ Ping check (DeviceProfileDefaults) | — |
| Estado da câmera | Active/Inactive/Offline | ❌ Não modelado | **NOVO** |
| Estado do NVR | Recording/Stopped/Error | ❌ Não modelado | **NOVO** |
| Estado do canal | Recording/Stopped/Motion | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**SNMP por fabricante**:

| Dado | Hikvision | Dahua | Axis | ONVIF |
|------|----------|-------|------|-------|
| Câmera status | Vendor-specific | Vendor-specific | Vendor-specific | ONVIF API |
| NVR status | Vendor-specific | Vendor-specific | Vendor-specific | ONVIF API |
| Recording status | Vendor-specific | Vendor-specific | Vendor-specific | ONVIF API |

**Universal (ifTable)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| ifOperStatus | `1.3.6.1.2.1.2.2.1.8.<ifIndex>` | Status da interface (up/down) |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Online/Offline | **Ping** (existente) | DeviceProfileDefaults |
| Estado da câmera/NVR/canal | **API do NVR/câmera** | Vendor-specific (ONVIF se disponível) |
| Status da interface | **SNMP** (ifOperStatus) | Universal |

---

## C. Gravação e Armazenamento

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Estado de gravação | Recording/Stopped | ❌ Não modelado | **NOVO** |
| Armazenamento total | Capacidade do disco | ❌ Não modelado | **NOVO** |
| Armazenamento usado | Espaço ocupado | ❌ Não modelado | **NOVO** |
| Armazenamento livre | Espaço disponível | ❌ Não modelado | **NOVO** |
| Dias de retenção | Dias de gravação disponíveis | ❌ Não modelado | **NOVO** |
| Estado do disco | OK/Fail/Warning | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**ONVIF API** (padrão universal):

| Dado | Endpoint | Descrição |
|------|----------|-----------|
| Recording status | `/onvif/device_service` | Estado de gravação |
| Storage status | `/onvif/device_service` | Status do armazenamento |
| Capacity | `/onvif/device_service` | Capacidade total/usada/livre |

**Vendor-specific (Hikvision)**:

| Dado | API | Descrição |
|------|-----|-----------|
| Storage info | `ISAPI/Storage/Info` | Capacidade, usado, livre |
| Recording status | `ISAPI/ContentMgmt/Recording` | Estado de gravação |

**Vendor-specific (Dahua)**:

| Dado | API | Descrição |
|------|-----|-----------|
| Storage info | `/RPC/ContentMgmt/Storage/Info` | Capacidade, usado, livre |
| Recording status | `/RPC/ContentMgmt/Recording` | Estado de gravação |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Estado de gravação | **API do NVR/câmera** | ONVIF (padrão) ou vendor-specific |
| Armazenamento | **API do NVR** | ONVIF (padrão) ou vendor-specific |
| Dias de retenção | **Derivado** (armazenamento / taxa diária) | Requer cálculo |

---

## D. Eventos e Alertas

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Detecção de movimento | Evento de motion | ❌ Não modelado | **NOVO** |
| Perda de vídeo | Video loss | ❌ Não modelado | **NOVO** |
| Tamper | Manipulação da câmera | ❌ Não modelado | **NOVO** |
| Line crossing | Cruzamento de linha | ❌ Não modelado | **NOVO** |
| Intrusão | Detecção de intrusão | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**ONVIF API** (padrão universal):

| Dado | Endpoint | Descrição |
|------|----------|-----------|
| Motion detection | `/onvif/event_service` | Evento de movimento |
| Video loss | `/onvif/event_service` | Perda de vídeo |
| Tamper | `/onvif/event_service` | Manipulação |

**Vendor-specific (Hikvision)**:

| Dado | API | Descrição |
|------|-----|-----------|
| Motion detection | `ISAPI/Event/triggers/motion` | Evento de movimento |
| Video loss | `ISAPI/Event/triggers/videoloss` | Perda de vídeo |
| Line crossing | `ISAPI/Event/triggers/lineCrossing` | Cruzamento de linha |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Eventos de movimento/loss/tamper | **API do NVR/câmera** | ONVIF (padrão) ou vendor-specific |
| Eventos avançados (line crossing, intrusão) | **API do NVR/câmera** | Vendor-specific |

---

## E. Qualidade de Vídeo

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Resolução | 1080p, 4K, etc. | ❌ Não modelado | **NOVO** |
| FPS | Frames por segundo | ❌ Não modelado | **NOVO** |
| Bitrate | Mbps | ❌ Não modelado | **NOVO** |
| Codec | H.264, H.265, etc. | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**ONVIF API** (padrão universal):

| Dado | Endpoint | Descrição |
|------|----------|-----------|
| Stream configuration | `/onvif/media_service` | Resolução, FPS, codec, bitrate |

**Vendor-specific (Hikvision)**:

| Dado | API | Descrição |
|------|-----|-----------|
| Stream info | `ISAPI/ContentMgmt/Stream` | Resolução, FPS, codec |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Resolução, FPS, codec, bitrate | **API do NVR/câmera** | ONVIF (padrão) ou vendor-specific |

---

## F. Histórico e Prevenção

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Tendência de armazenamento | Armazenamento ao longo do tempo | ❌ Não modelado | **NOVO** (prevenção) |
| Armazenamento esgotando | Previsão de disco cheio | ❌ Não detectado | **NOVO** (prevenção) |
| Perda de vídeo recorrente | Video loss repetindo | ❌ Não detectado | **NOVO** (prevenção) |
| Detecção de movimento | Eventos de movimento | ❌ Não modelado | **NOVO** |

### Modelo de Manutenção Preventiva (proposto)

| Problema | Sinal | Antecipação | Ação |
|----------|-------|-------------|------|
| Armazenamento esgotando | Disco > 90% | Disco > 80% e tendência ascendente | Limpar gravações antigas, aumentar disco |
| Perda de vídeo recorrente | Video loss repetindo | > X eventos em Y dias | Investigar cabo, alimentação, câmera |
| Taxa de eventos anormal | Muitos eventos de movimento | Eventos > limiar por hora | Ajustar sensibilidade, investigar ambiente |
| Falha de gravação | NVR não gravando | Estado de gravação = Stopped por X minutos | Investigar NVR, armazenamento |

---

## Métricas que Precisam de Histórico

| Métrica | Período de retenção | Resolução | Justificativa |
|---------|-------------------|-----------|---------------|
| Armazenamento % | 90 dias | 1 hora | Previsão de esgotamento |
| Estado de gravação | 30 dias | Evento | Análise de disponibilidade |
| Eventos de movimento | 30 dias | Evento | Análise de padrões |
| Video loss | 30 dias | Evento | Análise de confiabilidade |
| Uptime | Permanente | Evento (boot) | Detecção de reboots |

**Nota**: O Zabbix pode manter histórico numérico. O InfraControl deve:
- Consultar o Zabbix para histórico detalhado (via API)
- Persistir apenas eventos agregados e tendências no PostgreSQL (REGRA 8 de ADR-028)

---

## Condições de Alerta

### Alertas reativos (existentes)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Câmera/NVR não responde a ping | Critical | ✅ Ping check |
| Interface down | Warning | ✅ ifOperStatus via SNMP |

### Alertas preventivos (novos)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Armazenamento > 90% | Critical | ❌ Novo |
| Armazenamento > 80% | Warning | ❌ Novo |
| Perda de vídeo | Critical | ❌ Novo |
| Gravação parada | Critical | ❌ Novo |
| Disco falhando | Critical | ❌ Novo |
| Tamper detectado | Warning | ❌ Novo |
| Video loss recorrente (> X em Y dias) | Warning | ❌ Novo |

---

## Operações Administrativas Permitidas

### Escopo inicial (somente monitoramento)

| Operação | Fonte | Implementação | Status |
|----------|-------|----------------|--------|
| Consultar estado atual | SNMP/API | Provider novo | ⚠️ A implementar |
| Consultar armazenamento | API do NVR | Provider novo | ⚠️ A implementar |
| Consultar histórico de métricas | Zabbix API | Query Zabbix para histórico | ⚠️ Parcial |

### Operações futuras (fora escopo inicial)

| Operação | Justificativa | Pré-requisitos |
|----------|---------------|----------------|
| Reiniciar câmera/NVR | Operação crítica | SSH/power control |
| Ajustar sensibilidade de movimento | Operação crítica | Integração API |
| Exportar gravação | Operação crítica | Integração API |

---

## Tratamento de Dados Indisponíveis, Erros e Credenciais

### Dados indisponíveis

| Situação | Comportamento | Mensagem ao usuário |
|-----------|---------------|---------------------|
| Câmera/NVR offline | Estado = Unknown, Cache último valor conhecido | "Câmera offline. Última atualização: [timestamp]" |
| API indisponível | Estado = Degraded, Cache último valor conhecido | "API indisponível. Dados em cache." |
| SNMP community incorreta | Erro de autenticação, Log warn | "Erro de autenticação SNMP. Verifique community string." |
| ONVIF não suportado | Campo = null, Log info | "ONVIF não disponível neste modelo." |

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
| API (ONVIF/vendor) | Environment variable | Redação total em DTOs |

---

## Estratégia de Implementação

### Abordagem por vendor

**1. ONVIF (padrão universal) - Prioridade Alta**:
- ONVIF é implementado pela maioria dos fabricantes modernos
- Criar `OnvifCameraProvider` para estado, gravação, armazenamento
- Funciona para Hikvision, Dahua, Axis, etc.

**2. SNMP universal - Prioridade Média**:
- ifTable/ifXTable para tráfego
- ifOperStatus para estado da interface
- Funciona para maioria dos fabricantes

**3. Vendor-specific opcional**:
- Hikvision ISAPI para recursos avançados
- Dahua RPC para recursos avançados
- Fallback seguro: se vendor não suportado, usar ONVIF universal

### Modelo de domínio (proposto)

```csharp
// Extension para Device (somente quando DeviceType == Camera ou NVR)
public class CameraNvrMapping
{
    public Guid CameraId { get; }
    public Guid NvrId { get; }
    public int ChannelNumber { get; } // Canal no NVR
}

public class CameraStorage
{
    public Guid DeviceId { get; }
    public DateTime CapturedAt { get; }
    public long TotalBytes { get; }
    public long UsedBytes { get; }
    public long FreeBytes { get; }
    public int RetentionDays { get; } // Derivado
}
```

### Prioridade de Implementação

| Ordem | Item | Justificativa |
|-------|------|---------------|
| 1 | **OnvifCameraProvider** (estado, gravação, armazenamento) | ONVIF universal |
| 2 | **CameraNvrMapping** (relação câmera-NVR) | Modelar associação |
| 3 | **SnmpCameraProvider** (tráfego, estado interface) | SNMP universal |
| 4 | **Modelo de histórico/prevenção** | Limiar, tendência, armazenamento |
| 5 | **Vendor-specific APIs** (Hikvision ISAPI, Dahua RPC) | Apenas se necessário |

---

## Estratégia de Teste

### Ambiente de homologação

| Requisito | Implementação |
|-----------|---------------|
| Câmera/NVR real para testes | Câmera/NVR em laboratório (não produção) |
| ONVIF habilitado | ONVIF ativado no dispositivo |
| Comunidade SNMP liberada | Read-only community configurada |
| Acesso Zabbix | Host criado no Zabbix de teste |
| Dados variados | Gerar movimento, simular video loss, gravar |

### Testes automatizados

| Tipo | Escopo |
|------|--------|
| Unit | Providers ONVIF (OnvifCameraProvider, novos providers) |
| Integration | Repository + Query com PostgreSQL |
| E2E | Ponta-a-ponta com câmera/NVR real (gate de infra) |
| Mock | Simular respostas ONVIF/SNMP para cenários de erro |

---

## Próximos Passos (Recomendação)

1. **Prioridade imediata**:
   - Criar `CameraNvrMapping` (relação câmera-NVR)
   - Criar `OnvifCameraProvider` (estado, gravação, armazenamento via ONVIF)
   - Criar `SnmpCameraProvider` (tráfego, estado interface)

2. **Prioridade média**:
   - Implementar modelo de histórico (tendência de armazenamento)
   - Implementar modelo de prevenção (limiar, armazenamento esgotando)
   - Adicionar `Device.Criticality` para câmeras/NVRs

3. **Prioridade baixa**:
   - Vendor-specific APIs (Hikvision ISAPI, Dahua RPC)
   - Monitoramento de eventos avançados (line crossing, intrusão)
   - Operações administrativas (reboot, ajuste de sensibilidade)

---

## Referências

- ONVIF Specification: https://www.onvif.org/
- ADR-028: Arquitetura da Camada de Integrações
- ADR-048: Identidade Única de Device, Zabbix×UniFi via API
- F2-T008: DeviceProfile
- Hikvision ISAPI Documentation: https://www.hikvision.com/
- Dahua RPC Documentation: https://www.dahuasecurity.com/
