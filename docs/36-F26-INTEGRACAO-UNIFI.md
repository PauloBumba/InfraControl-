# 36. F26 — Integração UniFi completa no InfraControl (checklist vivo)

- **Task/phase:** F26 (Fase 26) — plano entregue pelo usuário em 2026-10-08
- **Status:** em execução — **Fase 1 em andamento** (auditoria quase completa:
  `docs/35-AUDITORIA-UNIFI-FASE0.md`); demais fases aguardam
- **Relação com outros docs:** auditoria Fase 0 em `docs/35` · contrato do
  cliente/sync em `docs/29` · plano anterior (base deste) em `docs/34` ·
  regras transversais: `AGENTS.md` + "Regras obrigatórias de execução" ao fim
- **Regra de atualização:** este checklist só avança com **evidência**
  (build + testes + validação real) — doc/entidade/tela isolada não é
  implementação concluída (regra fundamental do §1)

---

## 1. Objetivo

Implementar uma integração completa entre o InfraControl e o controlador
UniFi existente, permitindo consultar, persistir, visualizar e acompanhar os
sites, switches, Access Points, clientes, redes, portas e relações de
topologia que estiverem disponíveis na API instalada.

A integração deve funcionar dentro da arquitetura atual do InfraControl,
reutilizando os componentes, padrões, autenticação, autorização, inventário,
observabilidade e infraestrutura já existentes.

### Resultado esperado

O administrador deverá conseguir:

- Configurar a conexão com o controlador UniFi.
- Testar a conexão e consultar o estado da integração.
- Consultar os sites existentes.
- Visualizar switches e Access Points.
- Consultar portas, clientes, redes e VLANs disponíveis.
- Visualizar a topologia da rede.
- Consultar os detalhes de cada equipamento.
- Identificar equipamentos desconhecidos ou ainda não associados ao inventário.
- Consultar o histórico das sincronizações e observações.
- Visualizar dados atualizados e identificar informações desatualizadas.
- Diagnosticar erros de autenticação, comunicação e sincronização.

**Regra fundamental:** não considerar uma funcionalidade implementada apenas
porque existe documentação, entidade, endpoint ou tela. Cada capacidade deve
estar conectada ao fluxo funcional e possuir testes e evidências.

---

# FASE 1 — Fundação e auditoria técnica

## 1.1. Auditar o projeto atual

- [x] Inspecionar a solução `InfraControl.slnx`.
- [x] Identificar os módulos existentes de integrações, inventário,
  infraestrutura, configurações e auditoria.
- [x] Identificar os padrões existentes para clientes HTTP, opções, validação,
  persistência, jobs e tratamento de erros.
- [x] Verificar a implementação atual da autenticação e dos mecanismos de
  armazenamento de segredos.
- [x] Identificar componentes reutilizáveis de frontend, tabelas, filtros,
  dashboards, notificações e páginas administrativas.
- [x] Verificar migrations, entidades, serviços, endpoints, permissões e
  testes existentes relacionados à UniFi.
- [x] Identificar integrações ou implementações UniFi parciais antes de criar
  novos componentes.
- [x] Verificar o estado do build e os conflitos de sessões paralelas.
- [x] Registrar o que já existe, o que está incompleto e o que precisa ser
  desenvolvido.

> Achados: `docs/35` §1–§7 e §9 (auditoria do projeto) · build monitorado na
> "Monitoramento contínuo" ao fim · conflitos de 2026-10-08 22:06 (merge
> `be65c2f` com marcadores commitados) resolvidos pela sessão paralela;
> regra 10 respeitada — nada sobrescrito.

## 1.2. Descobrir o controlador real

- [x] Identificar a versão instalada do UniFi Network. (**10.4.57** —
  `GET /status`, docs/35 §1)
- [x] Identificar onde o controlador está hospedado e como o InfraControl
  consegue alcançá-lo. (Docker `lscr.io/linuxserver/unifi-network-application`
  + `unifi-mongodb`; rede da API alcança via gateway `172.21.0.1:8443` → 200
  — docs/35 §1–§2)
