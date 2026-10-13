# Ficha Técnica: Impressora e Multifunção

> **Status**: Rascunho (2026-10-13)
> **Contexto**: Especificação Operations Center - Segundo dispositivo a ser estudado
> **Referência**: Especificação do usuário (seção 2 - "Quais dispositivos devemos estudar?")

---

## Visão Geral

A impressora é o segundo caso de estudo porque:
- É um dispositivo crítico para operações de escritório
- Consumíveis (toner, papel) exigem gestão preventiva
- Atolamentos e filas impactam diretamente os usuários
- Printer-MIB (RFC 3805) oferece um padrão relativamente universal

**Ambiente de fabricantes** (específico):
- **HP**: Printers e multifunções HP
- **Canon**: Printers e multifunções Canon
- **Epson**: Printers e multifunções Epson

**Responsabilidades**:
- **Zabbix**: Coleta de métricas SNMP (toner, drum, contadores, estado), histórico detalhado, triggers de alerta
- **InfraControl**: Inventário central, visão unificada, análise preventiva, histórico de incidentes, correlação com outros dispositivos
- **Impressora**: Fonte de dados via SNMP (Printer-MIB) e APIs vendor-specific (quando disponível)

**Áreas de foco** (conforme especificação):
- A. Identificação e configuração
- B. Estado e disponibilidade
- C. Consumíveis
- D. Contadores e uso
- E. Filas de impressão
- F. Logs e eventos
- G. Histórico e prevenção

---

## A. Identificação e Configuração

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Nome | Nome da impressora | ✅ `Device.Name` | — |
| Fabricante | Vendor (HP, Canon, Brother, etc.) | ✅ `Device.Vendor` | — |
| Modelo | Modelo específico | ✅ `Device.Model` | — |
| Número de série | Serial number | ✅ `Device.SerialNumber` | — |
| Firmware | Versão do firmware | ❌ Não persistido | **NOVO** |
| IP de gestão | Endereço IP de acesso | ✅ `Device.Ip` | — |
| Localização | Local físico (sala, andar) | ✅ `Device.LocationId` → `Location` | — |
| Tipo | Impressora/Multifunção | ✅ `Device.DeviceType` | — |
| Referência Zabbix | Host ID no Zabbix | ✅ `ExternalDeviceReference` (Source=Zabbix) | — |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | OID / Endpoint | Disponibilidade |
|------|-------|---------------|-----------------|
| Nome (sysName) | SNMP | `1.3.6.1.2.1.1.5.0` | Universal (MIB-II) |
| Firmware versão | SNMP | `1.3.6.1.2.1.1.1.0` (sysDescr) | Universal (contém versão) |
| Serial | SNMP | Vendor-specific | Varia por fabricante |
| Uptime | SNMP | `1.3.6.1.2.1.1.3.0` (sysUpTime) | Universal (MIB-II) |

### Fonte de cada dado

| Dado | Fonte Primária | Fonte Secundária | Observação |
|------|----------------|------------------|------------|
| Nome, IP, Localização | **InfraControl** (Device) | — | System of Record |
| Firmware, Uptime | **SNMP** (sob demanda) | Zabbix (histórico) | Persistir no Device |
| Referência Zabbix | **Zabbix sync** (F29-1) | — | ExternalDeviceReference |

### Campos do cadastro

| Campo | Obrigatório? | Padrão | Validação |
|-------|--------------|--------|-----------|
| Nome | ✅ Sim | — | Único global, case-insensitive |
| Tipo | ✅ Sim | Printer/Multifunção | Enum fixo |
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
| Estado do dispositivo | Ready/Printing/Error | ⚠️ Parcial (SnmpSystem check genérico) | Provider específico |
| Estado da bandeja | Cheia/Vazia/OK | ❌ Não modelado | **NOVO** |
| Atolamento de papel | Detecção de jam | ❌ Não modelado | **NOVO** |
| Tampa aberta | Cover open status | ❌ Não modelado | **NOVO** |
| Erros do dispositivo | Alertas internos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**Printer-MIB (RFC 3805) - Universal**:

