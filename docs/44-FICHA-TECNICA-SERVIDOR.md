# Ficha Técnica: Servidor

> **Status**: Rascunho (2026-10-13)
> **Contexto**: Especificação Operations Center - Terceiro dispositivo a ser estudado
> **Referência**: Especificação do usuário (seção 2 - "Quais dispositivos devemos estudar?")

---

## Visão Geral

O servidor é o terceiro caso de estudo porque:
- É crítico para operações de TI (aplicações, bancos de dados, serviços)
- Requer monitoramento detalhado de CPU, RAM, discos e serviços
- Problemas de servidor impactam múltiplos usuários e sistemas
- HOST-RESOURCES-MIB oferece um padrão relativamente universal entre Windows e Linux

**Áreas de foco** (baseadas na especificação):
- A. Identificação e configuração
- B. CPU e Processamento
- C. Memória
- D. Discos e Armazenamento
- E. Serviços e Processos
- F. Rede
- G. Eventos do Sistema Operacional
- H. Histórico e Prevenção

---

## A. Identificação e Configuração

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Nome | Nome do servidor (hostname) | ✅ `Device.Name` | — |
| Fabricante | Vendor (Dell, HP, Lenovo, etc.) | ✅ `Device.Vendor` | — |
| Modelo | Modelo específico | ✅ `Device.Model` | — |
| Número de série | Serial number | ✅ `Device.SerialNumber` | — |
| Sistema Operacional | Windows/Linux version | ❌ Não persistido | **NOVO** |
| Firmware/BIOS | Versão do BIOS/firmware | ❌ Não persistido | **NOVO** |
| IP de gestão | Endereço IP de acesso | ✅ `Device.Ip` | — |
| Localização | Local físico (sala, rack) | ✅ `Device.LocationId` → `Location` | — |
| Rack | Rack onde está instalado | ✅ `Device.RackId` → `Rack` | — |
| Tempo de atividade (Uptime) | Tempo desde o último boot | ⚠️ Só via SNMP sob demanda | Persistir no Device |
| Referência Zabbix | Host ID no Zabbix | ✅ `ExternalDeviceReference` (Source=Zabbix) | — |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | OID / Endpoint | Disponibilidade |
|------|-------|---------------|-----------------|
| Nome (sysName) | SNMP | `1.3.6.1.2.1.1.5.0` | Universal (MIB-II) |
| Sistema Operacional (sysDescr) | SNMP | `1.3.6.1.2.1.1.1.0` | Universal (contém versão) |
| Uptime (hrSystemUptime) | SNMP | `1.3.6.1.2.1.25.1.1.0` | Universal (HOST-RESOURCES-MIB) |
| Serial | SNMP | Vendor-specific | Varia por fabricante |

### Fonte de cada dado

| Dado | Fonte Primária | Fonte Secundária | Observação |
|------|----------------|------------------|------------|
| Nome, IP, Localização, Rack | **InfraControl** (Device) | — | System of Record |
| Sistema Operacional, Uptime | **SNMP** (sob demanda) | Zabbix (histórico) | Persistir no Device |
| Referência Zabbix | **Zabbix sync** (F29-1) | — | ExternalDeviceReference |

### Campos do cadastro

| Campo | Obrigatório? | Padrão | Validação |
|-------|--------------|--------|-----------|
| Nome | ✅ Sim | — | Único global, case-insensitive |
| Tipo | ✅ Sim | Server | Enum fixo |
| Fabricante | ⚠️ Opcional | — | Dropdown de vendors conhecidos |
| Modelo | ⚠️ Opcional | — | Texto livre |
| Número de série | ❌ Opcional | — | Texto livre |
| IP de gestão | ✅ Sim | — | IP válido, único global (conflito detector F29-4) |
| Localização | ✅ Sim | — | FK para Location |
| Rack | ❌ Opcional | — | FK para Rack (exige localização) |
| Comunidade SNMP | ⚠️ Opcional | — | Config de integração (por device ou global) |

---

## B. CPU e Processamento

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| CPU % (total) | Utilização total de CPU | ⚠️ Parcial (SnmpSystem check genérico) | Provider específico |
| CPU % (por core) | Utilização por núcleo | ❌ Não modelado | **NOVO** |
| System load average | Load average (1m/5m/15m) | ❌ Não modelado | **NOVO** (Linux-only) |
| Número de CPUs | Quantidade de núcleos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**HOST-RESOURCES-MIB (RFC 2790) - Universal**:

