# docs/38 — Plano do Módulo de Projetos (Projects)

> **Origem:** Planejamento arquitetural aprovado pelo usuário em **2026-10-10** (ADR-043).
> Este documento consolida a especificação do módulo de Projetos no InfraControl,
> estruturando iniciativas de infraestrutura de TI com fases sequenciais,
> acompanhamento bilateral, homologação pelo Administrador e geração automática
> de documentação na Base de Conhecimento (Knowledge Base).

---

## 1. Objetivo e Motivação

O módulo **Projetos** é responsável por estruturar, executar, acompanhar e documentar iniciativas corporativas de infraestrutura, redes e sistemas de TI.

Diferente de um chamado pontual (`Task`) ou do plano de carreira de um colaborador (`PDI`), um **Projeto** no InfraControl representa um esforço coordenado com escopo delimitado, orçamento/recursos, equipe multidisciplinar, esteira de fases sequenciais e homologação formal de encerramento pelo Administrador.

Ao ser aprovado pelo Administrador, o projeto consolida automaticamente todo o seu histórico, entregáveis, lições aprendidas e evidências em um documento técnico formal na **Base de Conhecimento** (`Knowledge Base`).

---

## 2. Diferenciação Conceitual de Domínio

```text
┌─────────────────┐      ┌─────────────────────────┐      ┌───────────────────────────┐
│     TASKS       │      │           PDI           │      │         PROJECTS          │
├─────────────────┤      ├─────────────────────────┤      ├───────────────────────────┤
│ • Chamados      │      │ • Desenvolvimento       │      │ • Iniciativas Estruturais │
│ • Incidentes    │      │   individual de pessoas │      │ • Migrações & Expansões   │
│ • Manutenções   │      │ • Competências e Metas  │      │ • Equipes & Fases         │
│ • Rotinas de TI │      │ • 1 plano por usuário   │      │ • Homologação Admin       │
│ • Operacional   │      │ • RH & Liderança        │      │ • Gera Knowledge Base     │
└─────────────────┘      └─────────────────────────┘      └───────────────────────────┘
```

---

## 3. Modelo de Domínio e Entidades

### 3.1. Agregado Raiz: `Project` (`projects`)

- **`Id`**: Identificador único (`Guid`).
- **`Code`**: Código amigável sequencial (ex.: `PRJ-001`).
- **`Title`**: Título descritivo do projeto (até 150 caracteres).
- **`Description`**: Escopo técnico detalhado e objetivos (até 4000 caracteres).
- **`Justification`**: Justificativa e motivação do projeto (até 2000 caracteres).
- **`Priority`**: `ProjectPriority` (`Low`, `Medium`, `High`, `Critical`).
- **`Status`**: `ProjectStatus` (fluxo de 7 estados da §4).
- **`LeaderId`**: Líder técnico do projeto (`Guid`, sem FK destrutiva, resolvido via `DomainUsers`).
- **`ManagerId`**: Patrocinador ou gestor da área (`Guid?`).
- **`AdminApproverId`**: Administrador que realizou a homologação final (`Guid?`).
- **`ApprovalNotes`**: Parecer e observações do Administrador no encerramento (até 2000 caracteres).
- **`ApprovedAt`**: Carimbo de data/hora UTC da homologação formal (`DateTimeOffset?`).
- **`PlannedStartDate` / `PlannedEndDate`**: Datas planejadas (`DateOnly?`).
- **`ActualStartDate` / `ActualEndDate`**: Datas reais de início e conclusão (`DateOnly?`).

### 3.2. Fases do Projeto: `ProjectPhase` (`project_phases`)

- **`ProjectId`**: Vínculo com o projeto (`Guid`, FK cascade).
- **`Title`**: Nome da etapa (ex.: "1. Levantamento e Arquitetura", "2. Configuração em Bancada", "3. Migração").
- **`Description`**: Detalhamento do escopo da fase (até 2000 caracteres).
- **`Order`**: Posição na esteira do projeto (`int`: 1, 2, 3...).
- **`ResponsibleId`**: Responsável técnico pela fase (`Guid?`).
- **`PlannedStartDate` / `PlannedEndDate`**: Datas previstas da fase (`DateOnly?`).
- **`ActualStartDate` / `ActualEndDate`**: Datas reais da fase (`DateOnly?`).
- **`CompletionCriteria`**: Critérios formais de aceite para conclusão (até 1000 caracteres).
- **`Status`**: `ProjectPhaseStatus` (`NotStarted`, `InProgress`, `PendingValidation`, `Completed`).
- **`Percent`**: Percentual concluído da fase (0 a 100%).

