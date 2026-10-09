# 37. F27 — Monitoramento completo da infraestrutura com Zabbix + InfraControl (checklist vivo)

- **Task/phase:** F27 (Fase 27) — plano/checklist entregue pelo usuário em 2026-10-08
- **Status:** documentado — **não iniciado**; Fase 1 (auditoria) é o primeiro passo
  e pressupõe build + testes de baseline executados antes de qualquer alteração
  (§Fase 1.1)
- **Relação com outros docs:** integração Zabbix existente em
  `docs/27-INTEGRACAO-ZABBIX.md` · ADR-023 (ingestão de problemas) · ADR-027
  (MonitoringEvent) · ADR-028 (monitoramento) · F26 em `docs/36` · contrato do
  endpoint existente `POST /api/v1/integrations/zabbix/problems` · regras
  transversais: `AGENTS.md` + "Regras obrigatórias de execução" ao fim
- **Regra de atualização:** este checklist só avança com **evidência** (build +
  testes + validação real) — doc/entidade/tela isolada não é implementação
  concluída; cada fase só é marcada como concluída após validação (regra 14)

> **Texto integral do plano preservado abaixo** (apenas ajuste de níveis de
> título para o documento); nenhum item reescrito.

---

## Objetivo

Integrar o InfraControl ao Zabbix existente e proporcionar uma visão centralizada de toda a infraestrutura de TI, incluindo:

- Impressoras.
- Câmeras e gravadores de vídeo.
- Switches e Access Points.
- UniFi, Aruba, HP, Intelbras e TP-Link.
- Telefones IP.
- Servidores físicos.
- Máquinas virtuais e hypervisors.
- Roteadores, firewalls e links de rede.
- Serviços, aplicações, bancos de dados e outros componentes identificados na infraestrutura.

O InfraControl deverá consultar os dados do Zabbix, relacioná-los ao inventário e apresentar estado operacional, métricas, problemas, histórico e contexto dos equipamentos.

O Zabbix continuará responsável pela coleta, avaliação de triggers, métricas e geração de problemas de monitoramento.

**Regra:** antes de implementar qualquer recurso, auditar o que já existe no Zabbix e no InfraControl. Não recriar coletores nem duplicar funcionalidades existentes.

---

## FASE 1 — Auditoria completa e mapeamento da infraestrutura

### 1.1. Auditoria do InfraControl

- [ ] Inspecionar a solução e a arquitetura atual.
- [ ] Identificar o endpoint existente `POST /api/v1/integrations/zabbix/problems`.
- [ ] Inspecionar a ADR-023 e preservar contratos existentes.
- [ ] Identificar serviços, entidades, endpoints, páginas e testes já implementados.
- [ ] Identificar os módulos de inventário, monitoramento, incidentes, histórico, auditoria e dashboards.
- [ ] Identificar os mecanismos existentes de jobs, workers, logs e observabilidade.
- [ ] Verificar autenticação, autorização e configuração de integrações.
- [ ] Executar o build e os testes antes de realizar alterações.
- [ ] Registrar implementações concluídas, parciais e ausentes.

### 1.2. Auditoria do Zabbix

- [ ] Identificar versão e arquitetura da instalação.
- [ ] Identificar servidores, proxies e demais componentes existentes.
- [ ] Verificar conectividade e autenticação da API.
- [ ] Listar hosts, grupos, templates, interfaces, itens, triggers e problemas.
- [ ] Identificar hosts desativados e itens sem suporte.
- [ ] Identificar métodos de coleta: agente, SNMP, ICMP, HTTP, IPMI, JMX, ODBC, APIs e outros existentes.
- [ ] Verificar regras de descoberta automática.
- [ ] Identificar templates associados a cada categoria de equipamento.
- [ ] Verificar histórico, tendências e retenção.
- [ ] Identificar alertas, ações, webhooks e notificações existentes.
- [ ] Verificar permissões e limites de acesso.
- [ ] Identificar hosts duplicados e equipamentos sem monitoramento adequado.

### 1.3. Inventário da infraestrutura

Criar um levantamento dos equipamentos já existentes, incluindo:

