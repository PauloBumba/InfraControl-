# docs/30 — Plano do módulo PDI (Plano de Desenvolvimento Individual)

> **Origem:** documento de produto colado pelo usuário em **2026-10-04** como
> plano oficial do módulo. O texto abaixo preserva o conteúdo do usuário
> (requisito de produto, não inventado pela IA). O **mínimo viável já
> implementado** está no cabeçalho; a evolução segue as **8 fases** da §25,
> **uma task por fase**, com ADR antes de qualquer mudança arquitetural.
>
> ## Situação atual (2026-10-07) — o que já existe
>
> - `IndividualDevelopmentPlan` (1 por usuário): estratégia, prazo,
>   status, **gestor (`ManagerId`), período (`PeriodStart/End`)**, ator
>   do change;
> - **ciclo de vida de 9 estados com transições validadas** (tabela da
>   §3, ADR-029): Rascunho→Planejado→Aguardando aceite→Ativo→Em
>   acompanhamento→Em avaliação→Concluído; Ativo→Replanejado→Ativo;
>   Ativo→Cancelado; Completed/Cancelled terminais;
> - **histórico/timeline** da §17: `individual_development_plan_history`
>   (append-only, Created/Updated/StatusChanged + motivo), endpoint
>   `GET /pdi/{userId}/history`;
> - **estrutura do desenvolvimento (Fase 2, ADR-032)**: catálogo global
>   de competências (`pdi_competencies`, escrita Admin, duplicata em
>   lowercase → 409) e árvore **objetivos → estratégias → fases →
>   atividades** (tabelas `pdi_*`, FKs `Restrict` → 409 com filhos,
>   ordenação `sort_order`, fase com status provisório
>   NotStarted/InProgress/Completed + progresso, prazos), ownership
>   único `PdiAccess` (Admin qualquer plano / Operador só o próprio),
>   `GET /pdi/{userId}/structure` + CRUD dos 5 níveis, `PdiStructureDialog`
>   no front (árvore, forms, catálogo, responsável Admin=usuários /
>   Operador=plano+gestor);
> - permissões: Admin cria/edita/transiciona/lista tudo (gestor/período
>   só o Admin define), Operador edita só o próprio e **só o aceite
>   (Aguardando aceite → Ativo)** muda status, Viewer sem acesso (rota
>   `operate`);
> - `GET/PUT /api/v1/pdi` + `PdiPage` (`/pdi`, seletor de status com os
>   próximos estados válidos, gestor, período, motivo, timeline,
>   botão Estrutura);
> - **acompanhamento bilateral (Fase 3, ADR-033)**: quatro entidades sob
>   a fase — **progresso** (`pdi_progresses`, relato + comentário
>   append-only), **check-in** (`pdi_check_ins`, bilateral: relato do
>   dono + resposta do gestor, PUT por lado com `null` preserva / `""`
>   limpa), **impedimento** (`pdi_impediments`, tipo/descrição/impacto/
>   responsável/ação + status Open→Resolved com `ResolvedAt` derivado) e
>   **feedback** (`pdi_feedbacks`, histórico: autor→destinatário, tipo,
>   conteúdo, data; excluir = só Admin), FKs `Restrict` → 409, permissão
>   por ação em `PdiAccess` (ler: Admin/dono/gestor · relato: Admin/dono ·
>   responder: Admin/gestor · editar impedimento: Admin/dono/gestor),
>   `GET /pdi/{userId}/accompaniment` único, `PdiAccompanimentDialog`
>   (abas Check-ins/Progresso/Impedimentos/Feedback) pelo botão
>   "Acompanhar" de cada fase;
> - eventos `PdiCreated/Updated` → outbox → RabbitMQ → notificações
>   (criação: dono Email+SignalR; edição: dono ou Admin por role, só
>   SignalR);
> - testes `PdiCommandTests` (15) + `PdiTransitionsTests` (20) +
>   `PdiStructureTests` (11) + `PdiAccompanimentTests` (9) + suítes
>   verdes; migrações `20261005011124`, `20261006001904_PdiDevelopmentStructure`
>   e `20261006013525_PdiAccompaniment` aplicadas no dev;
>   containers/ smoke/ E2E;
> - **evidências (Fase 4, ADR-034)**: entidade `PdiEvidence`
>   (`pdi_evidences` sob a fase, FK `Restrict`) com os **9 tipos** do §12
>   (enum + rótulos pt-BR), status `Pending→Validated/Rejected`
>   (**revisão única** → 409, rejeição exige motivo), arquivo ≤10 MB com
>   nome saneado (`../evil.sh` → `evil.sh`), storage em disco
>   `var/pdi-evidence` (volume nomeado no compose + `client_max_body_size`
>   16m no nginx), upload `POST /pdi/evidences` (multipart), revisão
>   `PUT .../review`, download `GET .../download` (nosniff + attachment),
>   5ª lista no `GET /pdi/{userId}/accompaniment`, histórico
>   `EvidenceAdded`/`EvidenceReviewed` (sem migração — conversion string),
>   permissão por ação (enviar Admin/dono · revisar Admin/gestor · ler
>   Admin/dono/gestor), aba "Evidências" no `PdiAccompanimentDialog`;
>   testes `PdiEvidenceTests` (10) + migração `20261007015235_PdiEvidence`
>   aplicada no dev; E2E 50/50.
> - **governança (Fase 5, ADR-035)**: §14 — `Update` do plano exige
>   **motivo** quando campo muda (só status mantém opcional) e o
>   histórico `Updated` guarda `estratégia/prazo/gestor/período:
>   anterior → novo` + motivo; §15.2 — `PdiPhaseStatus.PendingValidation`
>   com `POST /pdi/phases/{id}/submit` (dono/Admin) e
>   `PUT .../validation` (Admin/gestor; rejeição com motivo → volta a
>   `InProgress`; validação → `Completed` + **auto-start** da próxima
>   fase), guard no PUT genérico (não-Admin não conclui/reabre fase →
>   400); §15.1 aceite sem motivo já vigente; §17 — timeline ganha
>   `PhaseStarted/Submitted/Validated/Rejected`,
>   `CheckInCreated`, `ImpedimentCreated/Resolved` (sem migração);
>   auditoria — todos os 30 comandos PDI com `IAuditableRequest`
>   (`AuditResource = "PDI"`); front (botões Concluir/Validar/Rejeitar
>   com diálogo de motivo, motivo obrigatório no plano quando campo
>   muda, status restrito por papel); testes `PdiGovernanceTests` (11);
>   E2E 63/63 (sem migração).
>
> - **avaliação final (Fase 6, ADR-036)**: entidades `PdiAssessment`
>   (`pdi_assessments`, 1 linha por lado — `Self` autoavaliação do
>   colaborador / `Manager` avaliação do gestor, autor + conteúdo
>   ≤4000, upsert) e `PdiResult` (`pdi_results`, único por plano: 4
>   desfechos do §16 + nota opcional + carimbo do finalizador); fluxo
>   do §15.3 imposto pelo backend — autoavaliação leva o plano a
>   **Em avaliação** (+`StatusChanged` no timeline), gestor exige a
>   autoavaliação, resultado exige as duas avaliações e é imutável
>   (2ª → 409; plano concluído congela a avaliação); **seta nova**
>   `UnderEvaluation → Replanned` para o resultado "Replanejado"
>   (§16 + §3) — os demais → `Completed`; rotas
>   `GET /pdi/{userId}/evaluation`, `PUT .../assessment`,
>   `PUT .../result`; histórico `AssessmentCreated`/`ResultFinalized`
>   (sem migração de dados); auditoria nos 2 comandos novos; botão
>   "Avaliar" + `PdiEvaluationDialog` (snapshot + 2 avaliações +
>   resultado, leitura após finalizar); migração
>   `20261007233505_PdiEvaluation`; testes `PdiEvaluationTests` (15);
>   E2E 31/31.
>
> **Fases 1 (§25), 2, 3, 4, 5 e 6 executadas** — ver ADR-029, ADR-032,
> ADR-033, ADR-034, ADR-035 e ADR-036.
> Gap para a **Fase 7**: gestão (dashboard do colaborador; dashboard
> do gestor; dashboard administrativo).

