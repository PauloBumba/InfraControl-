# Ficha Técnica: Computadores e Portáteis

> **Status**: Rascunho (2026-10-13)
> **Contexto**: Especificação Operations Center - Oitavo dispositivo a ser estudado
> **Referência**: Especificação do usuário (seção 2 - "Quais dispositivos devemos estudar?")

---

## Visão Geral

Computadores e portáteis são o oitavo caso de estudo porque:
- São endpoints usados por usuários (laptops, desktops, workstations)
- Requerem monitoramento de sistema operacional, recursos e antivírus
- Problemas de computador impactam produtividade dos usuários
- Monitoramento pode ser feito via agente (WMI para Windows, SNMP/agent para Linux)

**Áreas de foco** (baseadas na especificação):
- A. Identificação e configuração
- B. Sistema Operacional
- C. CPU e Processamento
- D. Memória
- E. Discos e Armazenamento
- F. Serviços e Processos
- G. Rede
- H. Antivírus e Segurança
- I. Eventos do Sistema Operacional
- J. Histórico e prevenção

---

## A. Identificação e Configuração

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Nome | Nome do computador | ✅ `Device.Name` | — |
| Fabricante | Vendor (Dell, HP, Lenovo, Apple, etc.) | ✅ `Device.Vendor` | — |
| Modelo | Modelo específico | ✅ `Device.Model` | — |
| Número de série | Serial number | ✅ `Device.SerialNumber` | — |
| Sistema Operacional | Windows/Linux version | ❌ Não persistido | **NOVO** |
| IP de gestão | Endereço IP de acesso | ✅ `Device.Ip` | — |
| Mac Address | MAC do computador | ✅ `NetworkInterface.MacAddress` | — |
| Tipo | Desktop/Laptop/Workstation | ✅ `Device.DeviceType` | — |
| Usuário principal | Usuário que usa o computador | ❌ Não modelado | **NOVO** |
| Localização | Local físico (sala, andar) | ✅ `Device.LocationId` → `Location` | — |
| Tempo de atividade (Uptime) | Tempo desde o último boot | ⚠️ Só via SNMP sob demanda | Persistir no Device |
| Referência Zabbix | Host ID no Zabbix | ✅ `ExternalDeviceReference` (Source=Zabbix) | — |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | OID / Endpoint | Disponibilidade |
|------|-------|---------------|-----------------|
| Nome (sysName) | SNMP | `1.3.6.1.2.1.1.5.0` | Universal (MIB-II) |
| Sistema Operacional (sysDescr) | SNMP | `1.3.6.1.2.1.1.1.0` | Universal (contém versão) |
| Uptime (hrSystemUptime) | SNMP | `1.3.6.1.2.1.25.1.1.0` | Universal (HOST-RESOURCES-MIB) |
| Serial | SNMP | Vendor-specific | Varia por fabricante |
| Usuário | WMI (Windows) / Agent (Linux) | Vendor-specific | Requer agente |

### Fonte de cada dado

| Dado | Fonte Primária | Fonte Secundária | Observação |
|------|----------------|------------------|------------|
| Nome, IP, Localização | **InfraControl** (Device) | — | System of Record |
| Sistema Operacional, Uptime | **SNMP/Agente** (sob demanda) | Zabbix (histórico) | Persistir no Device |
| Usuário principal | **Agente/WMI** | — | Requer agente |
| Referência Zabbix | **Zabbix sync** (F29-1) | — | ExternalDeviceReference |

### Campos do cadastro

| Campo | Obrigatório? | Padrão | Validação |
|-------|--------------|--------|-----------|
| Nome | ✅ Sim | — | Único global, case-insensitive |
| Tipo | ✅ Sim | Workstation/Laptop | Enum fixo |
| Fabricante | ⚠️ Opcional | — | Dropdown de vendors conhecidos |
| Modelo | ⚠️ Opcional | — | Texto livre |
| Número de série | ❌ Opcional | — | Texto livre |
| IP de gestão | ✅ Sim | — | IP válido, único global (conflito detector F29-4) |
| Localização | ✅ Sim | — | FK para Location |
| Usuário principal | ❌ Opcional | — | Texto livre |
| Comunidade SNMP | ⚠️ Opcional | — | Config de integração (por device ou global) |

---

