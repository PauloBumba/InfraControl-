# Cenário do Dia a Dia de TI: Monitoramento Operacional e Investigação

> **Status**: Rascunho (2026-10-13)
> **Contexto**: Ilustrar como a plataforma InfraControl seria usada na prática
> **Referência**: Especificação do usuário - centro de operações de TI

---

## Visão Geral

Este documento descreve um cenário realista do dia a dia de uma equipe de TI, mostrando como a plataforma InfraControl seria usada para:
- **Monitoramento operacional**: visibilidade contínua do estado da infraestrutura
- **Investigação de problemas**: diagnóstico rápido quando algo falha
- **Operação preventiva**: antecipar falhas antes que afetem usuários

**Ambiente do cenário**:
- Empresa com ~100 usuários
- Switches: 3 switches (corredor, salas, servidor)
- Impressoras: 5 multifunções (HP, Canon, Epson)
- Access Points: 10 APs UniFi (todo escritório)
- Servidores: 3 servidores (AD, arquivo, aplicação)
- Computadores: ~100 laptops/desktops
- Roteador/Firewall: 1 roteador + 1 firewall
- Telefones IP: 20 telefones Yealink

**Responsabilidades**:
- **Zabbix**: Coleta de métricas SNMP, histórico detalhado, triggers de alerta
- **UniFi Controller**: Métricas de rádio, clientes, sinal para APs
- **InfraControl**: Inventário central, visão unificada, análise preventiva, histórico de incidentes, correlação

---

## Cenário 1: Monitoramento Operacional - Manhã de Segunda

### 08:00 - Dashboard Operacional

O técnico de TI abre o dashboard da InfraControl:

**Visão geral:**
- Disponibilidade: 98.2% (3 dispositivos offline)
- Saúde dos sistemas: 2 warning, 0 critical
- Alertas recentes: 5 novos nas últimas 24h
- Atividades da plataforma: 2 incidentes em resolução

**Alertas recentes:**
1. **Critical**: Impressora-HP-Andar-3 offline (08:15)
2. **Warning**: CPU Servidor-Arquivo > 80% (07:30)
3. **Warning**: Toner Impressora-Canon-Recepção < 20% (ontem)
4. **Info**: AP-Sala-Conferência flapping (ontem)
5. **Info**: Backup Servidor-AD concluído (03:00)

### 08:15 - Investigação: Impressora Offline

**Passo 1: Identificar o problema**
- Técnico clica no alerta "Impressora-HP-Andar-3 offline"
- Tela de detalhe da impressora:
  - Estado: Offline (última atualização: 08:15)
  - Localização: Andar 3, Sala 302
  - IP: 192.168.1.53
  - Zabbix Host: HP-Andar-3 (status: down)
  - Ping: Failed (timeout)

**Passo 2: Correlação com outros dispositivos**
- Técnico verifica conexões do switch:
  - Switch: Switch-Corredor, Porta 15 → Impressora-HP-Andar-3
  - Estado da porta: Down
  - Topologia: Porta 15 está ligada ao switch, mas interface down

**Passo 3: Investigação remota**
- Técnico verifica se há outros dispositivos na sala 302:
  - Computador-Joao-Sala302: Online
  - Roteador da sala: Online
- Conclusão: Problema localizado na impressora ou cabo

**Passo 4: Ação**
- Técnico envia mensagem para o usuário da sala 302 via Teams
- Usuário confirma: impressora foi desligada acidentalmente
- Usuário liga a impressora
- Após 2 minutos: alerta "Resolved" aparece automaticamente

**Plataforma registra:**
- Incidente criado automaticamente às 08:15
- Histórico de ações: "Usuário confirmou desligamento acidental" (08:20)
- Incidente marcado como "Resolved" (08:22)
- Tempo total de resolução: 7 minutos

### 08:30 - Monitoramento Preventivo: Toner Baixo

**Alerta preventivo:**
- Warning: Toner Impressora-Canon-Recepção < 20% (ontem)
- Histórico de toner:
  - 30 dias atrás: 85%
  - 15 dias atrás: 60%
  - 7 dias atrás: 40%
  - Hoje: 18%

**Análise preventiva:**
- Tendência: consumo estável (~5% por dia)
- Previsão: toner esgota em ~3-4 dias
- Volume médio: ~500 páginas/semana

