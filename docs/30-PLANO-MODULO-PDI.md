# docs/30 — Plano do módulo PDI (Plano de Desenvolvimento Individual)

> **Origem:** documento de produto colado pelo usuário em **2026-10-04** como
> plano oficial do módulo. O texto abaixo preserva o conteúdo do usuário
> (requisito de produto, não inventado pela IA). O **mínimo viável já
> implementado** está no cabeçalho; a evolução segue as **8 fases** da §25,
> **uma task por fase**, com ADR antes de qualquer mudança arquitetural.
>
> ## Situação atual (2026-10-04) — o que já existe
>
> - `IndividualDevelopmentPlan` (1 por usuário): estratégia, prazo,
>   status (InProgress/Completed/Cancelled), ator do change;
> - permissões: Admin cria/edita/lista tudo, Operador edita só o próprio,
>   Viewer sem acesso (rota `operate`);
> - `GET/PUT /api/v1/pdi` + `PdiPage` (`/pdi`);
> - eventos `PdiCreated/Updated` → outbox → RabbitMQ → notificações
>   (criação: dono Email+SignalR; edição: dono ou Admin por role, só
>   SignalR);
> - testes `PdiCommandTests` (7) + suítes verdes; containers/ smoke/ E2E.
>
> **Gap mínimo → plano:** ciclo de vida completo (RASCUNHO…CONCLUÍDO,
> REPLANEJADO, CANCELADO), diagnóstico, período, histórico/timeline,
> gestor como participante — é a **Fase 1** da §25.

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

- **Fase 1 — Fundamento:** revisar entidade PDI atual; pessoa/gestor; período; status; permissões; histórico.
- **Fase 2 — Estrutura do desenvolvimento:** objetivos; competências; estratégias; fases; atividades; prazos.
- **Fase 3 — Acompanhamento:** check-ins; progresso; comentários; feedback; dificuldades; impedimentos.
- **Fase 4 — Evidências:** upload; tipos; validação; rejeição; histórico.
- **Fase 5 — Governança:** aprovação; aceite do colaborador; replanejamento; auditoria; histórico de alterações.
- **Fase 6 — Avaliação:** autoavaliação; avaliação do gestor; resultado; comparação inicial/final.
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

> Nota (agente, 2026-10-04): a mudança de status para os 7 estados do §3 e
> a separação de sub-agregados exigem ADR + migração de dados (o enum atual
> é string; histórico de valores antigos). Nada disso é executado sem task
> autorizada — ver AGENTS.md.