- [ ] Nome e categoria.
- [ ] Fabricante e modelo, quando conhecidos.
- [ ] IP, MAC e hostname, quando disponíveis.
- [ ] Localização física, setor ou unidade.
- [ ] Site, VLAN e segmento de rede, quando conhecidos.
- [ ] Identificador do host no Zabbix.
- [ ] Método de monitoramento utilizado.
- [ ] Template associado.
- [ ] Estado atual da coleta.
- [ ] Métricas e alertas disponíveis.
- [ ] Responsável e criticidade, caso cadastrados.

Não presumir que todos os dispositivos estejam configurados ou monitorados. A auditoria deve mostrar a diferença entre inventário conhecido, host existente no Zabbix e equipamento efetivamente monitorado.

### 1.4. Entregáveis

- [ ] Relatório da auditoria.
- [ ] Inventário inicial.
- [ ] Matriz de cobertura de monitoramento.
- [ ] Lista de hosts duplicados ou sem correspondência.
- [ ] Lista de métricas disponíveis e ausentes.
- [ ] Plano de correção das lacunas encontradas.

---

## FASE 2 — Fundação da integração Zabbix

### 2.1. Cliente da API

- [ ] Implementar ou completar `IZabbixClient`.
- [ ] Implementar ou completar `ZabbixClient`.
- [ ] Utilizar JSON-RPC 2.0 conforme a versão instalada.
- [ ] Implementar autenticação e gestão segura de credenciais.
- [ ] Implementar timeout, cancelamento e tratamento de erros.
- [ ] Implementar paginação e limites de resultados.
- [ ] Tratar falhas HTTP, erros JSON-RPC e respostas inválidas.
- [ ] Implementar consultas de versão, hosts, grupos, templates, itens, triggers, eventos e problemas conforme os métodos disponíveis.
- [ ] Implementar consultas históricas com limites de período e volume.
- [ ] Mapear respostas externas para contratos internos.
- [ ] Evitar expor tokens ou dados sensíveis em logs.
- [ ] Reutilizar os padrões existentes de configuração e observabilidade.

### 2.2. Configuração administrativa

Criar ou completar `Administração → Integrações → Zabbix`.

- [ ] Configurar URL e acesso à API.
- [ ] Armazenar credenciais de forma segura.
- [ ] Implementar teste de conexão.
- [ ] Exibir versão detectada, estado e última comunicação.
- [ ] Exibir última sincronização bem-sucedida.
- [ ] Exibir erros recentes sem revelar segredos.
- [ ] Permitir sincronização manual.
- [ ] Restringir configurações a administradores autorizados.
- [ ] Documentar permissões necessárias.

### 2.3. Critérios de aceite

- [ ] Conexão validada contra o Zabbix existente.
- [ ] Autenticação funcional.
- [ ] Credenciais protegidas.
- [ ] Erros tratados sem derrubar o InfraControl.
- [ ] Build e testes relevantes aprovados.

---

## FASE 3 — Inventário de hosts e descoberta

### 3.1. Sincronização de hosts

- [ ] Consultar hosts e grupos.
- [ ] Persistir identificadores externos e atributos relevantes.
- [ ] Consultar interfaces, endereços e estado de disponibilidade.
- [ ] Consultar templates e tags quando necessários.
- [ ] Atualizar registros existentes sem duplicações.
- [ ] Tratar hosts desativados, removidos ou inacessíveis.
- [ ] Registrar a data da última atualização.
- [ ] Preservar histórico e relacionamentos.

### 3.2. Descoberta

- [ ] Auditar as regras de Network Discovery existentes.
- [ ] Identificar regras de Low-Level Discovery.
- [ ] Identificar hosts descobertos automaticamente.
- [ ] Avaliar descoberta de interfaces, sistemas de arquivos e recursos de rede.
- [ ] Identificar recursos descobertos que ainda não possuem monitoramento adequado.
- [ ] Evitar criar novas regras que dupliquem as existentes.
- [ ] Documentar recursos que exigem configuração manual.
- [ ] Não adicionar automaticamente todos os dispositivos encontrados sem política e validação.

### 3.3. Interface

Criar ou completar `Infraestrutura → Hosts monitorados`.

- [ ] Pesquisar por nome, IP, MAC e identificador.
- [ ] Filtrar por grupo, categoria, fabricante e estado quando disponíveis.
- [ ] Exibir host, origem, método de monitoramento e última atualização.
- [ ] Abrir detalhes do host.
- [ ] Exibir problemas e métricas associados.
- [ ] Identificar hosts sem Asset correspondente.
- [ ] Identificar hosts com monitoramento incompleto.