**Ação preventiva:**
- Técnico verifica o histórico de impressões
- Agenda: solicitar toner amanhã (manhã)
- Alerta: notificação enviada para o grupo de TI via n8n
- Usuário da recepção: notificado por email "Toner baixo - previsão 3 dias"

**Resultado:**
- Toner chega antes de esgotar
- Sem interrupção de impressão
- Usuários não afetados

---

## Cenário 2: Investigação de Problema - Usuário Relata Lentidão

### 14:30 - Usuário relata: "Internet lenta"

**Usuário:** João da Contabilidade
**Localização:** Sala 201, computador JOAO-PC-01
**Sintoma:** "Internet muito lenta, emails demoram para abrir"

### 14:35 - Investigação na Plataforma

**Passo 1: Verificar estado do computador**
- Tela de detalhe do computador:
  - Estado: Online
  - CPU: 15% (normal)
  - Memória: 60% (normal)
  - Disco C: 78% (normal)
  - Antivírus: Ativo, atualizado
  - Ping: 2ms (normal)

**Passo 2: Verificar rede**
- Switch: Switch-Corredor, Porta 8 → JOAO-PC-01
  - Estado da porta: Up
  - Erros: 0 (normal)
  - Tráfego: Baixo (normal)
- AP mais próximo: AP-Sala-201
  - Estado: Online
  - Utilização de rádio: 35% (normal)
  - Sinal médio: -55 dBm (bom)

**Passo 3: Verificar roteador/firewall**
- Roteador principal:
  - CPU: 45% (elevado)
  - Tráfego de entrada: Alta
  - Tráfego de saída: Alta
  - Sessões: 850 (acima do normal ~500)

**Passo 4: Investigação de tráfego**
- Técnico verifica topologia:
  - Switch-Corredor → Roteador → Internet
- Tráfego no roteador:
  - Porta WAN: 95% de utilização
  - Conclusão: saturação de link WAN

**Passo 5: Correlação com incidentes**
- Verifica incidentes recentes:
  - 14:00: Warning "Tráfego roteador > 80%"
  - 14:15: Warning "Tráfego roteador > 90%"
  - 14:20: Critical "Tráfego roteador > 95%"

**Investigação adicional:**
- Verifica quais IPs estão consumindo mais tráfego
- Descobre: Backup Servidor-Arquivo em andamento (saturação esperada)
- Verifica horário de backup: 14:00-15:00 (configurado)

**Conclusão:**
- Lentidão causada por backup saturando link WAN
- Não é problema de computador do usuário
- Backup termina às 15:00, situação normaliza

**Ação:**
- Técnico informa usuário: "Lentidão causada por backup, normaliza às 15:00"
- Usuário entende e espera
- Às 15:05: usuário confirma "Internet normal"

**Plataforma registra:**
- Investigação documentada com timestamp
- Correlação: tráfego WAN → backup
- Ação preventiva: ajustar horário de backup para horário de menor uso (manhã)

---

## Cenário 3: Operação Preventiva - Switch Degradação

### 10:00 - Alerta Preventivo Detectado

**Alerta preventivo:**
- Warning: Erros na porta 24 do Switch-Servidor aumentando continuamente
- Histórico de erros:
  - 7 dias atrás: 0.1 erros/hora
  - 3 dias atrás: 0.5 erros/hora
  - 1 dia atrás: 2 erros/hora
  - Hoje: 5 erros/hora

**Análise preventiva:**
- Tendência: ascendente (erro está crescendo)
- Porta 24 → Servidor-Aplicação (crítico)
- Tempo de operação: 5 anos
- Impacto: se porta falhar, servidor offline

**Investigação:**
- Técnico verifica histórico de incidentes:
  - Nenhum incidente relacionado a porta 24
- Verifica configuração da porta:
  - Velocidade: 1Gbps
  - Duplex: Full
  - Tráfego médio: 60% (normal)
- Verifica cabo:
  - InfraControl não tem dados físicos do cabo
  - Agendado: inspeção física

**Ação preventiva:**
- Técnico agenda visita à sala de servidores
- Inspeção física: cabo parece danificado (esmagado)
- Ação: substituir cabo preventivamente antes de falha
- Após substituição: erros voltam para 0.1 erros/hora