---

## 1. Conceito do módulo

O PDI será responsável por acompanhar o desenvolvimento individual de uma pessoa durante um período determinado.

O fluxo não termina na criação do plano. Ele acompanha:

Diagnóstico
   ↓
Planejamento
   ↓
Objetivos
   ↓
Estratégias
   ↓
Fases
   ↓
Execução
   ↕
Acompanhamento bilateral
   ↕
Colaborador ↔ Liderança
   ↓
Evidências / Dificuldades / Feedbacks
   ↓
Avaliação
   ↓
Resultado
   ↓
Relatório / Indicadores
   ↓
Próximo ciclo

## 2. Participantes

**Administrador**
Responsável pela configuração e visão organizacional.
Pode:

- configurar períodos;
- gerenciar modelos;
- configurar competências;
- configurar tipos de desenvolvimento;
- visualizar PDIs;
- gerar relatórios;
- consultar indicadores;
- administrar permissões.

Não precisa participar do acompanhamento diário.

**Liderança / Gestor**
É o responsável pelo desenvolvimento.
Pode:

- criar PDI;
- definir objetivos;
- definir estratégias;
- criar fases;
- estabelecer prazos;
- acompanhar progresso;
- dar feedback;
- validar evidências;
- registrar avaliações;
- ajustar estratégias;
- replanejar fases.