| Dado | OID | Descrição |
|------|-----|-----------|
| hrProcessorLoad | `1.3.6.1.2.1.25.3.3.1.2.<hrProcessorIndex>` | CPU load % por processador |

**UCD-SNMP-MIB (Net-SNMP/Linux)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| ssCpuRawUser | `1.3.6.1.4.1.2021.11.50` | Ticks CPU user |
| ssCpuRawSystem | `1.3.6.1.4.1.2021.11.52` | Ticks CPU system |
| ssCpuRawIdle | `1.3.6.1.4.1.2021.11.53` | Ticks CPU idle |
| ssCpuRawWait | `1.3.6.1.4.1.2021.11.54` | Ticks CPU wait |
| ssCpuRawNice | `1.3.6.1.4.1.2021.11.51` | Ticks CPU nice |
| ssCpuRawKernel | `1.3.6.1.4.1.2021.11.55` | Ticks CPU kernel |
| ssCpuRawInterrupt | `1.3.6.1.4.1.2021.11.56` | Ticks CPU interrupt |
| ssCpuRawSoftIRQ | `1.3.6.1.4.1.2021.11.57` | Ticks CPU softirq |

**WINDOWS-NT-PERFORMANCE (Windows)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| memoryAvailableBytes | `1.3.6.1.4.1.311.1.1.3.1.1.1.1` | Memória disponível (bytes) |
| processor time counters | Vendor-specific | Utilização CPU via Performance Counters |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| CPU % (por core) | **SNMP** (hrProcessorLoad) | Universal HOST-RESOURCES-MIB |
| System load average | **SNMP** (UCD-SNMP-MIB) | Linux-only (Net-SNMP) |
| CPU detalhado (Windows) | **SNMP** (WINDOWS-NT-PERFORMANCE) | Windows-only |

---

## C. Memória

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Memória total | Total de RAM instalada | ❌ Não modelado | **NOVO** |
| Memória livre | RAM disponível | ❌ Não modelado | **NOVO** |
| Memória usada | RAM em uso | ❌ Não modelado | **NOVO** |
| Memória % | Percentual de uso | ❌ Não modelado | **NOVO** |
| Swap total | Total de swap/partição virtual | ❌ Não modelado | **NOVO** |
| Swap livre | Swap disponível | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**HOST-RESOURCES-MIB (RFC 2790) - Universal**:

| Dado | OID | Descrição |
|------|-----|-----------|
| hrMemorySize | `1.3.6.1.2.1.25.2.2` | Memória física total (KBytes) |
| hrStorageDescr | `1.3.6.1.2.1.25.2.3.1.3.<index>` | Descrição do storage (ex: "Physical memory", "/") |
| hrStorageAllocationUnits | `1.3.6.1.2.1.25.2.3.1.4.<index>` | Unidades de alocação (bytes) |
| hrStorageSize | `1.3.6.1.2.1.25.2.3.1.5.<index>` | Tamanho total (em unidades) |
| hrStorageUsed | `1.3.6.1.2.1.25.2.3.1.6.<index>` | Usado (em unidades) |

**Cálculo de bytes**: Bytes = value × hrStorageAllocationUnits

**Cálculo de porcentagem**: % = (hrStorageUsed / hrStorageSize) × 100

**UCD-SNMP-MIB (Net-SNMP/Linux)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| memTotalReal | `1.3.6.1.4.1.2021.4.5` | Memória física total (KB) |
| memTotalFree | `1.3.6.1.4.1.2021.4.11` | Memória livre (inclui swap) |
| memAvailReal | `1.3.6.1.4.1.2021.4.6` | Memória física disponível (KB) |
| memBuffer | `1.3.6.1.4.1.2021.4.14` | Memória em buffer (KB) |
| memCached | `1.3.6.1.4.1.2021.4.15` | Memória em cache (KB) |
| memShared | `1.3.6.1.4.1.2021.4.13` | Memória compartilhada (KB) |
| memSwapTotal | `1.3.6.1.4.1.2021.4.3` | Swap total (KB) |
| memSwapFree | `1.3.6.1.4.1.2021.4.4` | Swap livre (KB) |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Memória física | **SNMP** (hrStorageTable) | Universal HOST-RESOURCES-MIB |
| Swap | **SNMP** (UCD-SNMP-MIB) | Linux-only |
| Memória detalhada (Windows) | **SNMP** (WINDOWS-NT-PERFORMANCE) | Windows-only |

---