### 3.4. Critérios de aceite

- [ ] Hosts sincronizados corretamente.
- [ ] Descobertas não geram duplicações.
- [ ] Dados reais são diferenciados de dados indisponíveis.
- [ ] Falha de consulta não é interpretada como host offline.

---

## FASE 4 — Monitoramento de impressoras

### 4.1. Cobertura de impressoras

Abranger HP, Brother e demais fabricantes presentes no inventário.

- [ ] Identificar impressoras de rede.
- [ ] Verificar suporte a SNMP e templates compatíveis.
- [ ] Verificar impressoras acessíveis somente por USB ou servidor de impressão.
- [ ] Identificar estado de disponibilidade.
- [ ] Monitorar toner ou tinta quando suportado.
- [ ] Monitorar cilindro, unidade de imagem ou outros consumíveis quando disponíveis.
- [ ] Consultar contadores de páginas quando suportados.
- [ ] Identificar erros, estados de papel e alertas do dispositivo quando expostos.
- [ ] Monitorar interfaces de rede quando aplicável.
- [ ] Identificar impressoras sem métricas de consumíveis.

### 4.2. Templates e coleta

- [ ] Auditar templates já existentes.
- [ ] Reutilizar templates oficiais ou compatíveis com o modelo.
- [ ] Validar OIDs e tipos de dados antes de criar itens SNMP personalizados.
- [ ] Evitar criar itens duplicados.
- [ ] Configurar intervalos de coleta adequados.
- [ ] Validar os valores coletados contra o dispositivo real.

### 4.3. Alertas

- [ ] Impressora indisponível.
- [ ] Falha de coleta persistente.
- [ ] Toner ou tinta abaixo dos limites configurados.
- [ ] Cilindro ou consumível abaixo do limite, quando disponível.
- [ ] Erros de dispositivo relevantes.
- [ ] Alertas configuráveis conforme a criticidade.

### 4.4. Interface

- [ ] Listar impressoras por setor e localização.
- [ ] Exibir estado, modelo, IP e última atualização.
- [ ] Exibir consumíveis disponíveis.
- [ ] Exibir problemas ativos.
- [ ] Identificar impressoras sem métricas de consumíveis.

---

## FASE 5 — Câmeras, gravadores e videomonitoramento

### 5.1. Equipamentos

- [ ] Identificar câmeras IP e gravadores existentes.
- [ ] Incluir Intelbras e outros fabricantes presentes.
- [ ] Identificar modelos, endereços e interfaces.
- [ ] Verificar suporte a SNMP, ICMP, HTTP ou APIs específicas.
- [ ] Monitorar disponibilidade e perda de comunicação.
- [ ] Monitorar métricas de sistema, interfaces e armazenamento quando disponíveis.
- [ ] Verificar recursos de canais, discos e gravação dos gravadores quando expostos.
- [ ] Identificar equipamentos sem método de coleta adequado.

### 5.2. Alertas

- [ ] Câmera ou gravador indisponível.
- [ ] Falha de comunicação persistente.
- [ ] Problemas de armazenamento identificáveis.
- [ ] Falhas de coleta.
- [ ] Mudanças de estado relevantes fornecidas pelo equipamento.

Não presumir que o Zabbix consiga validar a gravação de vídeo de todas as câmeras. Isso depende dos recursos e interfaces disponibilizados pelo equipamento.

### 5.3. Interface

- [ ] Listar câmeras e gravadores.
- [ ] Agrupar por setor e localização.
- [ ] Exibir disponibilidade, modelo e última atualização.
- [ ] Exibir problemas e métricas suportadas.
- [ ] Diferenciar câmera offline de falha do mecanismo de consulta.

---

## FASE 6 — Rede, switches, Access Points e fabricantes

### 6.1. Equipamentos contemplados

- [ ] UniFi.
- [ ] Aruba.
- [ ] HP.
- [ ] Intelbras.
- [ ] TP-Link.
- [ ] Outros fabricantes encontrados no inventário.

### 6.2. Switches