| Dado | OID | Descrição |
|------|-----|-----------|
| hrDeviceStatus | `1.3.6.1.2.1.25.3.2.1.5.<hrDeviceIndex>` | Estado do dispositivo (running/warning/down) |
| prtInputStatus | `1.3.6.1.2.1.43.8.2.1.9.<prtInputIndex>` | Status da bandeja de entrada |
| prtOutputStatus | `1.3.6.1.2.1.43.9.2.1.6.<prtOutputIndex>` | Status da bandeja de saída |
| prtCoverStatus | `1.3.6.1.2.1.43.6.1.1.3.<prtCoverIndex>` | Status da tampa (open/closed) |
| prtAlertTable | `1.3.6.1.2.1.43.18` | Tabela de alertas (papel vazio, toner vazio, atolamentos) |

**Vendor-specific (HP)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| Paper jam status | Vendor-specific | HP-LASERJET-COMMON-MIB |
| Paper status | Vendor-specific | HP-LASERJET-COMMON-MIB |
| Toner status | Vendor-specific | HP-LASERJET-COMMON-MIB |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Online/Offline | **Ping** (existente) | DeviceProfileDefaults |
| Estado da bandeja, tampa, alertas | **SNMP** (Printer-MIB) | Provider novo `PrinterStatusProvider` |
| Erros específicos | **SNMP** (vendor-specific) | Provider custom por vendor |

---

## C. Consumíveis

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Toner preto | Nível % | ✅ SnmpToner check (DeviceProfileDefaults) | Falta cilindro/fusor |
| Toner ciano | Nível % | ⚠️ Parcial (color printers) | **NOVO** |
| Tiano magenta | Nível % | ⚠️ Parcial (color printers) | **NOVO** |
| Tiano amarelo | Nível % | ⚠️ Parcial (color printers) | **NOVO** |
| Cilindro (Drum) | Nível % | ✅ SnmpDrum check (DeviceProfileDefaults) | — |
| Fusor (Fuser) | Nível % | ❌ Não modelado | **NOVO** |
| Resíduo de toner | Nível % | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**Printer-MIB (RFC 3805) - Universal**:

| Dado | OID | Observação |
|------|-----|-----------|
| prtMarkerSuppliesDescription | `1.3.6.1.2.1.43.11.1.1.6.<index>` | Nome do consumível (ex: "Black Toner") |
| prtMarkerSuppliesMaxCapacity | `1.3.6.1.2.1.43.11.1.1.8.<index>` | Capacidade máxima (unidades) |
| prtMarkerSuppliesLevel | `1.3.6.1.2.1.43.11.1.1.9.<index>` | Nível atual (unidades) |
| prtMarkerColorantValue | `1.3.6.1.2.1.43.12.1.1.4.<index>` | Valor do colorante (1=K, 2=C, 3=M, 4=Y) |

**Cálculo de porcentagem**:
```
% = (prtMarkerSuppliesLevel / prtMarkerSuppliesMaxCapacity) × 100
```

**Sentinelas especiais**:
- `-1` = unknown
- `-2` = unrestricted
- `-3` = some remaining

**Vendor-specific (HP Color LaserJet 8550)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| Drum Kit level | `1.3.6.1.2.1.43.11.1.1.9.5` | Cilindro |
| Transfer Kit level | `1.3.6.1.2.1.43.11.1.1.9.6` | Transferência |
| Fuser Kit level | `1.3.6.1.2.1.43.11.1.1.9.7` | Fusor |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Toner/Cilindro | **SNMP** (Printer-MIB) | Provider `SnmpTonerDrumProvider` já existe |
| Fusor/Transfer Kit | **SNMP** (vendor-specific) | Provider custom por vendor |
| Resíduo de toner | **SNMP** (Printer-MIB) | PrtMarkerSupplies index 5 (waste) |

### Modelo de domínio (proposto)

```csharp
// Extension para Device (somente quando DeviceType == Printer ou Multifunção)
public class PrinterConsumables
{
    public Guid DeviceId { get; }
    public DateTime CapturedAt { get; }
    public decimal? BlackTonerPercent { get; }
    public decimal? CyanTonerPercent { get; }
    public decimal? MagentaTonerPercent { get; }
    public decimal? YellowTonerPercent { get; }
    public decimal? DrumPercent { get; }
    public decimal? FuserPercent { get; }
    public decimal? WasteTonerPercent { get; }
}
```