**Colaborador**
É participante ativo.
Pode:

- visualizar seu PDI;
- aceitar/participar do planejamento;
- atualizar fases;
- registrar progresso;
- fazer check-ins;
- informar dificuldades;
- solicitar ajuda;
- anexar evidências;
- responder feedbacks;
- fazer autoavaliação;
- solicitar alteração de prazo/estratégia.

## 3. Ciclo de vida do PDI

Estados claros:

RASCUNHO
   ↓
PLANEJADO
   ↓
AGUARDANDO ACEITE
   ↓
ATIVO
   ↓
EM ACOMPANHAMENTO
   ↓
EM AVALIAÇÃO
   ↓
CONCLUÍDO

Com possibilidades alternativas:

ATIVO
  ↓
REPLANEJADO
  ↓
ATIVO

ou:

ATIVO
  ↓
CANCELADO

Importante: **não apagar histórico**.

## 4. Diagnóstico inicial

Antes do objetivo, registrar o contexto.

Exemplo:

- Pessoa: João
- Competência: Liderança
- Situação atual: possui conhecimento técnico, mas apresenta dificuldade em delegação e condução de equipe.
- Situação desejada: desenvolver capacidade de conduzir pequenas atividades e distribuir responsabilidades.

Isso permite comparar depois:

ANTES
↓
DESENVOLVIMENTO
↓
DEPOIS

## 5. Objetivos

Um PDI pode ter um ou vários objetivos.

Exemplo (PDI João): Objetivo 1 "Desenvolver liderança"; Objetivo 2 "Melhorar comunicação"; Objetivo 3 "Desenvolver capacidade de delegação".

Cada objetivo pode possuir:

- descrição;
- competência;
- situação atual;
- situação desejada;
- resultado esperado;
- indicador;
- prazo.

## 6. Estratégias

A estratégia responde: **como vamos desenvolver essa competência?**

Exemplo:

- Objetivo: desenvolver liderança
- Estratégia: curso de liderança, acompanhamento do gestor, participação em reuniões, condução de pequenas atividades.

Uma estratégia pode gerar várias fases.

## 7. Fases

Parte principal do modelo. Cada objetivo pode ter uma sequência de fases:

Objetivo
   ↓
Fase 1 — Capacitação
Fase 2 — Observação
Fase 3 — Aplicação
Fase 4 — Acompanhamento
Fase 5 — Avaliação
Fase 6 — Consolidação

Cada fase terá:

- título;
- descrição;
- objetivo;
- responsável;
- data inicial;
- data final;
- critérios de conclusão;
- status;
- percentual;
- atividades;
- evidências;
- dificuldades;
- feedbacks.

## 8. Atividades

Dentro da fase podem existir ações menores.

Exemplo — Fase "Capacitação": ☐ Realizar curso · ☐ Ler material · ☐ Participar de treinamento · ☐ Apresentar aprendizados.

Isso permite diferenciar:

- **fase** = marco de desenvolvimento;
- **atividade** = ação necessária para chegar ao marco.

## 9. Acompanhamento bilateral

Característica principal do PDI. Não é "gestor cria → colaborador executa":

```
              FASE
               │
      ┌────────┴────────┐
      ↓                 ↓
COLABORADOR           GESTOR
      │                 │
    Relato            Feedback
    Progresso         Orientação
    Dificuldade       Avaliação
    Evidência         Validação
      │                 │
      └────────┬────────┘
               ↓
            CHECK-IN
```

