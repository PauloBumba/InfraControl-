# ADR-059: Contrato JSON de enums (string) e correção dos achados da caça-bugs frontend — lote 1

- **Status**: **aceito** (aprovado pelo usuário 2026-10-17; implementação via F33-T001–T003)
- **Data**: 2026-10-17
- **Contexto**: 14 bugs confirmados pela caça-bugs (`docs/55-RELATORIO-CACA-BUGS-FRONT.md`,
  scan-only de 2026-10-17). Os 3 altos **H1–H3** são o mesmo defeito de raiz
  (contrato de enum front↔back) e exigem decisão arquitetural; os demais têm
  fix local, mas este ADR fixa os padrões para não regredir.

## Contexto

### Raiz dos 400 (H1–H3)

O backend tem **duas convenções vivas** de enum no JSON:

| Padrão | Exemplo | No wire |
|---|---|---|
| **String de ponta a ponta** (maioria) | `IncidentDto(string Status)` via `.ToString()` + `ParseStatus` em `UpdateIncidentStatusCommand` | `"Resolved"` |
| Enum tipado + conversor explícito | PDI: `PdiEvidenceViewModels` com `[JsonStringEnumConverter]` | `"Accepted"` |
| **Enum tipado SEM conversor** (defeito) | `UpdateInvitationViewModel(IReadOnlyCollection<UserRole>)`; `ProjectEvidenceReviewDecision` | número (STJ default) |

O MVC **não registra** `JsonStringEnumConverter` global (verificado em
`DependencyInjection.cs`); cada view model carrega o próprio converter ou nada.
Bancos já persistem enum como **string** (decisão F0/recente: `HasConversion<string>`
em todo enum, incl. migração `20261017015256`). Logo, **o número no wire é a
exceção que quebrou 3 fluxos** (convites→roles, evidências→decisão, knowledge→status:
front envia número, view model espera enum numérico, ou vice-versa).

### Por que não é só "corrigir o front"

Corrigir o front 1-para-1 (H1–H3) deixa a armadilha: o próximo módulo pode
escrever `enum` tipado sem converter e o bug renasce sem teste que o pegue.
A caça-bugs também expôs padrões de UI que se repetem e merecem regra
(loop de render com array novo por render, rota `:id` sem reset, mutation
compartilhada sem chave de resultado, inferir estado administrativo de telemetria).

## Decisão

### 1. Contrato JSON: enum viaja como **string**

- **No wire (request e response), todo enum é string** (`"Admin"`, `"Validated"`,
  case-insensitive no bind). Nunca número. Isso espelha o banco (string) e o
  padrão majoritário já existente (`IncidentDto`, Zabbix, etc.).
- **Como escrever view models a partir de agora** (obrigatório em slices novos):
  - **Preferido**: propriedade `string` + parse explícito com `Enum.TryParse`
    (molde `IncidentCommands.ParseStatus`) ou constante do front — mesmo molde
    dos comandos CQRS já existentes.
  - **Aceito**: propriedade enum tipada **somente** com
    `[JsonStringEnumConverter]` no tipo (molde PDI). Converter é obrigatório;
    enum tipado sem anotação é erro de revisão.
- **Proibido**: registrar `JsonStringEnumConverter` global no MVC
  (`AddJsonOptions`). Motivo: mudaria silenciosamente o wire de todos os
  endpoints existentes (blast radius invisível, quebra clientes numéricos sem
  teste falhar de forma dirigida); explícito por view model mantém cada
  contrato auditável — mesma filosofia do ADR-048/§"contrato explícito".
- **Front**: constantes de enum sempre `string` no tipo (`status: string`,
  `roles: string[]`); enviar número é bug de contrato.

### 2. Fix dos 3 bugs de contrato (H1–H3)

| Bug | Fix decidido |
|---|---|
| H1 roles de convite | `UpdateInvitationViewModel.Roles` vira `IReadOnlyCollection<string>` + parse para `UserRole` no command (molde Incident); o front já manda string. |
| H2 decisão de evidência de projeto | `ProjectEvidenceReviewDecision Decision` ganha `[JsonStringEnumConverter]` (molde PDI irmão); o front já manda `"Validated"`/`"Rejected"`. |
| H3 status de knowledge | `ChangeKnowledgeArticleStatusViewModel.Status` **continua string**; o **front** deixa de mandar número (`Number(newStatus)` → string) e o comentário errado em `knowledge.ts:200-210` é corrigido. |

Critério de aceite comum: teste de contrato por bug (view model bind de string
→ command; e teste de serviço do front) + regra de revisão "enum tipado sem
converter = request changes".