---

## D. Contadores e Uso

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Total de páginas | Impressões totais | ❌ Não persistido | **NOVO** |
| Páginas monocromáticas | Impressões P&B | ❌ Não persistido | **NOVO** |
| Páginas coloridas | Impressões color | ❌ Não persistido | **NOVO** |
| Páginas escaneadas | Scans total | ❌ Não persistido | **NOVO** |
| Volume mensal | Páginas por mês | ❌ Não modelado | **NOVO** (prevenção) |

### Dados que o equipamento realmente disponibiliza

**Printer-MIB (RFC 3805) - Universal**:

| Dado | OID | Descrição |
|------|-----|-----------|
| prtMarkerLifeCount | `1.3.6.1.2.1.43.10.2.1.4.1.1` | Total de impressões (monotônico) |
| prtMarkerLifeCount | `1.3.6.1.2.1.43.10.2.1.4.1.2` | Impressões monocromáticas (se disponível) |
| prtMarkerLifeCount | `1.3.6.1.2.1.43.10.2.1.4.1.3` | Impressões coloridas (se disponível) |

**Vendor-specific (HP)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| Total | `1.3.6.1.4.1.11.2.3.9.4.2.1.4.1.2.5.0` | Total de impressões |
| Mono total | `1.3.6.1.4.1.11.2.3.9.4.2.1.4.1.2.6.0` | Impressões P&B |
| Color total | `1.3.6.1.4.1.11.2.3.9.4.2.1.4.1.2.7.0` | Impressões color |

**Vendor-specific (Canon)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| Total | `1.3.6.1.4.1.1602.1.2.1.3.1.0` | Total de impressões (Canon enterprise) |
| Mono total | `1.3.6.1.4.1.1602.1.2.1.3.2.0` | Impressões P&B (Canon enterprise) |
| Color total | `1.3.6.1.4.1.1602.1.2.1.3.3.0` | Impressões color (Canon enterprise) |

**Vendor-specific (Epson)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| Total | `1.3.6.1.4.1.1248.1.2.2.1.1.1.1.0` | Total de impressões (Epson enterprise) |
| Mono total | `1.3.6.1.4.1.1248.1.2.2.1.1.1.2.0` | Impressões P&B (Epson enterprise) |
| Color total | `1.3.6.1.4.1.1248.1.2.2.1.1.1.3.0` | Impressões color (Epson enterprise) |

**Vendor-specific (Xerox)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| Total | `1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.1` | Total de impressões |
| Mono total | `1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.34` | Impressões P&B |
| Color total | `1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.35` | Impressões color |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Total de páginas | **SNMP** (Printer-MIB) | Provider novo `PrinterCountersProvider` |
| Split mono/color | **SNMP** (vendor-specific) | Provider custom por vendor |
| Volume mensal | **Derivado** (diff de contadores) | Requer persistência temporal |

---

## E. Filas de Impressão

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Trabalhos pendentes | Jobs na fila | ❌ Não modelado | **NOVO** |
| Trabalhos com erro | Jobs falhados | ❌ Não modelado | **NOVO** |
| Tempo de espera | Tempo desde o envio | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | Disponibilidade |
|------|-------|-----------------|
| Filas de impressão | Servidor de impressão | Requer integração com servidor de impressão (Windows Print Server, CUPS, etc.) |
| Status de jobs | API do servidor de impressão | Vendor-specific |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Filas de impressão | **Integração servidor de impressão** | Fora escopo inicial - requer integração específica |

---

## F. Logs e Eventos

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Erros do dispositivo | Alertas e erros | ❌ Não modelado | **NOVO** |
| Avisos | Warnings | ❌ Não modelado | **NOVO** |
| Histórico de erros | Log de eventos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**Printer-MIB (RFC 3805)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| prtAlertTable | `1.3.6.1.2.1.43.18` | Tabela de alertas ativos |

**Tipos de alerta (Canon imagePRESS)**:

| Caso de alerta | Severidade | Grupo | Código |
|----------------|------------|-------|-------|
| Bandeja "n" vazia | Warning | Input | n |
| Todas as bandejas vazias | Critical | Input | 100 |
| Bandeja de saída "n" cheia | Warning | Output | n |
| Toner vazio | Critical | Supply | n |
| Toner baixo | Warning | Supply | n |
| Resíduo de toner cheio | Warning | General | 1 |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Alertas ativos | **SNMP** (prtAlertTable) | Provider novo `PrinterAlertsProvider` |
| Histórico de erros | **Syslog** ou **API do fabricante** | Fora escopo inicial |

---

## G. Histórico e Prevenção

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Tendência de uso | Páginas por mês | ❌ Não modelado | **NOVO** (prevenção) |
| Toner esgotando | Taxa de consumo | ❌ Não detectado | **NOVO** (prevenção) |
| Atolamentos recorrentes | Jam count recorrente | ❌ Não detectado | **NOVO** (prevenção) |
| Previsão de substituição | Quando trocar toner/fusor | ❌ Não modelado | **NOVO** (prevenção) |

### Modelo de Manutenção Preventiva (proposto)

| Problema | Sinal | Antecipação | Ação |
|----------|-------|-------------|------|
| Toner esgotando | Nível < 20% e consumo alto | Tendência descendente de nivel % × uso mensal | Solicitar reposição |
| Cilindro degradando | Erros de qualidade + nível baixo | Aumento de erros + cilindro < 20% | Programar troca |
| Fusor falhando | Erros de fusão + nível baixo | Alertas de erro + fusor < 20% | Programar troca |
| Atolamentos recorrentes | > X jams em Y dias | Count de jams em janela temporal | Investigar papel/rolo |
| Volume insuficiente | > Z páginas/mês no mesmo modelo | Comparação de volume por modelo | Redistribuir carga |

---

## Métricas que Precisam de Histórico

| Métrica | Período de retenção | Resolução | Justificativa |
|---------|-------------------|-----------|---------------|
| Nível de toner | 90 dias | 1 hora | Previsão de esgotamento |
| Contador de páginas | Permanente | Evento (impressão) | Cálculo de volume mensal |
| Alertas de dispositivo | 90 dias | Evento | Análise de recorrência |
| Estado da bandeja | 30 dias | 5 minutos | Detecção de problemas frequentes |

**Nota**: O Zabbix pode manter histórico numérico. O InfraControl deve:
- Consultar o Zabbix para histórico detalhado (via API)
- Persistir apenas eventos agregados e tendências no PostgreSQL (REGRA 8 de ADR-028)

---

## Condições de Alerta

### Alertas reativos (existentes)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Impressora não responde a ping | Critical | ✅ Ping check |
| Toner vazio | Critical | ✅ SnmpToner check (já implementado) |
| Cilindro vazio | Warning | ✅ SnmpDrum check (já implementado) |

### Alertas preventivos (novos)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Toner < 20% | Warning | ❌ Novo |
| Toner < 10% | Critical | ❌ Novo |
| Cilindro < 20% | Warning | ❌ Novo |
| Fusor < 20% | Warning | ❌ Novo |
| Bandeja vazia | Critical | ❌ Novo |
| Atolamento detectado | Critical | ❌ Novo |
| Tampa aberta | Warning | ❌ Novo |
| Atolamentos recorrentes (> X em Y dias) | Warning | ❌ Novo |

---

## Operações Administrativas Permitidas

### Escopo inicial (somente monitoramento)

| Operação | Fonte | Implementação | Status |
|----------|-------|----------------|--------|
| Consultar estado atual | SNMP | `SnmpTonerDrumProvider` existente | ✅ Implementado |
| Consultar nível de toner | SNMP | `SnmpTonerDrumProvider` existente | ✅ Implementado |
| Consultar histórico de métricas | Zabbix API | Query Zabbix para histórico | ⚠️ Parcial |

### Operações futuras (fora escopo inicial)

| Operação | Justificativa | Pré-requisitos |
|----------|---------------|----------------|
| Integração com servidor de impressão | Requer protocolo específico (Windows Print Server, CUPS) | Integração custom |
| Cancelar jobs | Operação crítica | Autenticação no servidor de impressão |
| Reiniciar impressora | Operação crítica | SSH/power control |

---

## Tratamento de Dados Indisponíveis, Erros e Credenciais

