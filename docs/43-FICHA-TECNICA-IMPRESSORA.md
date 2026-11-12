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

---

## Monitoramento no Zabbix — Implantação (2026-10-18)

Esta seção registra o que já está **ao vivo** no Zabbix (template
`IC Impressora SNMP`, templateid 10887) e o mapeamento com as áreas desta
ficha (F30). Detalhes operacionais em `docs/27-INTEGRACAO-ZABBIX.md`.

### Cobertura por área da ficha

| Área da ficha | Item/template no Zabbix | Status |
| --- | --- | --- |
| A. Identificação | `printer.info[sysDescr]` (modelo, firmware, S/N) + itens genéricos do template 10226 (sysName, sysContact, sysLocation, sysObjectID) | ✅ ao vivo |
| B. Estado | `printer.status` (hrPrinterStatus, value map em PT) + LLD **alertas** `printer.alerts.discovery` (`prtAlertDescription` → `{#SNMPVALUE}`); correção F27-T012 aplicada no template: item lê `prtAlertSeverityLevel` (coluna 2), triggers distinguem crítico e warning/valor não mapeado; coleta e ausência de problema ativo verificadas no host HSDS_HP_SETOR_1_ENF | ✅ amostra ao vivo verificada; demais hosts pendentes |
| C. Consumíveis | LLD **suprimentos** `printer.supplies.discovery` (`prtMarkerSuppliesDescription`), protótipos nível (`prtMarkerSuppliesLevel`) e capacidade (`prtMarkerSuppliesMaxCapacity`), triggers "baixo" (<10%) e "criticamente baixo" (<5%) | ✅ ao vivo |
| D. Contadores | `printer.pages.total` (`prtMarkerLifeCount` — total de páginas) | ✅ ao vivo (split mono/color, fase 2) |
| E. Filas | — | ⏳ fora do escopo inicial |
| F. Logs | — | ⏳ (prtAlertTable é o "estado atual"; histórico fica no history do Zabbix) |
| G. Prevenção | triggers de nível de suprimento (10%/5%) + uptime/reinicialização (`printer.uptime[sysUpTime]`) | ✅ parcial |

### Correção F27-T012 — interpretação Printer-MIB (2026-10-24)

- O provider `SnmpPrinterStatusProvider` agora interpreta os códigos
  padronizados da RFC 3805: `critical(3)`, `warning(4)` e
  `warningBinaryChangeEvent(5)`; outros valores são `unknown`. O agregado
  escolhe `critical` antes de `warning` e `unknown`. Testes focados: 17/17.
- O template Zabbix existente (10887) foi corrigido após documentação e
  autorização: item prototype `83315` passou a consultar a coluna 2
  (`prtAlertSeverityLevel`); trigger `38061` classifica `=3` como crítico/High
  e trigger `38062` classifica `<>1 and <>3` como Warning, incluindo enums não
  mapeados sem chamá-los de fatais. A ação global `InfraControl - triggers`
  (id 7) foi pausada durante a mudança e restaurada; a listagem foi conferida
  como Enabled. Após o próximo ciclo, `HSDS_HP_SETOR_1_ENF` (host 10907)
  mostrou valor `1` no item de severidade; o filtro de Problems para o host e
  trigger não encontrou problema ativo. A validação das outras impressoras
  continua pendente. Sem eventos sintéticos ou envio de WhatsApp.

### Validação ao vivo (2026-10-18)

- Canon iR1643i (CC): status=4 "imprimindo", 225.599 páginas, alerta
  `toner is out (black)` descoberto e **toner preto 0/100** → trigger
  "criticamente baixo" ativo (ação: trocar toner).
- HP Laser 408dn (Auditoria): 86.866 páginas, toner 13.800/15.000 (92%),
  suprimentos descobertos: Black Toner, Imaging Unit (OPC), Fuser,
  Transfer Roller, Tray 2 Roller/Retard Roller; alerta "Power Saver
  Mode" descoberto em severidade informativa (sem alarme).
- Epson e Canons HSSM respondendo idem.

### Particularidades por marca (empiricamente verificado)

- **Canon**: somente **SNMPv1** (`public`); walk da Printer-MIB funciona
  nativamente; alertas em texto curto.
- **HP LaserJet/MFP**: **SNMPv2c** (`public`) mas **GETBULK quebra** o
  walk da Printer-MIB (`genError`) → interface SNMP do host com
  **`bulk=0`**; alertas vêm com frase inteira (ex.: Power Saver) — nomes
  de trigger precisam ficar curtos (limite de 200 caracteres).