## D. Discos e Armazenamento

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Discos | Lista de discos/partições | ❌ Não modelado | **NOVO** |
| Capacidade total | Tamanho de cada disco | ❌ Não modelado | **NOVO** |
| Espaço usado | Espaço ocupado | ❌ Não modelado | **NOVO** |
| Espaço livre | Espaço disponível | ❌ Não modelado | **NOVO** |
| % de uso | Percentual de uso por disco | ❌ Não modelado | **NOVO** |
| I/O de disco | Throughput de leitura/escrita | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**HOST-RESOURCES-MIB (RFC 2790) - Universal**:

| Dado | OID | Descrição |
|------|-----|-----------|
| hrStorageDescr | `1.3.6.1.2.1.25.2.3.1.3.<index>` | Descrição (ex: "C:", "/") |
| hrStorageAllocationUnits | `1.3.6.1.2.1.25.2.3.1.4.<index>` | Unidades de alocação (bytes) |
| hrStorageSize | `1.3.6.1.2.1.25.2.3.1.5.<index>` | Tamanho total (em unidades) |
| hrStorageUsed | `1.3.6.1.2.1.25.2.3.1.6.<index>` | Usado (em unidades) |

**hrDiskStorageTable** (dispositivos físicos):

| Dado | OID | Descrição |
|------|-----|-----------|
| hrDiskStorageAccess | `1.3.6.1.2.1.25.3.6.1.2.<index>` | Tipo de acesso (read/write/read-only) |
| hrDiskStorageMedia | `1.3.6.1.2.1.25.3.6.1.3.<index>` | Tipo de mídia (HDD/SSD/etc) |
| hrDiskStorageRemovable | `1.3.6.1.2.1.25.3.6.1.4.<index>` | Removível (true/false) |
| hrDiskStorageCapacity | `1.3.6.1.2.1.25.3.6.1.5.<index>` | Capacidade (MBytes) |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Discos/partições | **SNMP** (hrStorageTable) | Universal HOST-RESOURCES-MIB |
| I/O de disco | **SNMP** (vendor-specific) | Fora escopo inicial |

---

## E. Serviços e Processos

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Processos em execução | Lista de processos | ❌ Não modelado | **NOVO** |
| Contagem de processos | Número de processos | ❌ Não modelado | **NOVO** |
| Serviços específicos | Estado de serviços críticos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**HOST-RESOURCES-MIB (RFC 2790) - Universal**:

| Dado | OID | Descrição |
|------|-----|-----------|
| hrSystemProcesses | `1.3.6.1.2.1.25.1.6.0` | Número de processos em execução |
| hrSWRunTable | `1.3.6.1.2.1.25.4.2` | Tabela de processos em execução |
| hrSWRunName | `1.3.6.1.2.1.25.4.2.1.2.<index>` | Nome do processo |
| hrSWRunPath | `1.3.6.1.2.1.25.4.2.1.4.<index>` | Caminho do executável |
| hrSWRunParameters | `1.3.6.1.2.1.25.4.2.1.5.<index>` | Parâmetros de linha de comando |
| hrSWRunStatus | `1.3.6.1.2.1.25.4.2.1.7.<index>` | Status (running/runnable/notRunnable) |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Contagem de processos | **SNMP** (hrSystemProcesses) | Universal HOST-RESOURCES-MIB |
| Lista de processos | **SNMP** (hrSWRunTable) | Universal HOST-RESOURCES-MIB |
| Serviços específicos | **Verificação custom** | Fora escopo inicial |

---

## F. Rede

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Interfaces | Lista de interfaces de rede | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |
| Tráfego | Bytes/s entrada/saída | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |
| Erros | Erros de rede | ⚠️ Parcial (F29-3 SNMP) | Já existe para switches |

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

## G. Eventos do Sistema Operacional

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Logs do sistema | Eventos do kernel/Windows | ❌ Não modelado | **NOVO** |
| Eventos de aplicação | Logs de aplicação | ❌ Não modelado | **NOVO** |
| Erros do sistema | Erros críticos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | Disponibilidade |
|------|-------|-----------------|
| Logs do sistema | Syslog (Linux) / Event Log (Windows) | Requer integração específica |
| Eventos de aplicação | Syslog / Event Log | Requer integração específica |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Logs do sistema | **Syslog / Event Log** | Fora escopo inicial - requer integração custom |

---