**Resultado:**
- Falha evitada
- Servidor nunca ficou offline
- Tempo de parada planejada: 10 minutos (substituição do cabo)
- Sem impacto em usuários

**Plataforma registra:**
- Alerta preventivo criado automaticamente
- Tendência documentada com gráfico
- Ação preventiva registrada
- Incidente evitado (métrica de prevenção)

---

## Cenário 4: Operação de Monitoramento - Falha em AP

### 16:45 - Usuário relata: "Wi-Fi caiu"

**Usuário:** Maria do RH
**Localização:** Sala 105
**Sintoma:** "Wi-Fi caiu há 10 minutos"

### 16:50 - Investigação na Plataforma

**Passo 1: Verificar AP**
- AP-Sala-105:
  - Estado: Online (ping OK)
  - UniFi Controller: AP online
  - Rádio 2.4GHz: Up
  - Rádio 5GHz: Down (crítico)
  - Clientes conectados: 0 (anormal)

**Passo 2: Verificar topologia**
- Switch: Switch-Corredor, Porta 22 → AP-Sala-105
  - Estado da porta: Up
  - PoE: OK (29W fornecidos)
  - Tráfego: Normal

**Passo 3: Verificar eventos UniFi**
- Log do AP: "Rádio 5GHz parado devido a temperatura alta"
- Temperatura do AP: 75°C (acima do limite de 70°C)
- Conclusão: AP superaqueou, rádio 5GHz parou automaticamente

**Passo 4: Correlação com ambiente**
- Verifica condição da sala:
  - Sala 105: janelas fechadas, ar condicionado desligado
  - Temperatura ambiente: 28°C (alta)
- Conclusão: calor excessivo na sala

**Ação:**
- Técnico aciona ar condicionado da sala
- Após 10 minutos: temperatura do AP volta para 45°C
- Rádio 5GHz: volta automaticamente
- Usuário confirma: "Wi-Fi voltou"

**Plataforma registra:**
- Alerta: "AP-Sala-105 rádio 5GHz parado (temperatura alta)"
- Incidente criado automaticamente
- Ação registrada: "Ar condicionado ativado"
- Incidente resolvido às 17:05
- Prevenção: alerta de temperatura configurado para esta sala

---

## Cenário 5: Operação Preventiva - Servidor Disco Esgotando

### 09:00 - Alerta Preventivo Detectado

**Alerta preventivo:**
- Warning: Disco do Servidor-Arquivo esgotando
- Histórico de espaço:
  - 30 dias atrás: 60%
  - 15 dias atrás: 75%
  - 7 dias atrás: 85%
  - Hoje: 92%

**Análise preventiva:**
- Tendência: ascendente (disco esgotando)
- Taxa de crescimento: ~1% por dia
- Previsão: disco cheio em ~8 dias
- Impacto: servidor pararia, usuários não acessariam arquivos

**Investigação:**
- Técnico verifica o que está ocupando espaço:
  - Backups: 200 GB (normal)
  - Logs: 50 GB (pode ser limpo)
  - Arquivos de usuários: 300 GB (crescendo)
- Verifica política de retenção:
  - Logs são retidos por 90 dias
  - Logs antigos podem ser limpos

**Ação preventiva:**
- Técnico limpa logs antigos (mais de 90 dias)
- Espaço liberado: 30 GB
- Disco volta para: 87%
- Nova previsão: ~13 dias
- Agenda: adicionar disco ao servidor (30 dias)
- Configura alerta crítico em 95% (para ação imediata)

**Resultado:**
- Disco não esgota
- Usuários não afetados
- Ação preventiva documentada
- Planejamento de expansão

**Plataforma registra:**
- Alerta preventivo criado automaticamente
- Tendência documentada com gráfico
- Ação preventiva registrada
- Planejamento de expansão (task criada)

---

## Cenário 6: Investigação de Impressora - Atolamentos Recorrentes

### 11:30 - Usuário relata: "Impressora atolando sempre"

**Usuário:** Ana do Financeiro
**Impressora:** Impressora-Epson-Financeiro
**Sintoma:** "Atolamentos toda vez que imprime faturas"

### 11:35 - Investigação na Plataforma

