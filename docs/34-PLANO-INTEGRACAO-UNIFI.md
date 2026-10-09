# 34. Plano de execução — Integração UniFi no InfraControl (spec)

- **Tipo:** spec de execução (não é ADR — decisões arquiteturais seguem ADR-028)
- **Status:** em execução — **Fase 0 concluída (2026-10-08)**, evidências em
  `docs/35-AUDITORIA-UNIFI-FASE0.md`; Fase 1+ aguardam autorização do usuário
  (ver §7)
- **Pedido:** "documenta isso antes de executar"
- **Estado atual da integração:** `docs/29-INTEGRACAO-UNIFI.md` (F20-T001
  concluída) · `TASKS.md` §Fase 20 (F20-T002/T003 pendentes) ·
  `docs/adr/ADR-028-INTEGRATION-LAYER-ARCHITECTURE.md`

Este plano trata exclusivamente da integração UniFi: implementação, fases,
arquitetura, interface, sincronização, portas, clientes, topologia, segurança,
testes e critérios de conclusão.

A documentação oficial da Ubiquiti confirma APIs para consultar sites,
dispositivos adotados, detalhes e estatísticas de dispositivos, clientes
conectados e redes. Os endpoints disponíveis dependem da versão do UniFi
Network instalada.

## 0. Relação com o estado atual (auditoria obrigatória antes de executar)

O plano abaixo **não parte do zero** — a Fase 0 começa auditando o que já
existe, para não duplicar módulos, tabelas nem violar decisões já tomadas:

| Já existe | Onde |
| --- | --- |
| Cliente HTTP UniFi (`UnifiApiClient`: login cookie `unifises`, re-auth 1× em 401, listagem de devices, health `GET /api/self`) + porta `IUnifiApiClient`/DTO/options (`Integrations:Unifi`, envs `UNIFI_*`) | `src/InfraControl.Infrastructure/Integrations/Unifi/` · `src/InfraControl.Application/Integrations/Unifi/` |
| Sync idempotente de inventário (`SyncUnifiInventoryCommand`, upsert por MAC) + endpoints `POST /integrations/unifi/sync` e `GET /integrations/unifi/health` | `SyncUnifiInventoryCommand.cs` · `UnifiIntegrationController.cs` |
| Persistência mínima: **apenas** `Device` + `ExternalDeviceReference` (ExternalId = MAC lowercase sem separadores) | REGRA 8 (`TASKS.md` §regra 8, ADR-028 §7) |
| Testes com `HttpMessageHandler` fake (17 unit) e doc do contrato | `tests/.../Integrations/` · `docs/29-INTEGRACAO-UNIFI.md` |

**Decisões existentes que o executor precisa respeitar** (não podem ser
revertidas sem ADR novo):

- **REGRA 8** — telemetria de massa (portas PoE, tráfego, clientes Wi-Fi)
  **nunca** entra no PostgreSQL; consulta sob demanda com cache curto é o
  escopo de F20-T003 (`UniFi API → Redis → Frontend`).
- **REGRA 9** — cache Redis ≠ fonte de verdade.
- **ADR-028 §7** — identidade de dispositivo externo = MAC lowercase sem
  separadores; sem realocação automática de dono de referência.
- **Primeira versão somente leitura** (Fase 10 deste plano) — alinhado ao que
  já existe (`sync` é a única escrita, e é escrita no *nosso* inventário, não
  no equipamento).
- F20-T002 (webhooks/eventos UniFi) e F20-T003 (consultas sob demanda/Redis)
  já estão na fila do `TASKS.md` — este plano deve **encaixar-se** nelas em
  vez de criar rotas paralelas.

## 1. Objetivo final

Construir no InfraControl um módulo chamado **Infrastructure & UniFi**,
permitindo visualizar e consultar a infraestrutura de rede por meio de uma
interface unificada.

- **Dashboard de infraestrutura** — resumo de sites, switches, access points,
  dispositivos online/offline e última sincronização.
- **Gestão de portas** — portas, velocidade, estado, informações disponíveis de
  conexão e detalhes do switch.
- **Topologia interativa** — mapa visual de dispositivos e conexões
  confirmadas, com pesquisa, filtros e navegação por equipamento.

As imagens de referência do pedido são **estilo** para o desenvolvimento, não
funcionalidades já implementadas.

## 2. Arquitetura prevista

```text
InfraControl
│
├── Infrastructure Dashboard
├── UniFi Sites
├── Network Devices
├── Switch Ports
├── Connected Clients
├── Networks
├── Network Topology
├── Device History
│
└── UniFi Integration
    ├── API Client
    ├── Authentication
    ├── Synchronization
    ├── Data Normalization
    ├── Correlation
    ├── Audit
    └── Health Check
```