- [ ] Monitorar disponibilidade.
- [ ] Monitorar interfaces e portas quando suportadas.
- [ ] Monitorar tráfego de entrada e saída.
- [ ] Monitorar erros, descartes e estado das interfaces.
- [ ] Monitorar velocidade e duplex quando disponíveis.
- [ ] Consultar CPU, memória, temperatura e fontes de alimentação quando suportadas.
- [ ] Identificar portas com problemas persistentes.
- [ ] Identificar interfaces sem coleta de métricas esperadas.
- [ ] Verificar templates e descoberta de interfaces.
- [ ] Evitar duplicar itens e triggers existentes.

### 6.3. Access Points

- [ ] Monitorar disponibilidade.
- [ ] Monitorar interfaces e tráfego quando disponíveis.
- [ ] Monitorar estado e recursos do equipamento quando suportados.
- [ ] Consultar clientes conectados quando houver integração ou API compatível.
- [ ] Identificar problemas de comunicação.
- [ ] Identificar APs sem métricas suficientes.

### 6.4. Roteadores, firewalls e links

- [ ] Identificar roteadores e firewalls existentes.
- [ ] Monitorar interfaces e conectividade.
- [ ] Monitorar tráfego e erros quando suportados.
- [ ] Monitorar CPU, memória e temperatura quando disponíveis.
- [ ] Identificar links WAN e seus indicadores de disponibilidade.
- [ ] Implementar verificações de serviços e destinos críticos quando aplicável.
- [ ] Diferenciar falha de link, equipamento indisponível e falha de consulta.

### 6.5. Critérios de aceite

- [ ] Templates e métodos compatíveis com cada modelo.
- [ ] Métricas verificadas em equipamentos reais.
- [ ] Interfaces identificadas corretamente.
- [ ] Descoberta e sincronização sem duplicações.
- [ ] Limitações de cada fabricante documentadas.

---

## FASE 7 — Telefonia e serviços de comunicação

### 7.1. Telefonia

- [ ] Identificar telefones IP, centrais telefônicas e gateways existentes.
- [ ] Verificar os métodos de monitoramento suportados por cada modelo.
- [ ] Monitorar disponibilidade dos equipamentos.
- [ ] Monitorar interfaces e recursos SNMP quando disponíveis.
- [ ] Monitorar serviços da central por checks apropriados.
- [ ] Consultar estado de registros ou ramais somente quando houver interface autorizada e compatível.
- [ ] Não coletar conteúdo de chamadas ou informações pessoais desnecessárias.

### 7.2. Alertas

- [ ] Equipamento indisponível.
- [ ] Serviço da central indisponível.
- [ ] Falha persistente de comunicação.
- [ ] Indicadores de capacidade ou recursos críticos quando suportados.

### 7.3. Interface

- [ ] Listar telefones, centrais e gateways.
- [ ] Exibir estado e problemas ativos.
- [ ] Agrupar por setor ou localização.
- [ ] Mostrar métricas realmente disponíveis.

---

## FASE 8 — Servidores físicos, máquinas virtuais e plataformas

### 8.1. Servidores físicos

- [ ] Identificar servidores e sistemas operacionais.
- [ ] Verificar instalação e versão dos agentes Zabbix.
- [ ] Monitorar CPU, memória, swap e processos relevantes.
- [ ] Monitorar discos, sistemas de arquivos e espaço disponível.
- [ ] Monitorar interfaces de rede.
- [ ] Monitorar serviços essenciais.
- [ ] Monitorar logs quando houver configuração apropriada.
- [ ] Monitorar hardware via IPMI ou mecanismos do fabricante quando suportados.
- [ ] Identificar servidores sem agente ou com coleta incompleta.

### 8.2. Máquinas virtuais e hypervisors

- [ ] Identificar a plataforma de virtualização instalada.
- [ ] Verificar se existe VMware, Hyper-V, Proxmox ou outra plataforma.
- [ ] Escolher o método de monitoramento adequado à plataforma.
- [ ] Monitorar hypervisors e recursos de virtualização.
- [ ] Monitorar VMs e os recursos disponibilizados.
- [ ] Correlacionar VM, hypervisor e sistema operacional quando possível.
- [ ] Evitar cadastrar a mesma VM duas vezes por mecanismos de descoberta diferentes.
- [ ] Identificar VMs sem agente ou sem métricas suficientes.

### 8.3. Aplicações e bancos de dados