## H. Histórico e Prevenção

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Tendência de CPU | CPU ao longo do tempo | ❌ Não modelado | **NOVO** (prevenção) |
| Tendência de memória | RAM ao longo do tempo | ❌ Não modelado | **NOVO** (prevenção) |
| Tendência de disco | Espaço ao longo do tempo | ❌ Não modelado | **NOVO** (prevenção) |
| Disk esgotando | Previsão de espaço crítico | ❌ Não detectado | **NOVO** (prevenção) |
| Reinicializações | Contagem de reboots | ❌ Não modelado | **NOVO** (prevenção) |

### Modelo de Manutenção Preventiva (proposto)

| Problema | Sinal | Antecipação | Ação |
|----------|-------|-------------|------|
| CPU saturada | CPU > 90% sustained | CPU > 80% por 10 minutos | Investigar processos, escalonar |
| Memória esgotando | RAM > 90% | RAM > 80% e tendência ascendente | Investigar memory leaks, aumentar RAM |
| Disk esgotando | Disco > 90% | Disco > 80% e tendência de crescimento | Limpar logs, aumentar disco |
| Reinicializações recorrentes | > X reboots em Y dias | Reboot count em janela temporal | Investigar stability, hardware, bugs |

---

## Métricas que Precisam de Histórico

| Métrica | Período de retenção | Resolução | Justificativa |
|---------|-------------------|-----------|---------------|
| CPU % | 30 dias | 1 minuto | Detecção de sobrecarga |
| Memória % | 30 dias | 1 minuto | Detecção de memory leaks |
| Disco % | 90 dias | 5 minutos | Previsão de esgotamento |
| Uptime | Permanente | Evento (boot) | Detecção de reboots |
| Process count | 30 dias | 5 minutos | Detecção de anomalias |

**Nota**: O Zabbix pode manter histórico numérico. O InfraControl deve:
- Consultar o Zabbix para histórico detalhado (via API)
- Persistir apenas eventos agregados e tendências no PostgreSQL (REGRA 8 de ADR-028)

---

## Condições de Alerta

### Alertas reativos (existentes)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| Servidor não responde a ping | Critical | ✅ Ping check |

### Alertas preventivos (novos)

| Condição | Severidade | Implementação |
|----------|------------|----------------|
| CPU > 90% por 10 minutos | Critical | ❌ Novo |
| CPU > 80% por 5 minutos | Warning | ❌ Novo |
| Memória > 90% | Critical | ❌ Novo |
| Memória > 80% | Warning | ❌ Novo |
| Disco > 90% | Critical | ❌ Novo |
| Disco > 80% | Warning | ❌ Novo |
| Swap > 50% | Warning | ❌ Novo |
| Reinicialização não planejada | Critical | ❌ Novo |

---

## Operações Administrativas Permitidas

### Escopo inicial (somente monitoramento)

| Operação | Fonte | Implementação | Status |
|----------|-------|----------------|--------|
| Consultar estado atual | SNMP | Provider novo | ⚠️ A implementar |
| Consultar histórico de métricas | Zabbix API | Query Zabbix para histórico | ⚠️ Parcial |

### Operações futuras (fora escopo inicial)

| Operação | Justificativa | Pré-requisitos |
|----------|---------------|----------------|
| Integração Syslog (Linux) | Requer daemon syslog | Integração custom |
| Integração Event Log (Windows) | Requer WMI ou API | Integração custom |
| Gerenciar serviços | Operação crítica | Permissões granulares |
### Acesso Remoto (SSH/RDP)\n\nConforme especificação:\n> "Acesso remoto deve funcionar para computador, switch e servidor. Credenciais configuradas na plataforma para acesso via SSH."\n\n**Escopo atual**:\n- Credenciais SSH podem ser configuradas na plataforma via **Credential Store** (F11-T006)\n- Credenciais RDP podem ser configuradas na plataforma via **Credential Store** (F11-T006)\n- Credenciais são armazenadas de forma segura via ICredentialProtector\n- Acesso remoto autorizado: a plataforma fornece credenciais para acesso SSH (Linux) ou RDP (Windows)\n- Terminal integrado fica para fase futura

---

## Tratamento de Dados Indisponíveis, Erros e Credenciais

### Dados indisponíveis

| Situação | Comportamento | Mensagem ao usuário |
|-----------|---------------|---------------------|
| Servidor offline | Estado = Unknown, Cache último valor conhecido | "Servidor offline. Última atualização: [timestamp]" |
| SNMP community incorreta | Erro de autenticação, Log warn | "Erro de autenticação SNMP. Verifique community string." |
| OID não suportado | Campo = null, Log info | "Métrica não disponível neste sistema operacional." |
| Timeout SNMP | Retentativa (Polly), após 3 falhas → Degraded | "Tempo esgotado ao consultar servidor." |

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