## B. Sistema Operacional

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Sistema Operacional | Windows/Linux version | ❌ Não persistido | **NOVO** |
| Versão do OS | Versão específica (ex: Windows 11, Ubuntu 22.04) | ❌ Não persistido | **NOVO** |
| Última atualização | Data do último update | ❌ Não modelado | **NOVO** |
| Estado do agente | Agent running/stopped | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**WMI (Windows)**:

| Dado | Classe | Descrição |
|------|--------|-----------|
| OS | Win32_OperatingSystem | Nome, versão, build |
| LastBootUpTime | Win32_OperatingSystem | Último boot |
| ServicePack | Win32_OperatingSystem | Service pack version |

**SNMP (Linux)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| sysDescr | `1.3.6.1.2.1.1.1.0` | Contém versão do OS |
| sysUpTime | `1.3.6.1.2.1.1.3.0` | Uptime |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Sistema Operacional, versão | **WMI (Windows) / SNMP (Linux)** | Requer agente ou SNMP |
| Última atualização | **WMI / Agente** | Vendor-specific |
| Estado do agente | **Zabbix** | Via Zabbix agent |

---

## C. CPU e Processamento

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| CPU % (total) | Utilização total de CPU | ⚠️ Parcial (SnmpSystem check genérico) | Provider específico |
| CPU % (por core) | Utilização por núcleo | ❌ Não modelado | **NOVO** |
| System load average | Load average (1m/5m/15m) | ❌ Não modelado | **NOVO** (Linux-only) |
| Número de CPUs | Quantidade de núcleos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**WMI (Windows)**:

| Dado | Classe | Descrição |
|------|--------|-----------|
| CPU % | Win32_Processor | LoadPercentage |
| Number of cores | Win32_Processor | NumberOfCores |
| Number of logical processors | Win32_Processor | NumberOfLogicalProcessors |

**SNMP (Linux)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| hrProcessorLoad | `1.3.6.1.2.1.25.3.3.1.2.<hrProcessorIndex>` | CPU load % por processador |
| ssCpuRawUser | `1.3.6.1.4.1.2021.11.50` | Ticks CPU user (UCD-SNMP-MIB) |
| ssCpuRawSystem | `1.3.6.1.4.1.2021.11.52` | Ticks CPU system |
| ssCpuRawIdle | `1.3.6.1.4.1.2021.11.53` | Ticks CPU idle |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| CPU % (por core) | **WMI (Windows) / SNMP (Linux)** | Vendor-specific |
| System load average | **SNMP** (UCD-SNMP-MIB) | Linux-only |

---

## D. Memória

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

**WMI (Windows)**:

| Dado | Classe | Descrição |
|------|--------|-----------|
| Total memory | Win32_ComputerSystem | TotalPhysicalMemory |
| Free memory | Win32_OperatingSystem | FreePhysicalMemory |

**SNMP (Linux)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| hrMemorySize | `1.3.6.1.2.1.25.2.2` | Memória física total (KBytes) |
| memTotalReal | `1.3.6.1.4.1.2021.4.5` | Memória física total (KB) |
| memAvailReal | `1.3.6.1.4.1.2021.4.6` | Memória física disponível (KB) |
| memSwapTotal | `1.3.6.1.4.1.2021.4.3` | Swap total (KB) |
| memSwapFree | `1.3.6.1.4.1.2021.4.4` | Swap livre (KB) |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Memória física | **WMI (Windows) / SNMP (Linux)** | Vendor-specific |
| Swap | **SNMP** (UCD-SNMP-MIB) | Linux-only |

---

## E. Discos e Armazenamento

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Discos | Lista de discos/partições | ❌ Não modelado | **NOVO** |
| Capacidade total | Tamanho de cada disco | ❌ Não modelado | **NOVO** |
| Espaço usado | Espaço ocupado | ❌ Não modelado | **NOVO** |
| Espaço livre | Espaço disponível | ❌ Não modelado | **NOVO** |
| % de uso | Percentual de uso por disco | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**WMI (Windows)**:

| Dado | Classe | Descrição |
|------|--------|-----------|
| Disk | Win32_LogicalDisk | DeviceID, Size, FreeSpace |

**SNMP (Linux)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| hrStorageDescr | `1.3.6.1.2.1.25.2.3.1.3.<index>` | Descrição (ex: "/", "C:") |
| hrStorageAllocationUnits | `1.3.6.1.2.1.25.2.3.1.4.<index>` | Unidades de alocação (bytes) |
| hrStorageSize | `1.3.6.1.2.1.25.2.3.1.5.<index>` | Tamanho total (em unidades) |
| hrStorageUsed | `1.3.6.1.2.1.25.2.3.1.6.<index>` | Usado (em unidades) |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Discos/partições | **WMI (Windows) / SNMP (Linux)** | Vendor-specific |