### Dados indisponíveis

| Situação | Comportamento | Mensagem ao usuário |
|-----------|---------------|---------------------|
| Impressora offline | Estado = Unknown, Cache último valor conhecido | "Impressora offline. Última atualização: [timestamp]" |
| SNMP community incorreta | Erro de autenticação, Log warn | "Erro de autenticação SNMP. Verifique community string." |
| OID não suportado | Campo = null, Log info | "Métrica não disponível neste modelo." |
| Timeout SNMP | Retentativa (Polly), após 3 falhas → Degraded | "Tempo esgotado ao consultar impressora." |

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
| Servidor de impressão (futuro) | Credential Store (F11-T006) | ICredentialProtector |

---

## Estratégia de Implementação

### Abordagem de Providers SNMP

**1. Abstração universal (Printer-MIB)**:
- Printer-MIB (RFC 3805) é implementado pela maioria dos fabricantes
- Criar `PrinterStatusProvider` (estado, bandejas, tampas, alertas)
- Criar `PrinterCountersProvider` (total de páginas, split mono/color quando disponível)
- Reutilizar `SnmpTonerDrumProvider` existente e estender para color

**2. Vendor-specific opcional**:
- Para split mono/color detalhado: `HpPrinterCountersProvider`, `XeroxPrinterCountersProvider`
- Para fusor/transfer kit: providers custom por vendor
- Fallback seguro: se vendor não suportado, usar Printer-MIB universal

**3. Filas de impressão**:
- Fora escopo inicial
- Requer integração com servidor de impressão (Windows Print Server, CUPS, etc.)
- Pode ser adicionado em fase futura com task própria

### Prioridade de Implementação

| Ordem | Item | Justificativa |
|-------|------|---------------|
| 1 | **Estender SnmpTonerDrumProvider** para color | Reutilizar código existente |
| 2 | **PrinterStatusProvider** (bandejas, tampas, alertas) | Printer-MIB universal |
| 3 | **PrinterCountersProvider** (total de páginas) | Printer-MIB universal |
| 4 | **Modelo de histórico/prevenção** | Limiar, tendência, volume mensal |
| 5 | **Vendor-specific counters** (mono/color split) | HP, Xerox, etc. |

---

## Estratégia de Teste

### Ambiente de homologação

| Requisito | Implementação |
|-----------|---------------|
| Impressora real para testes | Impressora gerenciada em laboratório (não produção) |
| Comunidade SNMP liberada | Read-only community configurada |
| Acesso Zabbix | Host criado no Zabbix de teste |
| Dados variados | Imprimir testes, esvaziar toner, gerar atolamentos |

### Testes automatizados

| Tipo | Escopo |
|------|--------|
| Unit | Providers SNMP (`SnmpTonerDrumProvider`, novos providers) |
| Integration | Repository + Query com PostgreSQL |
| E2E | Ponta-a-ponta com impressora real (gate de infra) |
| Mock | Simular respostas SNMP para cenários de erro |

---

## Próximos Passos (Recomendação)

1. **Prioridade imediata**:
   - Estender `SnmpTonerDrumProvider` para toner color (C/M/Y)
   - Criar `PrinterStatusProvider` (bandejas, tampas, alertas via Printer-MIB)
   - Criar `PrinterCountersProvider` (total de páginas via Printer-MIB)

2. **Prioridade média**:
   - Implementar modelo de histórico (diff de contadores para volume mensal)
   - Implementar modelo de prevenção (limiar de toner, tendência de consumo)
   - Adicionar `Device.Criticality` para impressoras

3. **Prioridade baixa**:
   - Integração com servidor de impressão (filas, jobs)
   - Vendor-specific counters (mono/color split detalhado)
   - Logs de eventos via Syslog

---

## Referências

- RFC 3805: Printer MIB v2
- Printer-MIB: `1.3.6.1.2.1.43`
- ADR-028: Arquitetura da Camada de Integrações
- ADR-048: Identidade Única de Device, Zabbix×UniFi via API
- F2-T008: DeviceProfile
- SNMP Printer OIDs Reference: https://snmp-monitoring.info/sensors/printer/
- PrinterMS OID Reference: https://www.printerms.com/en/resources/printer-oid-reference