## 10. Check-ins

Acompanhamento periódico (ex.: mensal).

**Colaborador:** o que realizou? · o que aprendeu? · qual dificuldade encontrou? · precisa de suporte?

**Gestor:** feedback (ex.: "a evolução foi positiva; na próxima fase vamos trabalhar a aplicação prática") · próxima ação (ex.: "delegar uma atividade pequena da equipe").

## 11. Dificuldades e impedimentos

Entidade própria — não apenas campo de texto:

Impedimento
├── Tipo
├── Descrição
├── Impacto
├── Data
├── Responsável
├── Ação tomada
├── Status
└── Resolvido em

Exemplo: tipo "recurso indisponível", problema "treinamento não disponibilizado", impacto "atraso na fase", ação "reagendamento", novo prazo 15/05.

Assim é possível gerar indicadores de dificuldade.

## 12. Evidências

Mecanismo genérico de evidência.

**Tipos:** certificado · documento · imagem · arquivo · atividade realizada · avaliação · feedback · resultado · outro.

**Fluxo:** colaborador envia → evidência pendente → gestor analisa → validada / rejeitada (com motivo da rejeição).

## 13. Feedback

Histórico — não sobrescrever:

- Data
- Autor
- Destinatário
- Fase
- Tipo
- Conteúdo

Exemplo: 15/04 — Carlos — feedback sobre Fase 3 — "Você apresentou evolução na delegação…".

## 14. Replanejamento

O PDI aceita mudanças: prazo original 30/04 → impedimento → replanejamento → novo prazo 15/05.

Toda alteração deve registrar: **valor anterior; novo valor; motivo; quem alterou; quando alterou**. Nada de editar e perder o histórico.

## 15. Aprovações

Aprovação em pontos específicos:

- gestor cria PDI → colaborador analisa → colaborador aceita → PDI começa;
- colaborador conclui fase → gestor valida → próxima fase;
- colaborador faz autoavaliação → gestor faz avaliação → resultado final.

## 16. Avaliação final

Comparação:

- **Inicial** — o que precisava desenvolver?
- **Durante** — o que aconteceu?
- **Final** — o que foi desenvolvido?
- **Resultado** — atingido · parcialmente atingido · não atingido · replanejado.

Pode existir avaliação qualitativa.

## 17. Histórico completo (timeline)

O PDI deve possuir timeline:

01/01 — PDI criado · 05/01 — colaborador aceitou · 10/01 — fase 1 iniciada · 20/01 — certificado anexado · 21/01 — gestor validou evidência · 01/02 — fase 2 iniciada · 10/02 — check-in realizado · 15/03 — impedimento registrado · 20/03 — prazo alterado · 10/04 — fase concluída · 30/06 — avaliação final.

Importantíssimo para auditoria e relatório.

## 18. Dashboard do colaborador

```
MEU PDI — Desenvolvimento de Liderança

Progresso   ████████████████░░░░ 78%
Fases       ✓ Capacitação ✓ Observação ✓ Aplicação ● Avaliação ○ Consolidação
Próximo prazo 15/05 · Pendências 2 · Evidências 5 · Check-ins 4
```

## 19. Dashboard do gestor

```
MEUS PDIs — 12 colaboradores
🟢 7 dentro do prazo · 🟡 3 precisam de atenção · 🔴 2 atrasados
Fases próximas do vencimento: 4 · Impedimentos ativos: 3 · Evidências aguardando validação: 5
```

Acesso direto aos casos que precisam de atenção.

## 20. Dashboard administrativo

```
PDI — 1º SEMESTRE 2027
PDIs criados 42 · Concluídos 28 · Em andamento 11 · Replanejados 3 · Taxa de conclusão 67%
Fases concluídas 184 · Check-ins 231 · Evidências 156 · Impedimentos 37
```

## 21. Indicadores

**Execução:** quantidade de PDIs; taxa de conclusão; atrasos; fases concluídas/atrasadas; tempo médio de conclusão.

**Desenvolvimento:** competências trabalhadas; objetivos atingidos/parcialmente atingidos; evolução por competência.

**Acompanhamento:** quantidade e frequência de check-ins; feedbacks realizados; evidências validadas.

**Dificuldades:** tipos e quantidade de impedimentos; setores com maior ocorrência; tempo médio de resolução.