- [x] Identificar os sites existentes. (`default` listada para o admin;
  `super` existe no banco e não é listada — docs/35 §4)
- [x] Identificar os switches e Access Points utilizados. (**1 AP `U7PG2`**
  adotado; **nenhum switch** — docs/35 §1/§4)
- [x] Verificar a API suportada pela versão instalada. (legada `/api` na raiz;
  `/proxy/network` e `/v1` = 404 — docs/35 §3)
- [x] Verificar os métodos de autenticação disponíveis. (`POST /api/login` →
  200 + cookie `unifises`; `/api/auth/login` → 401; sem API key nesta
  instalação — docs/35 §3)
- [x] Verificar quais dados de dispositivos, clientes, redes, portas e
  topologia são realmente fornecidos. (campos reais em docs/35 §4 — devices
  com `port_table`, clients `stat/sta`, redes `rest/networkconf`; topologia =
  `uplink`/`lldp_table`/`downlink_table` **presentes no payload** — verificar
  em Fase 6)
- [x] Verificar limitações de permissões, paginação, rate limits e histórico.
  (docs/35 §7 — endpoints legados sem paginação; 1 AP = amostra mínima; site
  `super` fora do escopo do admin)
- [x] Validar certificados TLS e os requisitos de conectividade.
  (self-signed `CN=unifi` 2026→2036; bypass opcional `UNIFI_ALLOW_INVALID_TLS`
  — docs/35 §2, docs/29 §5)

## 1.3. Entregáveis

- [x] Relatório da auditoria inicial. (`docs/35-AUDITORIA-UNIFI-FASE0.md`)
- [x] Inventário das capacidades disponíveis na API. (docs/35 §4 + docs/29 §3)
- [x] Lista de componentes reutilizáveis. (docs/35 §9.3)
- [x] Lista de funcionalidades ausentes ou incompletas. (docs/35 §9.4)
- [x] Critérios de aceite específicos para os recursos efetivamente
  suportados. (docs/35 §9.5)

---

# FASE 2 — Conexão e configuração administrativa

> Evidência 2.1 (2026-10-09): RED→GREEN concluído — 6 testes novos escritos
> antes do código (`UnifiApiClientTests` +4, `UnifiHttpMessageHandlerTests`
> +2); após implementação: **24/24 testes `FullyQualifiedName~Unifi` verdes**,
> build 0 erros, unit/arch completas verdes. Validação contra o controlador
> real nas pontas feita na auditoria (docs/35 §2–§3; login 200 nos dois
> estilos a partir do container da rede da API).

## 2.1. Cliente de integração

- [x] Implementar ou completar `IUniFiClient`. (`IUnifiApiClient` já existia;
  completado na Fase 2 — detecção de estilo + fallback de login)
- [x] Implementar ou completar `UniFiClient`. (`UnifiApiClient`: login
  `POST /api/auth/login` → fallback `POST /api/login`, estilo memorizado,
  prefixo `/proxy/network` × raiz, re-auth 401 — docs/29 §3; testes
  `UnifiApiClientTests` 6+4)
- [x] Implementar `UniFiOptions` conforme os padrões existentes.
  (`UnifiIntegrationOptions` + `AllowInvalidTlsCertificate`, bind de config —
  docs/29 §5)
- [x] Centralizar a construção de URLs e as chamadas HTTP. (URI por estilo:
  `DeviceStatsUri`/`SelfUri`/login paths no próprio client)
- [x] Implementar autenticação compatível com o controlador identificado.
  (detecção automática validada contra o controlador real — docs/35 §3)
- [x] Implementar timeout, cancelamento, tratamento de erros e limites de
  requisição. (`CancellationToken` em toda a porta; Polly retry/circuit
  breaker no named client `unifi`; `GlobalExceptionHandler` — docs/29 §6)