---

## Estratégia de Implementação

### Abordagem de Providers SNMP

**1. Abstração universal (HOST-RESOURCES-MIB)**:
- HOST-RESOURCES-MIB (RFC 2790) é implementado tanto em Windows quanto Linux
- Criar `ServerHealthProvider` baseado em hrStorageTable, hrProcessorLoad, hrSystemProcesses
- Funciona para ambas as plataformas com o mesmo código

**2. OS-specific opcional**:
- Linux: UCD-SNMP-MIB para load average, memória detalhada (cached/buffer), swap
- Windows: WINDOWS-NT-PERFORMANCE para Performance Counters detalhados
- Fallback seguro: se OS-specific não disponível, usar HOST-RESOURCES-MIB universal

**3. Logs e eventos**:
- Fora escopo inicial
- Requer integração Syslog (Linux) ou Event Log (Windows)
- Pode ser adicionado em fase futura com task própria

### Prioridade de Implementação

| Ordem | Item | Justificativa |
|-------|------|---------------|
| 1 | **ServerHealthProvider** (HOST-RESOURCES-MIB) | Universal Windows/Linux |
| 2 | **ServerCpuProvider** (hrProcessorLoad) | Universal HOST-RESOURCES-MIB |
| 3 | **ServerDiskProvider** (hrStorageTable para discos) | Universal HOST-RESOURCES-MIB |
| 4 | **Modelo de histórico/prevenção** | Limiar, tendência, disco esgotando |
| 5 | **OS-specific providers** (UCD-SNMP, WINDOWS-NT-PERFORMANCE) | Apenas se necessário |

---

## Estratégia de Teste

### Ambiente de homologação

| Requisito | Implementação |
|-----------|---------------|
| Servidor real para testes | Servidor Windows ou Linux em laboratório (não produção) |
| Comunidade SNMP liberada | Read-only community configurada |
| Acesso Zabbix | Host criado no Zabbix de teste |
| Dados variados | Gerar carga de CPU, memória, disco para validar regras |

### Testes automatizados

| Tipo | Escopo |
|------|--------|
| Unit | Providers SNMP (ServerHealthProvider, novos providers) |
| Integration | Repository + Query com PostgreSQL |
| E2E | Ponta-a-ponta com servidor real (gate de infra) |
| Mock | Simular respostas SNMP para cenários de erro |

---

## Próximos Passos (Recomendação)

1. **Prioridade imediata**:
   - Implementar campos faltantes em Device (Sistema Operacional, Uptime)
   - Criar `ServerHealthProvider` (HOST-RESOURCES-MIB - CPU/Memória/Discos)
   - Criar `ServerCpuProvider` (hrProcessorLoad)
   - Criar `ServerDiskProvider` (hrStorageTable para discos)

2. **Prioridade média**:
   - Implementar modelo de histórico (diff de contadores para tendências)
   - Implementar modelo de prevenção (limiar, tendência, disco esgotando)
   - Adicionar `Device.Criticality` para servidores

3. **Prioridade baixa**:
   - OS-specific providers (UCD-SNMP para Linux, WINDOWS-NT-PERFORMANCE para Windows)
   - Integração Syslog/Event Log
   - Monitoramento de serviços específicos

---

## Referências

- RFC 2790: Host Resources MIB
- HOST-RESOURCES-MIB: `1.3.6.1.2.1.25`
- UCD-SNMP-MIB: `1.3.6.1.4.1.2021` (Net-SNMP/Linux)
- WINDOWS-NT-PERFORMANCE: `1.3.6.1.4.1.311` (Windows)
- ADR-028: Arquitetura da Camada de Integrações
- ADR-048: Identidade Única de Device, Zabbix×UniFi via API
- F2-T008: DeviceProfile
- SNMP Server Monitoring: https://snmp-monitoring.info/monitoring/server/


### Acesso Remoto (SSH/RDP)

Conforme especificação:
> Acesso remoto deve funcionar para computador, switch e servidor. Credenciais configuradas na plataforma para acesso via SSH.

**Escopo atual**:
- Credenciais SSH podem ser configuradas na plataforma via **Credential Store** (F11-T006)
- Credenciais RDP podem ser configuradas na plataforma via **Credential Store** (F11-T006)
- Credenciais são armazenadas de forma segura via 
- Acesso remoto autorizado: a plataforma fornece credenciais para acesso SSH (Linux) ou RDP (Windows)
- Terminal integrado fica para fase futura