### 3.3. Equipe do Projeto: `ProjectMember` (`project_members`)

- **`ProjectId`**: FK cascade.
- **`UserId`**: FK cascade.
- **`Role`**: `ProjectMemberRole` (`Leader`, `Member`, `Reviewer`).

### 3.4. Acompanhamento Contínuo (Tracking)

- **Progresso (`ProjectProgress` / `project_progresses`)**:
  - Relatos de evolução com texto livre, autor, data e percentual estimado do projeto.
- **Impedimentos & Riscos (`ProjectImpediment` / `project_impediments`)**:
  - `Title`, `Description`, `Category` (`Technical`, `Vendor`, `Budget`, `SecurityAccess`), `Severity` (`Low`, `Medium`, `High`, `Critical`), `MitigationPlan`, `Status` (`Open`, `Resolved`), `ResolvedAt`.
- **Evidências Técnicas (`ProjectEvidence` / `project_evidences`)**:
  - Upload e armazenamento em disco seguro (`var/project-evidence`), proteção nosniff, limite de 10 MB.
  - Tipos: `TestReport`, `ConfigurationBackup`, `ArchitectureDiagram`, `AcceptanceMinutes`, `Other`.
  - Status: `Pending`, `Validated`, `Rejected` com motivo de rejeição.
- **Histórico & Linha do Tempo (`ProjectHistoryEntry` / `project_history_entries`)**:
  - Append-only: registra criação, alterações de escopo/prazo ("de → para"), transições de fase e homologação.

---

## 4. Ciclo de Vida do Projeto (`ProjectStatus`)

```text
       ┌───────────┐
       │   Draft   │  (Planejamento inicial)
       └─────┬─────┘
             │ Iniciar / Kickoff
             ▼
       ┌───────────┐
       │  Planned  │  (Planejado / Aguardando início formal)
       └─────┬─────┘
             │ Ativar
             ▼
       ┌───────────┐         Suspender
       │InProgress │ ────────────────────────► ┌──────────┐
       │ (Active)  │ ◄──────────────────────── │  OnHold  │
       └─────┬─────┘          Retomar          └──────────┘
             │ Conclusão de todas as fases
             ▼
┌────────────────────────┐
│  PendingFinalApproval  │  (Submetido para validação formal)
└────────────┬───────────┘
             │
             ├── Homologado pelo Admin (com parecer)
             ▼
       ┌───────────┐
       │ Completed │ ──► Dispara GERAÇÃO AUTOMÁTICA na Knowledge Base
       └───────────┘
```

> **Cancelamento**: Um projeto em qualquer estado não concluído pode ser movido para `Cancelled`, exigindo motivo de cancelamento registrado na linha do tempo.

---

## 5. Governança e Homologação pelo Administrador

1. **Submissão para Encerramento**:
   - Quando as fases são concluídas com seus critérios e evidências, o líder aciona:
     `POST /api/v1/projects/{id}/submit-approval` (permissão `Operate`).
   - O projeto passa para o status `PendingFinalApproval`.
2. **Homologação pelo Administrador**:
   - Restrito à role `Admin` (Policy `Administer`).
   - Endpoint: `POST /api/v1/projects/{id}/approve`.
   - Payload:
     ```json
     {
       "approvalNotes": "Projeto homologado com sucesso. Todos os switches core foram migrados para o novo firmware e testes de redundância validados."
     }
     ```
   - Efeitos atômicos:
     - O status transiciona para `Completed`.
     - `AdminApproverId` é preenchido com o `UserId` do Admin logado.
     - `ApprovedAt` recebe o timestamp UTC corrente.
     - `ApprovalNotes` é gravado no projeto.
     - Evento de domínio `ProjectApprovedDomainEvent` é emitido.