## 22. Relatórios

- **Relatório do PDI individual:** colaborador · gestor · período · objetivos · estratégias · fases · atividades · check-ins · dificuldades · evidências · feedbacks · avaliação final · resultado.
- **Relatório semestral:** quantidade · conclusão · evolução · competências · dificuldades · setores · gestores · tendências.
- **Relatório de dificuldades:** ex. "38% das dificuldades relacionadas à capacitação" → decisão estratégica de gestão ("precisamos criar um programa de treinamento").

É aí que o PDI começa a gerar informação estratégica, não apenas documentação.

## 23. Modelo de domínio

```
PDI
 ├── PDIObjective
 │     └── PDIStrategy
 │            └── PDIPhase
 │                   ├── PDIActivity
 │                   ├── PDIProgress
 │                   ├── PDICheckIn
 │                   ├── PDIEvidence
 │                   ├── PDIImpediment
 │                   └── PDIFeedback
 │
 ├── PDIAssessment
 ├── PDIResult
 └── PDIHistory

Entidades de apoio: Competency · DevelopmentType · PDIPeriod · PDIEvidenceType · PDIImpedimentType
```

## 24. Fluxo oficial completo

DIAGNÓSTICO → PLANEJAMENTO (objetivos + estratégia) → FASES → EXECUÇÃO ⇄ CHECK-INS ⇄ DIFICULDADES/APOIO → EVIDÊNCIAS → VALIDAÇÃO → (próxima fase | replanejar) → AVALIAÇÃO FINAL → RESULTADO → INDICADORES/RELATÓRIO.

## 25. Ordem de implementação no InfraControl

> "Como você já possui um PDI básico, eu não reescreveria tudo. Faria em etapas:"