### 3. Padrões anti-regressão (regra para slices novos e para os fixes)

| Defeito observado | Regra |
|---|---|
| **H4** — array novo por render em dep de `useEffect` (`?? []`) | Deps de efeito nunca recebem array/objeto literal derivado por render; usar `useMemo` com dependências estáveis ou depender de `data` bruto. |
| **H5** — rota `/tasks/:id` sem reset no troca de id | Elemento de rota parametrizado usa `key={id}` **ou** o loader zera estado (`setLoading(true)`, limpa entidades) antes de buscar; mutação destrutiva sempre opera sobre a entidade **recém-carregada** do id atual, não de `state` antigo. |
| **H7** — mutation compartilhada escrevendo resultado sem checar origem | Resultado de mutation assíncrona só é aplicado se o id/argumento for o do painel atual (guard no `onSuccess`). |
| **H6** — inferir estado administrativo de telemetria | Porta UniFi: **`enabled` é o estado administrativo**; `linkUp`/`link` é telemetria e **nunca** fonte de escrita. Botão usa `disabled: !port.enabled` e label por `enabled`. `linkUp === null` não desabilita o botão. |

### 4. Fora deste ADR (não decidido aqui)

- **M6** (toggle Modo Escuro persiste e não aplica tema): esconder o toggle
  **ou** implementar theming → exige ADR próprio de UI theme se for implementar.
  Fix mínimo permitido sem ADR: **ocultar o controle** (função morta).
- **M5** (Zabbix "testar conexão" testa config salva): tornar o endpoint aceitar
  payload opcional de teste é mudança de API → nova task + revisão de segurança
  (credenciais não logadas); fora do lote 1.
- Os 34 `manual-review` do relatório: revisar antes de fix em lote (nenhum é
  bloqueante de contrato).

## Consequências

- **Positivas**: um só formato de enum em todo o sistema (JSON string ≡ banco
  string ≡ DTO string); fim dos 400 de contrato nos 3 fluxos; regras baratas
  que evitam reincidência (H4–H7) sem framework novo.
- **Negativas/restrições**: quem escrever enum tipado precisa lembrar do
  conversor (guardado por teste de revisão + checklist doc/54); converter global
  continua proibido, então cada contrato novo carrega 1 anotação.

## Alternativas rejeitadas

1. **Converter global no MVC**: conveniente, mas breaking change silencioso de
   todos os contratos existentes; rejeitado (ver §1).
2. **Corrigir só o front (H1–H3) sem regra**: resolve o sintoma, mantém a
   armadilha; rejeitado.
3. **Voltar enum numérico no banco**: rejeitado — decisão atual do domínio é
   string (`HasConversion<string>`), e número no wire dobra o custo de
   manutenção (duas representações).

## Segurança

- H6 é o único achado com **escrita em equipamento real** (habilitar/desabilitar
  porta errada); entra no lote 1 de fix por prioridade de risco, não só por
  severidade UX.
- Parse de enum de entrada mantém comportamento de rejeição atual (valor
  desconhecido → 400/validação), sem abrir aceitação silenciosa.

## Testes exigidos para aprovar o lote

- H1: teste de bind `UpdateInvitationViewModel` com `["Admin","Viewer"]` →
  command com `UserRole[]`; e rejeição de valor inválido.
- H2: bind de `"Validated"`/`"Rejected"` em `ReviewProjectEvidenceViewModel`.
- H3: teste de serviço front enviando string; regressão do molde Incident.
- H4: teste de render de `DevicesPage` com query pendente (sem loop —
  contagem de efeito limitada).
- H5: teste de troca de rota pai→subtarefa (tela não mantém task anterior;
  excluir opera sobre o id da URL).
- H6: teste de `SwitchesPage` com `linkUp: null` → botão **não** envia
  `disabled: true`.
- H7: investigação de A após B não sobrescreve painel de B (mock com resposta
  atrasada).

## Referências

- `docs/55-RELATORIO-CACA-BUGS-FRONT.md` (14 confirmados, triggers)
- `docs/54-CHECKLIST-CACA-BUGS.md` (§2 command, §5 mensageria)
- ADR-048 (camada de integração/contrato explícito), ADR-051 (sobrepõe
  restrição com salvaguardas — mesmo formato deste documento)
- Molde Incident: `IncidentDto.cs`, `IncidentCommands.ParseStatus`
- Molde PDI: `PdiEvidenceViewModels.cs` (`[JsonStringEnumConverter]`)
- Migração de enum string: `20261017015256_PersistInvitationEnumsAsStrings`