**Passo 1: Verificar estado da impressora**
- Impressora-Epson-Financeiro:
  - Estado: Online
  - Toner: 65% (normal)
  - Papel: OK
  - Bandeja de saída: OK
  - Fila de impressão: 3 jobs pendentes

**Passo 2: Verificar histórico de incidentes**
- Últimos 30 dias:
  - 15 incidentes de atolamento
  - Padrão: todos relacionados a faturas (formato A4)
  - Frequência: ~1 atolamento por 2 dias

**Passo 3: Verificar contador de páginas**
- Total de páginas: 45.000
- Vida útil do fusor: 100.000 páginas
- Conclusão: fusor está com 45% da vida útil

**Investigação adicional:**
- Verifica tipo de papel: faturas usam papel de 90g (mais espesso)
- Verifica bandeja de entrada: papel A4, capacidade 250 folhas
- Conclusão: papel espesso + fusor degradando = atolamentos

**Ação:**
- Técnico agenda troca do fusor (preventiva)
- Enquanto isso: orienta usuário a usar papel de 80g para faturas
- Após troca do fusor: atolamentos param

**Resultado:**
- Problema resolvido
- Usuário satisfeito
- Prevenção: alerta configurado para fusor < 40%

**Plataforma registra:**
- Incidente criado automaticamente
- Histórico de atolamentos documentado
- Ação preventiva registrada
- Prevenção implementada

---

## Cenário 7: Operação de Monitoramento - Acesso Remoto

### 15:00 - Usuário relata: "Não consigo acessar servidor"

**Usuário:** Técnico de TI João
**Servidor:** Servidor-Aplicação
**Sintoma:** "Não consigo fazer SSH"

### 15:05 - Investigação na Plataforma

**Passo 1: Verificar estado do servidor**
- Servidor-Aplicação:
  - Estado: Online (ping OK)
  - CPU: 30% (normal)
  - Memória: 45% (normal)
  - Porta 22 (SSH): Verificado via switch: Up

**Passo 2: Verificar credenciais**
- Plataforma: credenciais SSH configuradas para Servidor-Aplicação
- Credenciais armazenadas via Credential Store (F11-T006)
- Última atualização: 7 dias atrás

**Passo 3: Acesso remoto via plataforma**
- Técnico clica em "Acesso Remoto SSH" na tela do servidor
- Plataforma fornece credenciais de forma segura
- Técnico usa SSH com credenciais fornecidas
- Acesso funciona normalmente

**Conclusão:**
- Problema era do usuário (usuário estava usando credenciais erradas)
- Plataforma forneceu acesso com credenciais corretas
- Usuário confundido

**Ação:**
- Técnico orienta usuário a usar credenciais corretas
- Plataforma não altera nada (apenas fornece credenciais)

**Plataforma registra:**
- Acesso remoto registrado no log de auditoria
- Sem incidente criado (não houve falha de infraestrutura)

---

## Resumo dos Cenários

| Cenário | Tipo | Dispositivo | Resolução | Tempo |
|---------|------|------------|-----------|-------|
| Impressora offline | Reativo | Impressora HP | Usuário ligou impressora | 7 min |
| Internet lenta | Investigação | Roteador | Backup saturando link WAN | 30 min |
| Switch degradação | Preventivo | Switch | Substituição preventiva de cabo | 10 min (ação) |
| AP Wi-Fi caiu | Investigação | AP UniFi | Ar condicionado ativado | 20 min |
| Servidor disco esgotando | Preventivo | Servidor | Limpeza de logs + planejamento | 15 min |
| Impressora atolamentos | Investigação | Impressora Epson | Troca de fusor preventiva | 1 dia (ação) |
| Acesso SSH falha | Investigação | Servidor | Credenciais fornecidas pela plataforma | 5 min |

**Benefícios da plataforma:**
1. **Visibilidade unificada**: todos os dispositivos em um só lugar
2. **Correlação rápida**: relacionar eventos entre dispositivos
3. **Prevenção**: antecipar falhas antes que afetem usuários
4. **Histórico**: aprender com incidentes passados
5. **Acesso remoto seguro**: credenciais centralizadas, não expostas

**Próximos passos:**
- Implementar fichas técnicas conforme prioridade
- Criar dashboard de prevenção (tendências, alertas preventivos)
- Implementar acesso remoto integrado (fase futura)
- Criar relatórios de incidentes e prevenção