- [ ] Identificar serviços e aplicações críticos.
- [ ] Verificar templates e agentes compatíveis.
- [ ] Monitorar disponibilidade dos serviços.
- [ ] Monitorar bancos de dados por métodos apropriados.
- [ ] Monitorar endpoints HTTP e certificados quando relevantes.
- [ ] Monitorar filas e dependências de aplicações quando suportadas.
- [ ] Identificar falhas de serviço mesmo quando o servidor está acessível.

### 8.4. Critérios de aceite

- [ ] Servidores e VMs com métricas validadas.
- [ ] Serviços críticos identificados.
- [ ] Alertas testados.
- [ ] Relacionamentos entre VMs e hypervisors preservados quando disponíveis.

---

## FASE 9 — Normalização, Assets e visão unificada

### 9.1. Correlação

- [ ] Relacionar hosts do Zabbix aos Assets existentes.
- [ ] Utilizar identificadores externos estáveis.
- [ ] Usar MAC, hostname e IP como evidências auxiliares.
- [ ] Evitar usar apenas IP como identidade do equipamento.
- [ ] Permitir associação manual quando houver ambiguidade.
- [ ] Identificar equipamentos monitorados sem Asset.
- [ ] Preservar histórico dos relacionamentos.
- [ ] Evitar duplicações durante sincronizações.
- [ ] Distinguir o inventário físico do host lógico de monitoramento.

### 9.2. Categorias e metadados

- [ ] Padronizar categorias de equipamento.
- [ ] Padronizar fabricantes e modelos quando conhecidos.
- [ ] Padronizar localização, setor e criticidade quando cadastrados.
- [ ] Preservar grupos, tags e metadados úteis do Zabbix.
- [ ] Identificar equipamentos sem categoria ou localização.
- [ ] Não inferir atributos sem evidência suficiente.

### 9.3. Interface unificada

- [ ] Permitir abrir um Asset e consultar seus dados de monitoramento.
- [ ] Exibir problemas, métricas e histórico relacionados.
- [ ] Mostrar host Zabbix associado.
- [ ] Exibir última coleta ou consulta válida.
- [ ] Permitir navegar do host ao Asset e vice-versa.
- [ ] Identificar relações desconhecidas ou ambíguas.

---

## FASE 10 — Problemas, alertas, histórico e dashboard geral

### 10.1. Integração de problemas e eventos

- [ ] Consultar problemas ativos.
- [ ] Consultar eventos e triggers conforme necessário.
- [ ] Reutilizar o endpoint existente quando compatível.
- [ ] Implementar processamento idempotente.
- [ ] Tratar recuperação de problemas.
- [ ] Preservar identificadores externos.
- [ ] Definir políticas para criar ou atualizar incidentes.
- [ ] Não transformar automaticamente todos os eventos em chamados.
- [ ] Evitar duplicação de incidentes e alertas.

### 10.2. Histórico

- [ ] Registrar sincronizações e resultados.
- [ ] Registrar mudanças observadas.
- [ ] Consultar histórico de métricas sob demanda quando apropriado.
- [ ] Diferenciar horário de origem e horário da observação.
- [ ] Preservar dados anteriores durante falhas temporárias.
- [ ] Definir retenção e limites de armazenamento.
- [ ] Identificar dados desatualizados.

### 10.3. Dashboard geral de infraestrutura

Criar ou completar `Infraestrutura → Visão geral`.

- [ ] Total de hosts monitorados.
- [ ] Hosts por categoria.
- [ ] Hosts por fabricante.
- [ ] Equipamentos com problemas ativos.
- [ ] Problemas por severidade.
- [ ] Impressoras com alertas de consumíveis quando disponíveis.
- [ ] Câmeras e gravadores com problemas.
- [ ] Switches, APs e interfaces com problemas.
- [ ] Servidores e VMs com problemas.
- [ ] Serviços críticos indisponíveis.
- [ ] Hosts sem Asset associado.
- [ ] Hosts sem coleta adequada.
- [ ] Estado da integração e última sincronização.

### 10.4. Interface por categoria

- [ ] Criar filtros por categoria, fabricante, localização, grupo e estado.
- [ ] Permitir navegação dos indicadores para os equipamentos.
- [ ] Exibir detalhes, métricas e problemas.
- [ ] Exibir origem e atualização dos dados.
- [ ] Diferenciar ausência de problemas de falha na consulta.
- [ ] Garantir paginação e desempenho para inventários grandes.

---

## FASE 11 — Operação, segurança, testes e aceite final

### 11.1. Segurança e resiliência

