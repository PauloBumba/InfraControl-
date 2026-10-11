# ADR-043: Módulo de Projetos — Ciclo de Vida, Fases, Acompanhamento, Governança e Geração na Knowledge Base

- **Status**: aceito
- **Data**: 2026-10-10
- **Tasks**: Fase 28 (Módulo de Projetos)
- **Origem**: Requisito de produto aprovado pelo usuário em 2026-10-10

---

## Contexto

O InfraControl conta com o módulo de **Tasks** (unidade genérica de trabalho operacional, tíquetes, incidentes e manutenções) e o módulo de **PDI** (Plano de Desenvolvimento Individual, focado em pessoas e competências de RH).

No entanto, a gestão de infraestrutura de TI executa frequentemente **Projetos Institucionais** (ex.: migração de datacenter, expansão de redes Wi-Fi, modernização de switches de core, implantação de Zabbix/SDN, planos de disaster recovery, adequação LGPD). Projetos possuem características próprias:
1. **Multidisciplinares**: contam com equipe dedicada (`ProjectMember`), líder técnico (`LeaderId`) e patrocinador/gestor (`ManagerId`).
2. **Estruturados em Fases**: divididos em esteiras sequenciais de etapas (ex.: Planejamento, Aquisição/Bancada, Implantação, Homologação/Testes, Virada) com critérios de aceite formais por fase.
3. **Acompanhamento Contínuo**: exigem registros periódicos de avanço (`ProjectProgress`), gestão de riscos e impedimentos (`ProjectImpediment`) e coleta de evidências técnicas (`ProjectEvidence` com arquivos em storage local).
4. **Governança Formal e Homologação pelo Administrador**: a conclusão do projeto depende do aceite formal de um Administrador (`AdminApproverId`), garantindo que o escopo foi entregue com qualidade.
5. **Preservação de Conhecimento Organizacional**: a especificação original do módulo de Knowledge (`docs/32 §9`) já definia que Projetos deveriam alimentar a Base de Conhecimento através de rastreabilidade de origem (`KnowledgeArticleOrigin`), aguardando a concepção do módulo.

---

## Decisão

Implementar o módulo de **Projetos** (`Projects`) como um contexto delimitado (Bounded Context) independente de Tasks e de PDI, integrado à Base de Conhecimento conforme os padrões consolidados no repositório.

### 1. Entidade Raiz: `Project` (`projects`)

- **Identificação**:
  - `Code`: código legível (ex.: `PRJ-001`, gerado sequencialmente ou amigável).
  - `Title`: título descritivo (máximo 150 caracteres, obrigatório).
  - `Description`: escopo e objetivos detalhados (máximo 4000 caracteres, obrigatório).
  - `Justification`: justificativa do investimento/iniciativa (máximo 2000 caracteres, opcional).
  - `Priority`: `ProjectPriority` (`Low`, `Medium`, `High`, `Critical`), default `Medium`.
- **Pessoas & Papéis**:
  - `LeaderId`: líder técnico responsável (obrigatório, sem FK destrutiva, resolvido via `DomainUsers`).
  - `ManagerId`: patrocinador ou gestor da área (opcional).
  - `AdminApproverId`: administrador que homologou a conclusão (preenchido no encerramento).
  - `ApprovalNotes`: parecer formal do administrador na homologação (máximo 2000 caracteres).
  - `ApprovedAt`: carimbo de data/hora UTC da homologação.
- **Prazos**:
  - `PlannedStartDate` / `PlannedEndDate`: datas previstas do cronograma inicial (`DateOnly?`).
  - `ActualStartDate` / `ActualEndDate`: datas reais de execução (`DateOnly?`).
- **Ciclo de Vida (`ProjectStatus`)**:
  - `Draft` (Elaboração do planejamento inicial).
  - `Planned` (Planejado, aguardando início/kickoff).
  - `InProgress` (Em execução ativa das fases).
  - `PendingFinalApproval` (Fases concluídas, submetido para validação formal do Administrador).
  - `Completed` (Homologado e aprovado pelo Administrador — estado terminal que habilita a geração na Knowledge Base).
  - `Cancelled` (Cancelado com justificativa obrigatória no histórico).
  - `OnHold` (Pausado temporariamente com motivo registrado).

### 2. Estrutura de Fases: `ProjectPhase` (`project_phases`)

- Cada projeto possui uma lista de fases ordenadas sequencialmente (`Order`).
- Atributos: `Title` (≤150), `Description` (≤2000), `Order`, `ResponsibleId` (usuário responsável pela fase), `PlannedStartDate`, `PlannedEndDate`, `ActualStartDate`, `ActualEndDate`, `CompletionCriteria` (≤1000).
- `Status`: `ProjectPhaseStatus` (`NotStarted`, `InProgress`, `PendingValidation`, `Completed`).
- `Percent`: 0 a 100% de conclusão da fase.
- Transições de fase:
  - O líder inicia a fase (`Start` ➔ `InProgress`).
  - O executor conclui as atividades e submete a fase (`Submit` ➔ `PendingValidation`).
  - O líder/gestor valida os critérios (`Validate` ➔ `Completed` com avanço/auto-start para a próxima fase, similar ao PDI ADR-035).