A integração deve respeitar a arquitetura atual do InfraControl, reutilizando
seus módulos, autenticação, permissões, persistência e componentes de
interface. **Não criar uma aplicação paralela.**

## 3. Fases de execução

### Fase 0 — Auditoria e descoberta técnica

**Objetivo:** entender a instalação existente antes de escrever código.

> **✅ Concluída (2026-10-08)** — evidências e resultado em
> [`docs/35-AUDITORIA-UNIFI-FASE0.md`](35-AUDITORIA-UNIFI-FASE0.md).
> Resumo: UniFi Network **10.4.57** standalone (Docker local; `/api` na raiz,
> `/proxy/network` e `/v1` = 404), login `POST /api/login` + cookie
> `unifises`, 6 endpoints `GET` autenticados validados (sites/devices/clients/
> networks/sysinfo/health), rede alcançável da rede da API, TLS self-signed —
> 3 gaps no `UnifiApiClient` atual documentados para a Fase 1 (§6 do
> documento).

Tarefas:

- [x] Identificar a versão do UniFi Network. (10.4.57 — `GET /status`)
- [x] Identificar onde o controlador está hospedado. (Docker
  `lscr.io/linuxserver/unifi-network-application` + `unifi-mongodb`)
- [x] Verificar se a API oficial está disponível. (legada `/api` sim;
  oficial `/v1` e `/proxy/network` não, nesta instalação standalone)
- [x] Confirmar acesso de rede entre o InfraControl e o controlador.
  (200 via gateway `172.21.0.1:8443` de container na rede da API)
- [x] Verificar TLS, DNS, portas e certificados. (self-signed `CN=unifi`
  2026→2036; portas 8443/8080/8843/8880/3478/10001)
- [x] Consultar a documentação da API correspondente à versão.
  (`developer.ui.com/network/v10.4.57`)
- [x] Identificar os endpoints disponíveis para sites, dispositivos,
  clientes, redes, estatísticas e portas. (todos validados com `GET`
  real — docs/35 §4)
- [x] Investigar o módulo de inventário existente no InfraControl.
  (docs/29 — `Device` + `ExternalDeviceReference`, REGRA 8)
- [x] Definir quais dados serão importados e quais serão apenas
  consultados. (docs/35 §5 — mantida REGRA 8)

**Entrega:** documento técnico com versão, endpoints validados, permissões
necessárias e limitações conhecidas → **`docs/35-AUDITORIA-UNIFI-FASE0.md`**.

**Critério de saída:** a equipe consegue realizar pelo menos uma consulta real à
API sem modificar a rede → **cumprido** (6 consultas `GET` autenticadas, zero
escritas).

### Fase 1 — Fundação da integração

**Objetivo:** estabelecer uma conexão segura e reutilizável.

Estrutura sugerida:

```text
Infrastructure/
└── Integrations/
    └── UniFi/
        ├── IUniFiClient.cs
        ├── UniFiClient.cs
        ├── UniFiOptions.cs
        ├── UniFiAuthentication.cs
        ├── UniFiMapper.cs
        ├── UniFiErrorHandler.cs
        └── UniFiHealthCheck.cs
```

Implementar:

- Cliente HTTP tipado.
- Autenticação por API Key, quando suportada.
- Timeout configurável.
- Tratamento de erros HTTP e respostas inválidas.
- Paginação quando necessária.
- Retry controlado para falhas transitórias.
- Logs estruturados e métricas.
- Armazenamento seguro dos segredos.
- Cancelamento das operações.
- Testes com respostas simuladas e com a API real.

**Critério de saída:** conexão e autenticação verificadas, erros tratados e
health check operacional.

> Nota de auditoria: `UnifiApiClient`, options, Polly (named client `unifi`),
> health check e testes de resposta simulada **já existem** (F20-T001,
> docs/29). A Fase 1 deve partir do delta (ex.: API Key se a versão instalada
> suportar, paginação, métricas) em vez de reescrever o cliente.

### Fase 2 — Configuração administrativa

Criar a tela: **Administração → Integrações → UniFi**

Campos:

- Nome da integração.
- Modo de conexão.
- URL/base da API.
- API Key.
- Timeout.
- Validação TLS.
- Site padrão.
- Status da integração.

Ações:

- Testar conexão.
- Validar autenticação.
- Consultar versão.
- Listar sites disponíveis.
- Ativar/desativar integração.
- Executar sincronização manual.