---

## 6. Geração Automática na Base de Conhecimento (`Knowledge Base`)

Seguindo o princípio determinístico do **ADR-035** (sem alucinações, sem LLM externo, baseado exclusivamente nos registros reais existentes):

### 6.1. Composição Determinística (`KnowledgeDraftComposer.ComposeFromProject`)

O rascunho Markdown gerado reúne estruturadamente:

1. **Banner de Proveniência**:
   `> Documentação técnica gerada automaticamente a partir da homologação do Projeto PRJ-001 pelo Administrador [Nome] em [Data]. Revise antes de publicar.`
2. **Visão Geral e Escopo**:
   - Título, Código, Justificativa e Escopo técnico.
3. **Equipe e Governança**:
   - Líder técnico, Gestor/Patrocinador, Participantes da equipe e Administrador homologador.
4. **Cronograma e Prazos**:
   - Datas planejadas vs. datas reais executadas e duração do ciclo.
5. **Fases Executadas e Entregáveis**:
   - Tabela de fases com ordenação, percentual de conclusão e critérios formais de aceite atendidos.
6. **Impedimentos e Lições Aprendidas**:
   - Problemas enfrentados durante a execução, impacto e ações mitigatórias adotadas (memória técnica reutilizável pela organização).
7. **Evidências e Artefatos Homologados**:
   - Relação de relatórios de testes, arquivos de configuração e topologias anexadas.
8. **Parecer Final de Homologação**:
   - Parecer emitido pelo Administrador no encerramento.

### 6.2. Persistência na Base de Conhecimento

- **Entidade**: `KnowledgeArticle.Create(...)`
- **Título**: `"Documentação Técnica do Projeto: [Título do Projeto]"`
- **Tipo**: `KnowledgeArticleType.TechnicalDocumentation` (ou `OperationalKnowledge`)
- **Status Inicial**: `Draft` ou `InReview` (permitindo revisão da equipe antes da publicação pública, conforme ADR-034)
- **Origem**: `KnowledgeArticleOrigin.Create(article.Id, KnowledgeOriginType.Project, project.Id)`
- **Versão**: `KnowledgeArticleVersion.SnapshotOf(article, "Geração automática a partir da homologação do Projeto '{project.Title}'", adminUserId)`

---

## 7. Estrutura de Execução em Fases (Trilhas de Desenvolvimento)

| Fase | Task | Escopo |
| :--- | :--- | :--- |
| **F28** | **F28-T001** | **ADR-043 e Especificação Arquitetural** (Concluído). |
| **F28** | **F28-T002** | **Domínio Base e Persistência**: Entidades `Project`, `ProjectMember`, enums, eventos de domínio, DbContext, repositório e migração EF Core. |
| **F28** | **F28-T003** | **Fases do Projeto e Estrutura**: Entidade `ProjectPhase`, ordenação, status de fase, critérios de aceite, comandos CQRS e testes. |
| **F28** | **F28-T004** | **Acompanhamento (Tracking)**: Entidades `ProjectProgress`, `ProjectImpediment`, `ProjectEvidence` (com storage seguro em disco `var/project-evidence`), histórico append-only e testes. |
| **F28** | **F28-T005** | **Governança e Homologação Admin**: Fluxo de submissão e aprovação formal pelo Admin (`ApproveProjectCommand`), bloqueios de transição e testes. |
| **F28** | **F28-T006** | **Integração com Knowledge Base**: Adição de `KnowledgeOriginType.Project`, método `KnowledgeDraftComposer.ComposeFromProject`, comando `GenerateProjectKnowledgeCommand` e testes de geração determinística. |
| **F28** | **F28-T007** | **API e Controllers**: `ProjectsController`, ViewModels, autorização RBAC (`Administer`, `Operate`, `Authorize`) e endpoints REST. |
| **F28** | **F28-T008** | **Frontend UI**: Rota `/projects`, listagem UniFi/PrimeReact, página de detalhe `/projects/:id` com abas (Visão Geral, Fases, Acompanhamento, Impedimentos, Evidências, Homologação do Admin e botão de Gerar Knowledge). |