### 3. Acompanhamento do Projeto (Tracking)

- **Progresso (`ProjectProgress` / `project_progresses`)**:
  - Relatos de evolução com percentual global reportado, texto livre descritivo, autor e data.
- **Impedimentos & Riscos (`ProjectImpediment` / `project_impediments`)**:
  - `Title`, `Description`, `Category` (`Technical`, `Vendor`, `Budget`, `SecurityAccess`), `Severity` (`Low`, `Medium`, `High`, `Critical`), `MitigationPlan`, `Status` (`Open`, `Resolved`), `ResolvedAt`.
  - Impedimentos não resolvidos são destacados na visão de governança e impedem ou alertam a aprovação final.
- **Evidências Técnicas (`ProjectEvidence` / `project_evidences`)**:
  - Arquivos anexados de homologação (relatórios de teste, topologias, configs de equipamentos, atas de homologação).
  - Armazenamento em disco seguro no diretório `var/project-evidence` (reutilizando a infraestrutura do ADR-033/034).
  - Arquivo limitado a 10 MB, nome de arquivo saneado contra path traversal, com status `Pending`, `Validated`, `Rejected` e notas de revisão.
- **Histórico e Linha do Tempo (`ProjectHistoryEntry` / `project_history_entries`)**:
  - Append-only: rastreia criação, edição de escopo, alterações de prazo ("de → para"), transições de fase e homologação.

### 4. Governança e Homologação pelo Administrador

- Quando todas as fases são finalizadas, o líder técnico submete o projeto para encerramento (`POST /api/v1/projects/{id}/submit-approval`). O projeto passa para `PendingFinalApproval`.
- **Aprovação exclusiva por Admin**:
  - Policy `Administer` (somente administradores).
  - Comando `ApproveProjectCommand` (`POST /api/v1/projects/{id}/approve`):
    - Recebe o parecer formal de encerramento (`ApprovalNotes`).
    - Transiciona o status para `Completed`.
    - Carimba `AdminApproverId` e `ApprovedAt`.
    - Dispara o evento de domínio `ProjectApprovedDomainEvent`.

### 5. Geração Automática na Base de Conhecimento (`Knowledge Base`)

- **Montagem Determinística (ADR-035)**:
  - A geração da documentação técnica não utiliza modelos de linguagem (LLM) e não inventa dados.
  - O `KnowledgeDraftComposer.ComposeFromProject(...)` consolida os registros reais do projeto em formato Markdown estruturado:
    1. *Banner de Proveniência*: Referência ao Projeto #{Code}, data de homologação e Administrador aprovador.
    2. *Objetivo, Escopo e Justificativa*: Dados cadastrais e contextualização do projeto.
    3. *Equipe e Liderança*: Líder, gestor e participantes.
    4. *Cronograma Executado*: Prazos planejados vs. datas reais de início e término.
    5. *Fases e Entregáveis*: Relação das fases executadas com status e critérios de aceite comprovados.
    6. *Impedimentos e Soluções (Lições Aprendidas)*: Histórico de dificuldades enfrentadas e soluções aplicadas (memória técnica reutilizável).
    7. *Evidências Homologadas*: Relação dos testes e artefatos validados.
    8. *Parecer do Administrador*: Parecer formal emitido na homologação.
- **Integração com o Módulo Knowledge**:
  - Estender o enum `KnowledgeOriginType` adicionando `Project`.
  - Criar o `KnowledgeArticle` com tipo `TechnicalDocumentation` (ou `OperationalKnowledge`).
  - Registrar a versão inicial `v1.0` em `knowledge_article_versions` com o motivo de proveniência.
  - Registrar a origem em `knowledge_article_origins` (`OriginType = Project`, `OriginId = project.Id`).
  - O artigo nasce no estado `Draft` (conforme workflow do ADR-034), permitindo que a equipe revise ou publique diretamente.

---

## Consequências

- **Clareza de Domínio**: Delimitação estrita entre Operações/Tíquetes (`Task`), Desenvolvimento Humano (`PDI`) e Projetos Estruturados de Infraestrutura (`Project`).
- **Preservação de Conhecimento**: Garante que projetos concluídos não fiquem esquecidos, gerando documentação técnica padronizada automaticamente na Base de Conhecimento.
- **Rastreabilidade e Governança**: Nenhuma iniciativa é encerrada sem parecer formal do Administrador e trilha completa de auditoria.
- **Sem Débito Técnico**: Segue os mesmos padrões de DDD, CQRS, MediatR, Outbox, PostgreSQL, armazenamento de evidências e testes automatizados já consagrados no repositório.