---

## F. Serviços e Processos

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Processos em execução | Lista de processos | ❌ Não modelado | **NOVO** |
| Contagem de processos | Número de processos | ❌ Não modelado | **NOVO** |
| Serviços específicos | Estado de serviços críticos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**WMI (Windows)**:

| Dado | Classe | Descrição |
|------|--------|-----------|
| Process | Win32_Process | Nome, ID, CPU, memória |
| Service | Win32_Service | Nome, estado |

**SNMP (Linux)**:

| Dado | OID | Descrição |
|------|-----|-----------|
| hrSystemProcesses | `1.3.6.1.2.1.25.1.6.0` | Número de processos em execução |
| hrSWRunTable | `1.3.6.1.2.1.25.4.2` | Tabela de processos em execução |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Processos, serviços | **WMI (Windows) / SNMP (Linux)** | Vendor-specific |

---

## G. Rede

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

## H. Antivírus e Segurança

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Antivírus instalado | Nome do antivírus | ❌ Não modelado | **NOVO** |
| Estado do antivírus | Active/Inactive/Outdated | ❌ Não modelado | **NOVO** |
| Última atualização | Data da última atualização de definições | ❌ Não modelado | **NOVO** |
| Scan agendado | Próximo scan agendado | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

**WMI (Windows)**:

| Dado | Classe | Descrição |
|------|--------|-----------|
| Antivirus | Win32_Product | Nome do antivírus (se instalado via MSI) |
| Antivirus status | Vendor-specific WMI | Status do antivírus |

**Vendor-specific APIs**:

| Dado | Fonte | Descrição |
|------|-------|-----------|
| Antivirus status | API do antivírus | Status, última atualização, scan agendado |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Antivírus instalado, status | **WMI / API do antivírus** | Vendor-specific |

---

## I. Eventos do Sistema Operacional

### Dados que queremos visualizar

| Campo | Descrição | Estado Atual | Lacuna |
|-------|-----------|--------------|--------|
| Logs do sistema | Eventos do kernel/Windows | ❌ Não modelado | **NOVO** |
| Eventos de aplicação | Logs de aplicação | ❌ Não modelado | **NOVO** |
| Erros do sistema | Erros críticos | ❌ Não modelado | **NOVO** |

### Dados que o equipamento realmente disponibiliza

| Dado | Fonte | Disponibilidade |
|------|-------|-----------------|
| Logs do sistema | Event Log (Windows) / Syslog (Linux) | Requer integração específica |
| Eventos de aplicação | Event Log (Windows) / Syslog (Linux) | Requer integração específica |

### Fonte de cada dado

| Dado | Fonte Primária | Observação |
|------|----------------|------------|
| Logs do sistema | **Event Log (Windows) / Syslog (Linux)** | Fora escopo inicial - requer integração custom |

---

## J. Histórico e Prevenção

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
| Antivírus desatualizado | Última atualização > X dias | Antivírus status = Outdated | Atualizar definições |

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
| Computador não responde a ping | Critical | ✅ Ping check |

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
| Antivírus desatualizado (> 7 dias) | Warning | ❌ Novo |
| Antivírus inativo | Critical | ❌ Novo |
| Reinicialização não planejada | Critical | ❌ Novo |

---

## Operações Administrativas Permitidas

### Escopo inicial (somente monitoramento)

| Operação | Fonte | Implementação | Status |
|----------|-------|----------------|--------|
| Consultar estado atual | SNMP/WMI | Provider novo | ⚠️ A implementar |
| Consultar histórico de métricas | Zabbix API | Query Zabbix para histórico | ⚠️ Parcial |

### Operações futuras (fora escopo inicial)

| Operação | Justificativa | Pré-requisitos |
|----------|---------------|----------------|
| Reiniciar computador | Operação crítica | SSH/power control |
| Acesso remoto (RDP) | Operação crítica | F11 - Remote Access |
### Acesso Remoto (RDP/SSH)\n\nConforme especificação:\n> "Acesso remoto deve funcionar para computador, switch e servidor. Credenciais configuradas na plataforma para acesso via SSH."\n\n**Escopo atual**:\n- Credenciais RDP podem ser configuradas na plataforma via **Credential Store** (F11-T006)\n- Credenciais SSH podem ser configuradas na plataforma via **Credential Store** (F11-T006)\n- Credenciais são armazenadas de forma segura via ICredentialProtector\n- Acesso remoto autorizado: a plataforma fornece credenciais para acesso RDP (Windows) ou SSH (Linux)\n- Terminal integrado fica para fase futura
| Executar comando remoto | Operação crítica | SSH/agent |