- **Epson**: responde SNMPv1.

### Pendências (fase 2)

- Ativar SNMP na `192.168.50.111` (HSSM_HP_SETOR2) pelo painel/web
  (usuário `user`, senha `1-7`) e promover de ping-only para SNMP+ping.
- Split mono/color por `prtMarkerSuppliesColorIndex` e contadores
  por tipo de mídia.
- LLD de bandejas (`prtInputTable`) e tampas (`prtCoverTable`).
- Zebra 220/503 e demais Epson: descobrir IPs (VLANs a investigar).

## Dashboard operacional de impressoras no Zabbix (plano e execução 2026-10-24)

### Objetivo

Criar no Zabbix um dashboard operacional para o grupo **Impressoras** (host
group ID 31), reutilizando itens, descoberta e triggers existentes. O painel
deve separar a visão global de problemas/disponibilidade do acesso aos dados
detalhados por impressora, sem criar coletores ou templates duplicados.

### Layout proposto

Uma página **Visão geral** com estes widgets nativos:

1. **Problems** — problemas recentes, filtrados pelo grupo `Impressoras`;
   mostrar recuperação recente para facilitar conferir o ciclo de alerta.
2. **Problems by severity** — distribuição da severidade para o mesmo grupo.
3. **Host availability** — disponibilidade SNMP dos hosts que têm interface
   SNMP configurada; ping-only continuará sem status SNMP e não deve ser
   apresentado como falha de impressora.
4. **Top hosts** — tabela do grupo `Impressoras`, com as colunas comuns
   `Impressora - status (hrPrinterStatus)` e
   `Impressora - total de paginas (prtMarkerLifeCount)`. Hosts sem esses itens,
   como o HP ping-only, podem aparecer sem valor para essas métricas.
5. **Item navigator** — seleção de host/item no grupo, para consultar
   suprimentos e alertas LLD que variam por modelo e por dispositivo.

O Zabbix 8.0 documenta suporte a dashboards com widgets de problemas,
disponibilidade, Top hosts, navegação de itens e histórico/gráficos. Os filtros
de grupo e widgets foram validados visualmente na instância.

### Limites conhecidos

- O snapshot da auditoria tinha 37 hosts no grupo: 36 com o template `IC
  Impressora SNMP` e SNMP disponível; `HSSM_HP_SETOR2` está ping-only. Logo,
  disponibilidade SNMP e Printer-MIB não cobrem ainda todos os hosts.
- Suprimentos são descobertos por LLD e descrições/índices variam entre
  fabricantes e dispositivos. O painel oferecerá navegação/drill-down por
  host; não fingirá que um único nível de toner representa toda a frota.
- Não há coleta implantada de filas de impressão, bandejas/tampas, consumo
  mensal agregado ou previsão de troca. Esses itens permanecem pendências da
  ficha e não serão simulados no dashboard.
- O dashboard apresenta o que está cadastrado no Zabbix; não altera a ação
  global, os hosts, interfaces, templates ou triggers.

### Plano de execução e rollback

1. Confirmar que não existe dashboard de impressoras equivalente.
2. Criar um dashboard chamado **Impressoras - Operacao**, com os cinco widgets
   descritos acima e grupo `Impressoras`.
3. Compartilhamento definido pelo usuário como **privado**; não adicionar
   compartilhamentos de grupo/usuário sem autorização posterior.
4. Conferir que cada widget carrega e permanece filtrado ao grupo; corrigir ou
   remover widget que apresente erro ou dados de outros grupos.
5. Validar visualmente problemas, severidades, disponibilidade e valores
   comuns sem gerar evento de teste.

### Resultado da execução

- Dashboard `Impressoras - Operacao`, ID `439`, salvo no Zabbix e confirmado
  como **privado**, sem compartilhamentos de usuários/grupos; página única
  chamada **Visão geral**.
- Os cinco widgets carregam para o grupo `Impressoras`: problemas recentes,
  problemas por severidade, disponibilidade SNMP, Top hosts e Item navigator.
- Problemas recentes mostra 25 de 32 entradas, incluindo uma linha recuperada;
  o widget de severidade mostra 12 `Average` e 19 `Warning`.
- Disponibilidade na validação: 36 disponíveis, 0 indisponíveis, 0 mistos e
  1 desconhecido, de 37 hosts.
- Top hosts mostra `printer.status` (valores numéricos, conforme o item atual)
  e `printer.pages.total`, com limite 37 e ordenação pela contagem de páginas.
  A disponibilidade desses itens depende da coleta por host.