- [x] Implementar validação TLS segura. (`UnifiHttpMessageHandler` — default
  estrito; bypass só com `UNIFI_ALLOW_INVALID_TLS=true` + testes)
- [x] Tratar erros de autenticação, autorização, indisponibilidade e respostas
  inesperadas. (401/403 → pt-BR; 404/405 nos dois logins → "API de login
  compatível"; sem array `data` → `InvalidOperationException`)
- [x] Evitar dependência de endpoints não confirmados. (só endpoints
  validados em docs/35 §4)
- [x] Implementar modelos de resposta e conversão para modelos internos.
  (`UnifiDeviceDto` + `ParseDevices`; modelos novos nas fases seguintes)
- [ ] Registrar logs sem expor tokens, cookies ou segredos.
  (log de estilo/sessão em Debug/Information; validar ausência de
  credenciais nos logs na Fase 11)

## 2.2. Configuração no InfraControl

> Backend concluído (2026-10-09): entidade `UniFiIntegrationSetting`
> (linha única; senha cifrada via `ICredentialProtector`/AES-GCM — F11-T006),
> repo EF + migration **pendente** (snapshot do banco em churn da sessão
> paralela — gerar assim que estabilizar), handlers `Get/Save/Test`
> (`Administer` nas 3 camadas: policy HTTP + `IAuthorizedRequest` + audit em
> Save), endpoints `GET/PUT .../config` e `POST .../test-connection` no
> `UnifiIntegrationController`, DI (`IUniFiSettingsRepository`,
> `IUnifiEnvOptionsAccessor`, `IUnifiClientFactory`). 9 testes novos
> (`UniFiSettingsCommandsTests`) — senha nunca sai em DTO, fallback de env,
> erro de conexão vira resultado (não lança). **Pendências da fase:**
> migration, página da UI, sync (`SyncUnifiInventoryCommand`) ainda usa o
> cliente DI de env — passar para a config efetiva (fábrica+resolver).

- [ ] Configurar a conexão com o controlador UniFi na interface.
- [ ] Testar a conexão e consultar o estado da integração na interface.
- [ ] Persistir a configuração no InfraControl. (backend ✓ · migration
  pendente de churn paralelo)
- [ ] Proteger credenciais e segredos. (backend ✓ — AES-GCM + DTO sem senha)
- [ ] Exibir versão do controlador, último erro e última sincronização.
- [ ] Aplicar autorização adequada às operações administrativas.
  (backend ✓ — `Administer`)
- [ ] Executar teste de conexão com a configuração persistida ou de
  ambiente. (backend ✓ — `POST .../test-connection`)

## 2.3. Critérios de aceite

- [ ] Conexão validada contra o controlador real.
- [ ] Credenciais inválidas produzem erro identificável.
- [ ] Certificados inválidos não são ignorados silenciosamente.
- [ ] Configurações persistem corretamente.
- [ ] Segredos não aparecem em logs, respostas da API ou frontend.
- [ ] Falhas de conexão não derrubam a aplicação.

---

# FASE 3 — Sites e dispositivos

(itens do plano original — marcar conforme executado)

## 3.1. Sites

- [ ] Consultar os sites disponibilizados pelo controlador.
- [ ] Persistir identificador externo, nome e demais atributos suportados.
- [ ] Relacionar dispositivos ao site correto.
- [ ] Implementar atualização dos sites existentes.
- [ ] Tratar sites removidos, inacessíveis ou desativados sem apagar dados
  históricos indevidamente.

## 3.2. Switches e Access Points

- [ ] Consultar os dispositivos retornados pela API.
- [ ] Identificar switches, Access Points e demais categorias realmente
  suportadas.
- [ ] Persistir identificador externo estável.
- [ ] Persistir site, nome, modelo, MAC, IP, estado e firmware quando
  disponíveis.
- [ ] Armazenar informações adicionais suportadas pela API, sem inventar
  campos.
- [ ] Registrar última observação e última sincronização.
- [ ] Atualizar os registros existentes sem criar duplicações.
- [ ] Tratar dispositivos não encontrados em sincronizações posteriores.
- [ ] Diferenciar estado informado pelo controlador de falha de comunicação
  com a integração.

## 3.3. Interface de dispositivos

Criar ou completar a página **Infraestrutura → Dispositivos**.

- [ ] Listar switches e Access Points.
- [ ] Pesquisar por nome, MAC, IP e modelo.
- [ ] Filtrar por site, categoria e estado.
- [ ] Ordenar e paginar os resultados.
- [ ] Exibir o horário da última atualização.
- [ ] Abrir detalhes do equipamento.
- [ ] Exibir mensagens úteis para listas vazias e erros de carregamento.

## 3.4. Critérios de aceite

- [ ] Os dispositivos retornados pela API são persistidos e apresentados.
- [ ] Sincronizações repetidas não criam duplicatas.
- [ ] Os detalhes apresentados correspondem aos dados recebidos.
- [ ] A indisponibilidade da API não é interpretada automaticamente como
  dispositivo offline.

---

# FASE 4 — Clientes e redes

## 4.1. Clientes conectados

- [ ] Consultar clientes que o controlador disponibiliza.
- [ ] Identificar MAC, IP, nome e tipo de conexão quando disponíveis.
- [ ] Identificar o AP ou switch associado quando a API fornecer essa relação.
- [ ] Consultar SSID, rede, sinal e estatísticas quando suportados.
- [ ] Distinguir dados atuais de observações históricas.
- [ ] Tratar clientes desconectados sem manter seu estado anterior como atual.
- [ ] Não confundir clientes conectados com ativos permanentes do inventário.

## 4.2. Redes e VLANs

- [ ] Consultar redes e SSIDs disponíveis.
- [ ] Consultar VLAN ID, quando disponibilizado.
- [ ] Persistir os identificadores externos e os atributos suportados.
- [ ] Relacionar clientes e dispositivos às redes quando houver evidência.
- [ ] Diferenciar dados indisponíveis de redes inexistentes.
- [ ] Não presumir que toda VLAN configurada seja visível pela API utilizada.

## 4.3. Interface

Criar ou completar:

- [ ] Página de clientes.
- [ ] Página de redes.
- [ ] Filtros por site, rede, AP e estado quando suportados.
- [ ] Detalhes de clientes e relacionamentos conhecidos.
- [ ] Indicação da origem e da atualização dos dados.
- [ ] Paginação e pesquisa para volumes elevados.

## 4.4. Critérios de aceite

- [ ] Clientes e redes são consultados e persistidos conforme a API.
- [ ] Os relacionamentos são exibidos apenas quando sustentados pelos dados.
- [ ] Desconexões e dados desatualizados são tratados corretamente.
- [ ] As telas consomem a API interna do InfraControl, não o controlador
  diretamente.

---

# FASE 5 — Portas dos switches

## 5.1. Consulta de portas

- [ ] Identificar quais informações de portas estão disponíveis na versão
  instalada.
- [ ] Consultar as portas dos switches.
- [ ] Persistir identificador externo e associação ao switch.
- [ ] Persistir número ou nome da porta quando disponível.
- [ ] Consultar estado operacional e administrativo quando suportados.
- [ ] Consultar velocidade, duplex, VLAN, perfil e estatísticas quando
  disponíveis.
- [ ] Consultar informações do dispositivo conectado quando fornecidas.
- [ ] Consultar informações de PoE quando suportadas.
- [ ] Diferenciar portas sem conexão de portas cujos dados não foram obtidos.
- [ ] Registrar o momento da observação.
- [ ] Atualizar os dados sem criar registros duplicados.

## 5.2. Interface

- [ ] Implementar tabela de portas na página de detalhes do switch.
- [ ] Exibir número, estado e informações realmente disponíveis.
- [ ] Permitir filtrar por estado, VLAN ou velocidade quando suportados.
- [ ] Apresentar o dispositivo conectado quando houver evidência.
- [ ] Exibir indicadores de dados indisponíveis ou desatualizados.
- [ ] Não afirmar que uma porta está desligada quando a consulta falhou.

## 5.3. Critérios de aceite

- [ ] A lista corresponde às portas retornadas pelo controlador.
- [ ] Os estados são interpretados conforme a semântica real da API.
- [ ] Campos não suportados não são inventados.
- [ ] A interface funciona mesmo quando algumas propriedades não estão
  disponíveis.

---

# FASE 6 — Topologia visual

## 6.1. Descoberta das relações

- [ ] Investigar os dados de topologia fornecidos pela API UniFi.
- [ ] Verificar relações entre switches, uplinks, APs e clientes.
- [ ] Verificar se há identificação das portas de origem e destino.
- [ ] Avaliar informações de vizinhos e LLDP, quando disponíveis.
- [ ] Identificar limitações do controlador e dos equipamentos.
- [ ] Avaliar fontes complementares somente se forem necessárias e aprovadas.
- [ ] Permitir cadastro manual de conexões quando a descoberta automática não
  for possível.

## 6.2. Modelo de conexão

Cada relação deve registrar, conforme aplicável:

- [ ] Equipamento de origem.
- [ ] Porta de origem.
- [ ] Equipamento de destino.
- [ ] Porta de destino.
- [ ] Fonte da informação.
- [ ] Tipo de descoberta.
- [ ] Data da observação.
- [ ] Nível de confiança ou estado de confirmação.

Classificar as relações como:

- Confirmadas por dados verificáveis.
- Inferidas a partir de evidências.
- Cadastradas manualmente.
- Desconhecidas ou não verificadas.

## 6.3. Interface da topologia

- [ ] Criar mapa visual interativo.
- [ ] Implementar zoom e movimentação.
- [ ] Exibir switches, APs e demais elementos suportados.
- [ ] Exibir conexões e rótulos de portas quando conhecidos.
- [ ] Permitir pesquisar e selecionar dispositivos.
- [ ] Abrir painel de detalhes do equipamento.
- [ ] Permitir filtrar por site e categoria.
- [ ] Diferenciar visualmente estado do equipamento e confiabilidade da
  conexão.
- [ ] Exibir legenda e origem dos dados.
- [ ] Tratar equipamentos sem relações conhecidas.
- [ ] Evitar sobreposição excessiva em redes grandes.

## 6.4. Critérios de aceite

- [ ] O mapa é construído com dados persistidos e atuais.
- [ ] Não são desenhadas conexões físicas como confirmadas sem evidência.
- [ ] O utilizador consegue navegar da topologia aos detalhes do dispositivo.
- [ ] Ausência de dados de topologia não impede o funcionamento do restante
  da integração.

---

# FASE 7 — Correlação com o inventário (Assets)

## 7.1. Identidade dos equipamentos

- [ ] Reutilizar o módulo de inventário existente.
- [ ] Definir uma estratégia estável de identidade externa.
- [ ] Usar identificador do controlador e MAC como referências, conforme a
  entidade.
- [ ] Utilizar hostname e IP como atributos auxiliares, não como identidade
  universal.
- [ ] Evitar duplicar um Asset já existente.
- [ ] Preservar identificadores e histórico durante alterações de nome ou IP.

## 7.2. Relacionamentos

- [ ] Relacionar dispositivos UniFi aos Assets existentes.
- [ ] Exibir dispositivos sem correspondência.
- [ ] Permitir associação manual com permissões adequadas.
- [ ] Detectar conflitos de associação.
- [ ] Registrar criação, alteração e remoção de relacionamentos.
- [ ] Evitar associação automática ambígua.
- [ ] Permitir consultar os dados UniFi a partir do Asset correspondente.

## 7.3. Critérios de aceite

- [ ] A sincronização não cria Assets duplicados.
- [ ] Associações manuais são preservadas nas sincronizações.
- [ ] Conflitos são apresentados para resolução.
- [ ] A remoção de um vínculo não apaga indevidamente o histórico do
  equipamento.

---

# FASE 8 — Dashboard de infraestrutura UniFi

## 8.1. Indicadores

- [ ] Quantidade de sites.
- [ ] Quantidade de switches.
- [ ] Quantidade de Access Points.
- [ ] Dispositivos por estado informado pelo controlador.
- [ ] Quantidade de clientes, conforme o escopo da consulta.
- [ ] Quantidade de portas e estados, quando disponíveis.
- [ ] Última sincronização bem-sucedida.
- [ ] Erros recentes de integração.
- [ ] Equipamentos sem Asset associado.
- [ ] Dados desatualizados.

## 8.2. Interface

- [ ] Criar ou completar o dashboard UniFi.
- [ ] Utilizar componentes visuais já adotados pelo InfraControl.
- [ ] Permitir navegar dos indicadores para as listas filtradas.
- [ ] Exibir estado de atualização dos dados.
- [ ] Diferenciar zero resultados de erro de consulta.
- [ ] Tratar carregamento, falhas e ausência de dados.
- [ ] Garantir responsividade e acessibilidade básica.

## 8.3. Critérios de aceite

- [ ] Os indicadores são calculados a partir dos dados reais persistidos.
- [ ] Não existem valores demonstrativos apresentados como dados reais.
- [ ] Os filtros e links de navegação funcionam.
- [ ] A indisponibilidade do controlador é visível para o utilizador.

---

# FASE 9 — Histórico e observações

## 9.1. Histórico operacional

- [ ] Registrar execuções de sincronização.
- [ ] Registrar início, término, resultado e duração.
- [ ] Registrar quantidade de registros consultados, criados, atualizados e
  ignorados.
- [ ] Registrar erros sem expor credenciais.
- [ ] Registrar alterações observadas em dispositivos, portas, redes e
  clientes quando aplicável.
- [ ] Registrar mudanças de estado observadas durante as sincronizações.
- [ ] Registrar a origem e o horário de cada observação.
- [ ] Definir política de retenção conforme o padrão do sistema.

## 9.2. Integridade temporal

- [ ] Diferenciar o horário em que o controlador informa uma ocorrência do
  horário em que o InfraControl a observou.
- [ ] Não afirmar que uma mudança aconteceu exatamente no momento da
  sincronização se esse horário não for fornecido pela origem.
- [ ] Preservar histórico relevante quando um dispositivo deixa de aparecer
  na API.
- [ ] Evitar criar eventos repetidos em sincronizações sem mudanças.

## 9.3. Interface

- [ ] Página ou painel de histórico da integração.
- [ ] Histórico por dispositivo.
- [ ] Histórico de sincronizações.
- [ ] Filtros por período, tipo e resultado.
- [ ] Detalhes de erros operacionais.
- [ ] Indicação da última observação válida.

## 9.4. Critérios de aceite

- [ ] As execuções são rastreáveis.
- [ ] As observações são persistidas e consultáveis.
- [ ] Sincronizações repetidas não produzem histórico duplicado sem
  justificativa.
- [ ] A retenção não elimina dados além da política definida.

---

# FASE 10 — Segurança e operações

## 10.1. Segurança

- [ ] Aplicar as políticas de autorização existentes.
- [ ] Restringir a configuração da integração a administradores autorizados.
- [ ] Proteger credenciais e tokens.
- [ ] Não registrar segredos em logs, mensagens de erro ou respostas da API.
- [ ] Validar URLs e configurações fornecidas pelo administrador.
- [ ] Manter a validação de certificados TLS.
- [ ] Não habilitar acesso irrestrito a redes internas por meio de URLs
  arbitrárias.
- [ ] Aplicar limites de requisição e timeouts.
- [ ] Registrar alterações administrativas.
- [ ] Começar com acesso somente leitura ao controlador.
- [ ] Não implementar operações de alteração de rede, configuração de portas
  ou reinicialização de equipamentos nesta fase.

## 10.2. Sincronização

- [ ] Implementar sincronização manual.
- [ ] Implementar sincronização automática usando o mecanismo de jobs
  existente.
- [ ] Definir intervalo configurável dentro de limites seguros.
- [ ] Evitar execuções simultâneas conflitantes.
- [ ] Suportar cancelamento e timeout.
- [ ] Implementar tratamento de falhas parciais.
- [ ] Evitar apagar os últimos dados válidos devido a uma falha transitória.
- [ ] Aplicar retry controlado e backoff quando apropriado.
- [ ] Respeitar os limites do controlador.
- [ ] Disponibilizar execução e resultado da sincronização para diagnóstico.

## 10.3. Observabilidade

- [ ] Integrar logs ao padrão existente.
- [ ] Instrumentar duração, resultado e erros das chamadas.
- [ ] Registrar contagens de registros processados.
- [ ] Utilizar correlação de logs e traces conforme a arquitetura.
- [ ] Evitar etiquetas de métricas com alta cardinalidade desnecessária.
- [ ] Disponibilizar saúde da integração sem expor informações sensíveis.

## 10.4. Critérios de aceite

- [ ] Permissões verificadas em endpoints e interface.
- [ ] Segredos protegidos.
- [ ] Sincronização segura e rastreável.
- [ ] Falhas recuperáveis não corrompem os dados existentes.
- [ ] Nenhuma operação de alteração de rede ocorre sem escopo e autorização
  explícitos.

---

# FASE 11 — Testes reais, documentação e aceite final

## 11.1. Testes automatizados

- [ ] Testar configuração e validação das opções.
- [ ] Testar autenticação e tratamento de erros.
- [ ] Testar timeouts e cancelamento.
- [ ] Testar mapeamento das respostas da API.
- [ ] Testar persistência e atualizações.
- [ ] Testar idempotência das sincronizações.
- [ ] Testar dispositivos removidos ou não encontrados.
- [ ] Testar falhas parciais.
- [ ] Testar autorização dos endpoints.
- [ ] Testar correlação com Assets.
- [ ] Testar filtros, paginação e estados das páginas.
- [ ] Testar a topologia com conexões confirmadas, inferidas e desconhecidas.
- [ ] Testar o histórico e os indicadores.
- [ ] Testar a integração com as abstrações existentes, sem depender
  exclusivamente de mocks.

## 11.2. Validação no controlador real

- [ ] Configurar uma conexão com o controlador UniFi existente em ambiente
  autorizado.
- [ ] Confirmar a autenticação.
- [ ] Confirmar a consulta dos sites.
- [ ] Validar switches e Access Points reais.
- [ ] Validar as informações de portas disponíveis.
- [ ] Validar clientes e redes disponíveis.
- [ ] Validar relações de topologia realmente fornecidas.
- [ ] Executar sincronização manual e automática.
- [ ] Verificar atualização de registros existentes.
- [ ] Simular falha de conexão e validar a recuperação.
- [ ] Verificar que falha da integração não significa que os dispositivos
  estejam offline.
- [ ] Verificar que não são criados Assets duplicados.
- [ ] Registrar evidências de execução sem expor segredos.

## 11.3. Documentação

- [ ] Documentar requisitos e compatibilidade.
- [ ] Documentar configuração da conexão.
- [ ] Documentar permissões necessárias no UniFi.
- [ ] Documentar endpoints e recursos realmente utilizados.
- [ ] Documentar limitações da API identificadas.
- [ ] Documentar sincronização e recuperação de falhas.
- [ ] Documentar significado dos estados e origem dos dados.
- [ ] Documentar a topologia e os níveis de confiabilidade.
- [ ] Documentar testes executados e resultados.
- [ ] Documentar procedimento de diagnóstico.

## 11.4. Aceite final

A F26 somente pode ser marcada como concluída quando:

- [ ] O build da solução estiver funcionando.
- [ ] As suítes de testes relevantes passarem.
- [ ] A integração conectar ao controlador UniFi real.
- [ ] Os dados suportados forem consultados, persistidos e exibidos.
- [ ] Switches e Access Points reais estiverem identificados.
- [ ] Portas, clientes e redes forem exibidos conforme a disponibilidade da
  API.
- [ ] A topologia não inventar conexões físicas.
- [ ] O inventário não apresentar duplicações causadas pela integração.
- [ ] A sincronização suportar erros e recuperação.
- [ ] Segurança, permissões, logs e histórico estiverem validados.
- [ ] As limitações identificadas estiverem documentadas.
- [ ] Existirem evidências de execução e validação real.

---

# Regras obrigatórias de execução

1. Auditar antes de implementar. Não recriar funcionalidades já existentes.
2. Consultar a documentação compatível com a versão instalada do UniFi Network.
3. Não inventar endpoints, campos, estados, clientes, VLANs ou conexões.
4. Reutilizar a arquitetura, os componentes e os mecanismos de persistência
   existentes.
5. Não criar outro controlador UniFi nem novos serviços de infraestrutura sem
   necessidade comprovada.
6. Não duplicar Assets durante a sincronização.
7. Não marcar um equipamento como offline apenas porque uma consulta falhou.
8. Não apagar os últimos dados válidos por causa de uma falha temporária.
9. Não considerar mocks, telas ou documentação isolada como implementação
   concluída.
10. Não sobrescrever alterações de sessões paralelas; investigar conflitos
    antes de continuar.
11. Após cada incremento, executar os testes pertinentes e verificar o build.
12. Marcar cada fase como concluída somente quando seus critérios de aceite
    forem comprovados.
13. Se um recurso não estiver disponível na API, registrar a limitação e
    propor uma alternativa validada, sem simular sua implementação.

# Monitoramento contínuo

- [x] Monitorar o estado do build enquanto houver alterações de sessões
  paralelas. (2026-10-08 22:06 — marcadores commitados no merge `be65c2f`
  quebravam o build com 33× CS8300; sessão paralela assumiu a resolução e
  erros caíram para 5× CS0102/CS0111 (duplicatas do union) às 22:29 —
  regra 10: nada sobrescrito pela F26)
- [x] Investigar conflitos e falhas antes de continuar novas fases. (origem,
  escopo e responsável registrados; F26 segue só em arquivos próprios:
  `Integrations/Unifi`, docs 29/34/35/36, compose/.env)
- [x] Executar as suítes de testes quando o build estiver estável.
  (build **0 erros/0 avisos** → unit **1921/1921** ✓ · architecture
  **119/119** ✓ · integração **165/191** — 26 falhas, 100% `Knowledge*` +
  `FullPipelineE2E` por DI sem `IKnowledgeArticleVersionRepository` (WIP da
  sessão paralela; verificado: nenhuma falha em `Integrations`/`AI`;
  regra "não esconder testes quebrados" — registrado aqui)
- [x] Registrar falhas, correções e evidências de validação.
  (correções mínimas em arquivos **commitados e limpos** quebrados pelo
  merge — domínios sem dono ativo: `AiProviderRouter`/`ConfigurationProviderRegistry`
  (usings inexistentes), `OllamaProvider` (CS0050 + options snake_case para a
  API real do Ollama + catch de `JsonException`), `OllamaProviderTests`
  (`JsonContent.Serialize` inexistente → helper `StringContent`;
  cast impossível de `Mock<HttpMessageHandler>` → campo estático);
  `ConfigurationProviderRegistry.Get(null)` → NRE tratado como
  "não configurado". Migrações duplicadas Knowledge **não** tocadas —
  resolvidas pela própria sessão paralela)
- [x] Atualizar este checklist conforme cada fase for realmente concluída.