Exibir também:

- Última tentativa.
- Última sincronização bem-sucedida.
- Tempo de resposta.
- Último erro.
- Quantidade de dispositivos encontrados.

A API Key deve ser **mascarada** na interface e **nunca devolvida** ao
frontend depois de salva.

**Critério de saída:** um administrador consegue configurar, testar e
diagnosticar a integração sem alterar arquivos de configuração manualmente.

### Fase 3 — Sites e dispositivos

**Objetivo:** trazer a estrutura real gerenciada pelo UniFi.

Implementar consultas e persistência normalizada para:

- Sites.
- Switches.
- Access points.
- Gateways e outros dispositivos suportados.
- Identificadores externos.
- Nome, modelo, MAC e IP, quando disponíveis.
- Estado e versão de firmware, quando disponíveis.
- Estatísticas recentes suportadas pela API.

Criar uma página: **Infraestrutura → Dispositivos**

Recursos:

- Tabela com pesquisa e filtros.
- Filtro por site, tipo e estado.
- Ordenação por nome e última atualização.
- Página de detalhes por dispositivo.
- Link para abrir o dispositivo no controlador UniFi.
- Indicação de dados desatualizados.

**Critério de saída:** os dispositivos retornados pela API aparecem
corretamente, sem duplicação a cada sincronização.

> Nota de auditoria: o sync idempotente por MAC (REGRA 8 — só `Device` +
> `ExternalDeviceReference`) já existe; a página de dispositivos do
> inventário também. O delta é sites, detalhe de firmware/estado vindo do
> UniFi e a indicação de staleness.

### Fase 4 — Clientes conectados e redes

**Objetivo:** visualizar os dispositivos conectados e as redes conhecidas pelo
UniFi.

Implementar, conforme disponibilidade da API:

- Lista de clientes conectados.
- MAC e IP.
- Nome do cliente.
- Tipo de conexão.
- Access point ou dispositivo associado, quando fornecido.
- SSID ou rede relacionada, quando fornecidos.
- Sinal e estatísticas de tráfego, quando disponíveis.
- Lista de redes configuradas.

Criar páginas:

- **Infraestrutura → Clientes**
- **Infraestrutura → Redes**

O sistema deve diferenciar *cliente conectado atualmente*, *cliente visto
anteriormente* e *cliente sem informação atualizada*. **Não tratar a ausência
em uma consulta como prova imediata de que o dispositivo está desligado.**

**Critério de saída:** clientes e redes podem ser pesquisados e relacionados ao
site e aos dispositivos correspondentes.

> Nota de auditoria: encaixe com F20-T003 (consulta sob demanda + cache Redis
> curto — REGRA 9). Persistência de observações (Fase 9) respeita a REGRA 8.

### Fase 5 — Portas dos switches

**Objetivo:** mostrar cada switch e suas interfaces físicas.

A API oficial possui recursos de detalhes de dispositivos e ações de portas; os
campos exatos de estado, velocidade, contadores e conexão **devem ser
confirmados na versão instalada**.

Implementar:

- Inventário de portas por switch.
- Número ou índice da porta.
- Estado operacional, quando disponível.
- Velocidade e duplex, quando disponíveis.
- Contadores e estatísticas, quando disponíveis.
- Informações de VLAN/rede, quando disponíveis.
- Dispositivo conectado, somente quando houver uma associação confiável.
- Histórico das mudanças que o InfraControl efetivamente observou.

Tela de exemplo (ilustrativa — valores não são dados reais):

```text
SW-SETOR-02
Modelo: obtido da API
IP: obtido da API
Estado: atualizado pela integração

PORTAS
┌──────┬─────────┬───────────┬────────────────┐
│ Porta│ Estado  │ Velocidade│ Conexão        │
├──────┼─────────┼───────────┼────────────────┤
│ 01   │ UP      │ 1 Gbps    │ PC-023         │
│ 02   │ UP      │ 1 Gbps    │ AP-SETOR-02    │
│ 03   │ DOWN    │ —         │ Desconhecida   │
└──────┴─────────┴───────────┴────────────────┘
```

**Critério de saída:** a tela mostra os dados efetivamente obtidos da API e
identifica claramente campos desconhecidos ou indisponíveis.

### Fase 6 — Topologia visual

**Objetivo:** transformar os dados coletados em um mapa navegável.

Criar: **Infraestrutura → Topologia**

A interface deve permitir:

- Zoom e movimentação do mapa.
- Pesquisa de dispositivos.
- Filtro por site e tipo.
- Seleção de um dispositivo.
- Exibição dos detalhes em painel lateral.
- Navegação entre equipamentos relacionados.
- Indicação de links ativos, inativos ou desconhecidos, quando houver
  evidência.
- Diferenciação entre conexão descoberta e ligação cadastrada manualmente.
- Atualização da visualização depois das sincronizações.

Arquitetura da visualização:

```text
UniFi API
    ↓
Dados normalizados
    ↓
Network Relationship Service
    ↓
Topology Model
    ↓
Frontend Graph
    ↓
Mapa interativo
```

**Ponto crítico:** não presumir que a API oficial oferece descoberta completa de
vizinhos, LLDP ou a identificação de todos os cabos. Se os endpoints
documentados não fornecerem a relação entre uma porta e o equipamento
conectado, será necessário avaliar outra fonte compatível, como SNMP/LLDP, ou
permitir cadastro manual.

**Critério de saída:** todas as ligações exibidas têm origem identificável e um
nível de confiabilidade explícito.

> Nota de auditoria: o módulo de topologia do InfraControl (agregado Read do
> ADR-030) e a regra de conexões manuais já existem; o delta é alimentar o
> modelo com a origem/confiabilidade por evidência.

### Fase 7 — Correlação com o inventário

**Objetivo:** conectar os dados do UniFi ao inventário do InfraControl.

Relacionar os registros por:

1. Identificador externo do dispositivo.
2. MAC.
3. Outros identificadores estáveis disponíveis.
4. IP e nome como critérios auxiliares.
5. Associação manual quando necessário.

Implementar:

- Relação entre dispositivo UniFi e Asset.
- Localização e setor.
- Responsável.
- Histórico de associação.
- Detecção de possíveis duplicidades.
- Processo de correção manual.

Não criar automaticamente dois Assets para o mesmo equipamento apenas porque
seu IP ou nome mudou.

**Critério de saída:** cada associação é rastreável e pode ser corrigida por
usuário autorizado.

> Nota de auditoria: `ExternalDeviceReference` (Source=UniFi, ExternalId=MAC)
> já é o identificador externo; divergência de dono conta como erro e **não**
> realoca (ADR-028 §7). O plano soma histórico de associação, duplicidades e
> correção manual.

### Fase 8 — Dashboard e interface final

Criar a página inicial: **Infraestrutura → Visão geral**

Indicadores:

- Total de dispositivos conhecidos.
- Distribuição por tipo.
- Estado informado pela integração.
- Sites disponíveis.
- Clientes conectados, conforme consulta.
- Portas com informações disponíveis.
- Última sincronização.
- Erros de integração.
- Ativos sem correspondência no inventário.

Adicionar acesso rápido para:

- Dispositivos.
- Switches.
- Portas.
- Access points.
- Clientes.
- Redes.
- Topologia.
- Configuração da integração.

**Critério de saída:** a interface funciona em diferentes tamanhos de tela, tem
estados de carregamento e erro e **não apresenta dados antigos como se fossem
atuais**.

### Fase 9 — Histórico e eventos

Implementar um histórico de observações feitas pelo InfraControl:

- Mudança de estado detectada.
- Alteração de informações de dispositivo.
- Cliente observado ou não observado em consultas sucessivas.
- Mudanças de portas, quando detectáveis.
- Falha de sincronização.
- Alterações manuais no inventário e na topologia.

Registrar horário da observação, origem e resultado. **Não alegar que a
aplicação conhece uma mudança que ocorreu entre duas consultas se não recebeu
evento que comprove o momento exato.**

**Critério de saída:** mudanças registradas podem ser consultadas e auditadas.

### Fase 10 — Segurança e operações

A primeira versão será **somente leitura**.

Implementar:

- Autorização por função e permissão.
- Proteção dos segredos.
- Auditoria das consultas administrativas.
- Limites de requisição.
- Timeout e cancelamento.
- Tratamento de indisponibilidade.
- Monitoramento da própria integração.
- Retenção configurável do histórico.
- Separação entre consulta e operações que modificam dispositivos.

Ações futuras, como reiniciar dispositivos ou alterar portas, devem ser
desenvolvidas separadamente, com permissões específicas, confirmação explícita
e auditoria.

**Critério de saída:** nenhum usuário ou componente de IA consegue executar uma
ação de alteração sem passar pelas políticas de autorização definidas.

> Nota de auditoria: seguir o padrão ADR-030 (policy HTTP + `IAuthorizedRequest`
> na mesma camada, sem literais de role; guarda
> `RoleAuthorizationStandardizationTests`) e o rate limit/health já existentes
> do F20-T001.