- Item navigator está filtrado pelo grupo e mostra `100 de 100+` itens; ele
  permite buscar os itens descobertos de suprimentos e alertas por dispositivo.
- Durante a seleção das métricas do Top hosts, o seletor para
  `HSDS_HP_SETOR_1_ENF` marcou `Alerta 7 - severidade`
  (`printer.alert.severity[1.1]`) como `Not supported`. Isso contradiz a
  contagem unsupported anterior; quantidade total e causa ainda precisam ser
  apuradas, sem desabilitar ou remover itens por tentativa.
- A contagem anterior de zero problemas ativos não era válida: a consulta
  usou `r_eventid = 0`, mas eventos sem recuperação usam `r_eventid IS NULL`.
  A investigação desses alertas permanece pendente em F27-T012; nenhum foi
  reconhecido.
- A interface exibiu `Alerta não crítico ou severidade não mapeada: 6`; não
  alterar a interpretação no template sem conferir o valor e a regra
  Printer-MIB correspondentes.

## Dashboard individual ao abrir uma impressora (plano 2026-10-24)

### Problema observado

Ao abrir o host `HSDS_CANON_CE` (hostid `10889`) pelo endereço de host
dashboard informado, o Zabbix mostrou apenas o painel herdado **Network
interfaces**, com o gráfico `Interface {#IFNAME}({#IFALIAS}): Network traffic`
vazio. Esse é um host dashboard criado por template, não o dashboard global
`Impressoras - Operacao` (ID `439`). Os dashboards de host do Zabbix são
configurados em templates; o dashboard global não é aberto automaticamente
ao clicar num host.

### Plano antes da mudança

1. Adicionar ao template **IC Impressora SNMP** (ID `10887`) um novo host
   dashboard chamado **Impressora - Operacao**, sem apagar nem editar o
   painel herdado **Network interfaces**.
2. Configurar widgets nativos sem filtros globais de host:
   - **Problems**, para os problemas e recuperações do host atualmente aberto;
   - **Item navigator**, para explorar os itens comuns e os itens LLD de
     suprimentos do próprio host;
   - métricas comuns existentes no template (`printer.status` e
     `printer.pages.total`) somente se os widgets disponíveis no editor de
     dashboards de template permitirem selecionar esses itens e mostrar
     valores reais.
3. Não criar itens/LLD/triggers, alterar thresholds, reconhecer problemas,
   enviar mensagens, nem modificar dados de hosts.
4. Validar o painel no host `HSDS_CANON_CE` e em pelo menos um outro host que
   use o template: confirmar widgets não vazios quando houver dados, verificar
   que os nomes/valores pertencem ao host aberto e apontar itens sem histórico
   ou não suportados sem preencher com valores fictícios.
5. Se o tipo de widget não suportar a seleção dinâmica de dados do host no
   template dashboard, manter somente widgets realmente compatíveis; não
   colocar painéis de tráfego sem dados como substituto de métricas da
   impressora.

### Evidência técnica

A documentação oficial do Zabbix 8.0 estabelece que host dashboards são
configurados nos templates e gerados para cada host vinculado; seus widgets
mostram dados apenas do host selecionado. Dashboards globais pertencem à
seção Dashboards e não substituem esses painéis individuais.

- [Zabbix 8.0 — Host dashboards](https://www.zabbix.com/documentation/8.0/en/manual/web_interface/frontend_sections/monitoring/hosts/dashboards)
- [Zabbix 8.0 — Adicionar dashboards ao template](https://www.zabbix.com/documentation/8.0/en/manual/config/templates/template#adding-dashboards)

O rollback consiste em excluir apenas o dashboard criado nesta tarefa ou
remover widgets nele; não requer migração, alteração de host/template, restart
de container nem exclusão de histórico. Não executar operações de teste que
acionem notificações.

### Pesquisa oficial

- [Zabbix 8.0 — Dashboards](https://www.zabbix.com/documentation/8.0/en/manual/web_interface/frontend_sections/dashboards)
- [Zabbix 8.0 — Widgets de dashboard](https://www.zabbix.com/documentation/8.0/en/manual/web_interface/frontend_sections/dashboards/widgets)
- [Zabbix 8.0 — Problems widget](https://www.zabbix.com/documentation/8.0/en/manual/web_interface/frontend_sections/dashboards/widgets/problems)
- [Zabbix 8.0 — Top hosts widget](https://www.zabbix.com/documentation/8.0/en/manual/web_interface/frontend_sections/dashboards/widgets/top_hosts)