- [ ] Aplicar autorização aos endpoints e páginas.
- [ ] Proteger tokens e credenciais.
- [ ] Utilizar uma conta Zabbix com privilégios mínimos.
- [ ] Aplicar timeout, cancelamento e retry controlado.
- [ ] Evitar consultas excessivas à API.
- [ ] Limitar intervalos e volumes de consultas históricas.
- [ ] Tratar falhas parciais sem apagar dados válidos.
- [ ] Evitar execuções simultâneas conflitantes.
- [ ] Registrar logs, métricas e traces.
- [ ] Não expor segredos em logs.
- [ ] Manter operações de consulta somente leitura nesta integração inicial.

### 11.2. Testes automatizados

- [ ] Testar autenticação e erros de API.
- [ ] Testar sincronização e idempotência.
- [ ] Testar hosts duplicados.
- [ ] Testar problemas e recuperações.
- [ ] Testar consultas históricas.
- [ ] Testar correlação com Assets.
- [ ] Testar filtros, paginação e permissões.
- [ ] Testar falhas e recuperação da conexão.
- [ ] Testar dados ausentes e desatualizados.
- [ ] Testar políticas de incidentes.
- [ ] Testar dashboards com dados reais ou fixtures identificadas como testes.

### 11.3. Validação real

- [ ] Validar a integração com o Zabbix existente.
- [ ] Selecionar amostras reais de cada categoria de equipamento presente.
- [ ] Validar impressoras e consumíveis suportados.
- [ ] Validar câmeras e gravadores.
- [ ] Validar switches e interfaces.
- [ ] Validar Access Points.
- [ ] Validar telefonia.
- [ ] Validar servidores e VMs.
- [ ] Validar serviços e aplicações críticos.
- [ ] Confirmar que as métricas correspondem à origem.
- [ ] Testar uma falha de consulta e sua recuperação.
- [ ] Confirmar ausência de duplicações.
- [ ] Registrar evidências sem expor segredos.

### 11.4. Documentação

- [ ] Documentar arquitetura da integração.
- [ ] Documentar categorias e métodos de monitoramento.
- [ ] Documentar templates e recursos disponíveis.
- [ ] Documentar regras de correlação.
- [ ] Documentar políticas de alertas e incidentes.
- [ ] Documentar configuração e permissões.
- [ ] Documentar limites e lacunas de monitoramento.
- [ ] Documentar testes e evidências de aceite.

### 11.5. Critérios finais de aceite

- [ ] Build funcional.
- [ ] Suítes relevantes aprovadas.
- [ ] Integração validada com o Zabbix real.
- [ ] Hosts, problemas e métricas consultáveis conforme suportado.
- [ ] Categorias de equipamentos identificadas corretamente.
- [ ] Dados apresentados no InfraControl correspondem à origem.
- [ ] Assets não duplicados.
- [ ] Falhas de consulta não são confundidas com equipamentos offline.
- [ ] Dashboard utiliza dados reais.
- [ ] Segurança, histórico e observabilidade validados.
- [ ] Lacunas de monitoramento documentadas.
- [ ] Evidências de execução registradas.

---

## Regras obrigatórias de execução

1. Auditar antes de implementar.
2. Reutilizar o Zabbix e os templates já existentes.
3. Não duplicar mecanismos de coleta.
4. Não inventar métricas ou capacidades de dispositivos.
5. Não presumir que todos os fabricantes oferecem os mesmos dados.
6. Não criar Assets duplicados.
7. Não confundir falha da integração com falha do equipamento.
8. Não apagar dados válidos por falhas temporárias.
9. Não criar incidentes para todos os eventos sem política definida.
10. Não implementar ações de alteração de equipamentos nesta fase.
11. Não sobrescrever alterações de sessões paralelas.
12. Executar build e testes após incrementos relevantes.
13. Documentar limitações e lacunas encontradas.
14. Marcar cada fase como concluída somente após validação.
15. Considerar o monitoramento completo somente quando a cobertura real estiver comprovada.

## Monitoramento contínuo do desenvolvimento

- [ ] Acompanhar o estado do build.
- [ ] Investigar conflitos de sessões paralelas.
- [ ] Executar suítes de testes quando o build estiver estável.
- [ ] Registrar regressões e correções.
- [ ] Atualizar este checklist conforme a implementação avança.