---

## Tratamento de Dados Indisponíveis, Erros e Credenciais

### Dados indisponíveis

| Situação | Comportamento | Mensagem ao usuário |
|-----------|---------------|---------------------|
| Computador offline | Estado = Unknown, Cache último valor conhecido | "Computador offline. Última atualização: [timestamp]" |
| Agente/WMI indisponível | Estado = Degraded, Cache último valor conhecido | "Agente indisponível. Dados em cache." |
| SNMP community incorreta | Erro de autenticação, Log warn | "Erro de autenticação SNMP. Verifique community string." |
| OID não suportado | Campo = null, Log info | "Métrica não disponível neste sistema operacional." |

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
| WMI/Agente | Environment variable | Redação total em DTOs |

---

## Estratégia de Implementação

### Abordagem por plataforma

**1. SNMP universal - Prioridade Alta**:
- HOST-RESOURCES-MIB para CPU, memória, discos
- ifTable/ifXTable para tráfego
- Funciona tanto Windows quanto Linux

**2. Platform-specific opcional**:
- Windows: WMI para CPU detalhado, memória, discos, processos, serviços, antivírus
- Linux: UCD-SNMP-MIB para load average, memória detalhada, swap
- Fallback seguro: se platform-specific não disponível, usar SNMP universal

### Prioridade de Implementação

| Ordem | Item | Justificativa |
|-------|------|---------------|
| 1 | **SnmpWorkstationProvider** (HOST-RESOURCES-MIB) | Universal Windows/Linux |
| 2 | **WmiWorkstationProvider** (Windows específico) | WMI para dados detalhados |
| 3 | **SnmpWorkstationHealthProvider** (CPU, memória, discos) | HOST-RESOURCES-MIB |
| 4 | **Modelo de histórico/prevenção** | Limiar, tendência, disco esgotando |
| 5 | **Platform-specific providers** (UCD-SNMP para Linux) | Apenas se necessário |

---

## Estratégia de Teste

### Ambiente de homologação

| Requisito | Implementação |
|-----------|---------------|
| Computador real para testes | Windows ou Linux em laboratório (não produção) |
| Comunidade SNMP liberada | Read-only community configurada |
| WMI habilitado (Windows) | WMI ativado no dispositivo |
| Acesso Zabbix | Host criado no Zabbix de teste |
| Dados variados | Gerar carga de CPU, memória, disco para validar regras |

### Testes automatizados

| Tipo | Escopo |
|------|--------|
| Unit | Providers SNMP/WMI (SnmpWorkstationProvider, WmiWorkstationProvider) |
| Integration | Repository + Query com PostgreSQL |
| E2E | Ponta-a-ponta com computador real (gate de infra) |
| Mock | Simular respostas SNMP/WMI para cenários de erro |

---

## Próximos Passos (Recomendação)

1. **Prioridade imediata**:
   - Implementar campos faltantes em Device (Sistema Operacional, Uptime, Usuário principal)
   - Criar `SnmpWorkstationProvider` (HOST-RESOURCES-MIB - CPU/Memória/Discos)
   - Criar `WmiWorkstationProvider` (Windows específico - CPU/Memória/Discos/Processos/Serviços/Antivírus)

2. **Prioridade média**:
   - Implementar modelo de histórico (tendência de CPU/memória/disco)
   - Implementar modelo de prevenção (limiar, tendência, disco esgotando)
   - Adicionar `Device.Criticality` para computadores

3. **Prioridade baixa**:
   - Platform-specific providers (UCD-SNMP para Linux)
   - Integração Event Log/Syslog
   - Monitoramento de antivírus detalhado

---

## Referências

- RFC 2790: Host Resources MIB
- WMI Documentation: https://learn.microsoft.com/en-us/windows/win32/wmisdk/
- ADR-028: Arquitetura da Camada de Integrações
- ADR-048: Identidade Única de Device, Zabbix×UniFi via API
- F2-T008: DeviceProfile
- F11: Remote Access (RDP, SSH)