- **Fase 1 — Fundamento:** revisar entidade PDI atual; pessoa/gestor; período; status; permissões; histórico. <!-- [x] Executada 2026-10-04 — ADR-029: 9 status + tabela de transições (§3), ManagerId/PeriodStart/PeriodEnd, histórico append-only (17), permissões (Operador só aceite), migração `20261005011124` com remapeamento InProgress→Active, front (transições/gestor/período/motivo/timeline), testes 35 PDI. -->
- **Fase 2 — Estrutura do desenvolvimento:** objetivos; competências; estratégias; fases; atividades; prazos. <!-- [x] Executada 2026-10-05 — ADR-032: 5 entidades (PdiCompetency catálogo global lowercase 409, PdiObjective/Strategy/Phase/Activity com FK Restrict → 409, sort_order, fase NotStarted/InProgress/Completed + Percent), ownership PdiAccess (Admin tudo / Operador próprio), PdiStructureLookup cadeia→plano, migração `20261006001904_PdiDevelopmentStructure`, controller/VMs GET structure + CRUD 5 níveis, PdiStructureDialog (árvore/forms/catálogo/responsável), testes 11 PDI. -->
- **Fase 3 — Acompanhamento:** check-ins; progresso; comentários; feedback; dificuldades; impedimentos. <!-- [x] Executada 2026-10-06 — ADR-033: 4 entidades sob a fase (PdiProgress progresso+comentário, PdiCheckIn bilateral PUT por lado com null preserva / "" limpa, PdiImpediment status Open→Resolved + ResolvedAt, PdiFeedback histórico excluir=Admin), FK Restrict → 409, permissão por ação em PdiAccess (View=Admin/dono/gestor, Report=Admin/dono, Respond=Admin/gestor, Assist=Admin/dono/gestor, feedback Create=View/Delete=Administer), GET /pdi/{userId}/accompaniment único, PdiAccompanimentDialog (4 abas) pelo botão "Acompanhar", migração `20261006013525_PdiAccompaniment`, testes 9 PDI, E2E 64/64; "comentários" = PdiProgress.Comment; tipo de impedimento livre (lookup = Fase 8). -->
- **Fase 4 — Evidências:** upload; tipos; validação; rejeição; histórico. <!-- [x] Executada 2026-10-07 — ADR-034: PdiEvidence (pdi_evidences sob a fase, 9 tipos enum + rótulos pt-BR, Pending→Validated/Rejected com revisão única 409 e motivo obrigatório na rejeição, arquivo ≤10MB com nome saneado), storage disco var/pdi-evidence (IPdiEvidenceFileStore, arquivo antes do DB com rollback best-effort; volume nomeado + chown no Dockerfile e nginx client_max_body_size 16m — descobertas do E2E), POST /pdi/evidences multipart + PUT review (JsonStringEnumConverter explícito) + GET download (nosniff), 5ª lista no GET acompanhamento, histórico EvidenceAdded/EvidenceReviewed sem migração, PdiAccess EnsureCanReport/EnsureCanReview/EnsureCanView, aba "Evidências" no PdiAccompanimentDialog (upload/revisão/download blob), migração 20261007015235_PdiEvidence, testes 10 PDI, E2E 50/50. -->
- **Fase 5 — Governança:** aprovação; aceite do colaborador; replanejamento; auditoria; histórico de alterações. <!-- [x] Executada 2026-10-07 — ADR-035: §14 motivo obrigatório em mudança de campo + detalhe anterior→novo no evento Updated (status-only sem motivo; ordem EnsureCanMove→RequireStrategy→EnsureValid→motivo); §15.2 PdiPhaseStatus.PendingValidation + SubmitPdiPhaseCommand (dono/Admin) / ValidatePdiPhaseCommand (Admin/gestor, rejeição exige motivo → InProgress, validação → Completed + auto-start via ListPhasesAsync) + guard no PUT (não-Admin não muda status para/nem desde PendingValidation/Completed → 400 "…aprovação…"); §15.1 aceite sem motivo já vigente; §17 +7 tipos PhaseStarted/Submitted/Validated/Rejected, CheckInCreated, ImpedimentCreated/Resolved (sem migração) gravados pelos handlers; IAuditableRequest em 30 comandos PDI (AuditResource "PDI"); rotas POST /pdi/phases/{id}/submit + PUT .../validation (JsonStringEnumConverter); front PdiStructureDialog (Concluir/Validar/Rejeitar + diálogo de motivo, phaseStatusOptions por papel) e PdiPage (fieldsChanged/needsReason/payload reason); testes 11 PDI, suíte unit 1605, E2E 63/63 + regressão Fase 4 50/50. -->
- **Fase 6 — Avaliação:** autoavaliação; avaliação do gestor; resultado; comparação inicial/final. <!-- [x] Executada 2026-10-07 — ADR-036: PdiAssessment (1 linha/lado Self+Manager, upsert, autor, conteúdo ≤4000) + PdiResult único (4 desfechos §16 + nota + finalizador); fluxo §15.3 no backend (autoavaliação → UnderEvaluation com StatusChanged; gestor exige Self; resultado exige Manager e é único; Completed congela); seta nova UnderEvaluation→Replanned (Replanejado) + Completed; EnsureCanEvaluate (Admin/gestor); GET evaluation + PUT assessment/result; histórico AssessmentCreated/ResultFinalized; IAuditableRequest (32 comandos PDI); PdiEvaluationDialog + botão "Avaliar"; migração 20261007233505_PdiEvaluation; testes 15 PDI (unit 1621, arch 117, PDI 166), E2E 31/31 + regressão Fase 5 63/63. -->
- **Fase 7 — Gestão:** dashboard do colaborador; dashboard do gestor; dashboard administrativo.
- **Fase 8 — Analytics:** indicadores; filtros; relatórios (individual, semestral); exportação PDF/Excel.

**Decisão arquitetural (do usuário):** não colocar tudo dentro da entidade PDI. O PDI é o agregado principal, mas evidência, feedback, check-in, histórico e impedimento têm suas próprias responsabilidades e regras. Manter o PDI independente do restante do InfraControl:

```
InfraControl
├── Identity / Access
├── Organization
├── PDI
│   ├── Planning
│   ├── Development
│   ├── Check-ins
│   ├── Evidence
│   ├── Evaluation
│   └── Reports
├── Tasks
├── Knowledge
├── ITSM
├── Inventory
└── Monitoring
```

> Nota (agente, 2026-10-04): a mudança de status para os 9 estados do
> §3 e a separação de sub-agregados exigem ADR + migração de dados (o
> enum atual é string; histórico de valores antigos). **Ciclo de vida
> executado na Fase 1 (ADR-029, migração `20261005011124`)**; a
> separação de sub-agregados (evidência/feedback/check-in…) continua
> exigindo ADR próprio na fase correspondente — ver AGENTS.md.