### Fase 11 — Testes e validação real

Executar testes unitários para:

- Mapeamento das respostas.
- Paginação.
- Tratamento de erros.
- Normalização.
- Correlação.
- Idempotência da sincronização.
- Regras de atualização de estado.

Executar testes de integração para:

- Autenticação real.
- Listagem de sites.
- Listagem de dispositivos.
- Consulta de clientes e redes.
- Detalhes e estatísticas de dispositivos.
- Dados de portas.
- Sincronização repetida.
- Falhas e recuperação.

Validar o frontend para carregamento, filtros, estados vazios, erros e
atualização de dados.

**Critério de saída:** testes automatizados aprovados e evidência de consultas
reais contra o controlador existente.

## 4. Modelo de dados sugerido

O modelo definitivo deve ser adaptado ao domínio já existente no InfraControl.

| Entidade | Finalidade |
| --- | --- |
| `UniFiConnection` | Configuração e estado da integração |
| `UniFiSite` | Sites conhecidos |
| `NetworkDevice` | Equipamentos de rede |
| `NetworkPort` | Interfaces de switch |
| `NetworkClientObservation` | Observações de clientes |
| `NetworkDefinition` | Redes consultadas |
| `NetworkConnection` | Ligações com origem identificada |
| `TopologySnapshot` | Estado observado da topologia |
| `IntegrationSyncRun` | Execuções e resultados da sincronização |
| `DeviceAssetLink` | Relação entre dispositivo externo e Asset |

Os registros devem conservar o identificador externo e a data da última
atualização. **Não duplicar entidades se o InfraControl já possuir modelos
equivalentes** (ex.: `Device`, `ExternalDeviceReference`, `Connection`,
`MonitoringEvent`, histórico de auditoria — auditar antes de criar tabelas).

## 5. Regras obrigatórias para o executor do plano

### Regras de implementação

- Auditar o código antes de criar módulos ou tabelas.
- Consultar a documentação oficial correspondente à versão instalada.
- Não inventar endpoints, campos ou dados de conexão.
- Não criar uma nova instalação do UniFi se já existe uma.
- Não executar ações de alteração na primeira versão.
- Não mostrar conexões físicas sem evidências.
- Não tratar falha de consulta como prova de que um equipamento está offline.
- Não considerar a documentação como evidência de implementação.
- Validar a integração real, não apenas a compilação.

### Regras do AGENTS.md do repositório continuam valendo

- TDD (RED→GREEN), suítes unit/arch/integration verdes antes de fechar fase.
- Sem `git push`/commit sem pedido explícito do usuário.
- Uma task principal `IN_PROGRESS` por vez (trabalho paralelo só com
  autorização explícita — `TASKS.md` regra operacional).
- Não inventar requisitos nem alterar arquitetura sem ADR.
- Checkpoint ao fim de cada fase: resumo, arquivos, testes, pendências,
  próximo passo.

## 6. Ordem final de execução

1. Auditoria técnica e API.
2. Fundação e autenticação.
3. Configuração administrativa.
4. Sites e dispositivos.
5. Clientes e redes.
6. Portas de switches.
7. Topologia visual.
8. Correlação com Assets.
9. Dashboard de infraestrutura.
10. Histórico e auditoria.
11. Segurança operacional.
12. Testes reais, documentação e aceite.

**Resultado final:** o InfraControl terá uma integração UniFi com configuração
administrativa, inventário consultável, visualização de portas e clientes,
topologia interativa baseada em dados disponíveis e correlação com o
patrimônio. O que não puder ser descoberto pela API será identificado como
desconhecido ou complementado por outra fonte validada — **nunca inventado**.

## 7. Pendências de decisão antes da Fase 1

Registradas para o usuário decidir no momento da autorização de execução
(não decididas agora):

1. **Breakdown em tasks** — este plano ainda não está no `TASKS.md`; a Fase 0
   deve virar tasks com escopo/critérios/testes claros (regra "dividir para
   conquistar") antes de implementar.
2. **API Key vs cookie** — o cliente atual autentica por cookie (`login` →
   `unifises`); "API Key quando suportada" depende da versão instalada
   (decidir na Fase 0, sem inventar).
3. **Escopo vs F20-T002/T003** — webhooks e consultas sob demanda/Redis já
   existem como tasks; o plano deve absorvê-las ou sequenciar-se com elas.
4. **Entidades novas** — confirmar, após a auditoria da Fase 0, quais das
   entidades sugeridas (§4) realmente são necessárias frente aos modelos
   existentes.
