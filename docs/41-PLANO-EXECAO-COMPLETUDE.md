# 41. Plano de Execução — CRUD, UX e Completude de Plataforma

> **Origem**: pedido do usuário em 2026-10-11, transcrita abaixo sem edição.
> **Natureza**: este documento é o **contrato de execução**. Cada fase vira
> tasks no `TASKS.md` com objetivo, escopo, critérios, testes e evidências
> (AGENTS.md — "dividir para conquistar"). Nada é implementado sem a fase
> anterior fechada ou uma decisão explícita do usuário.
> **Status**: **Fase 0 em execução**.

## 0. O que foi pedido (transcrição literal)

> "todas as paginas tem que ter crud no front e backned, as paginas do front
> estão com uma complexidade linear e vão crescer, usar vertical slice,
> resolver o conflito da sequencia na dropdown no sidebar, add pagina para
> usuario alterar senha dentro da plataforma ou seja um profile, implementar
> o nome completo na entidade user, a pagina invite não está funcionando, ver
> a responsividade, pensar em coisas que sejam reutilizáveis tanto no front
> quanto no back, habilitar o login social apenas se usuario estiver ativo,
> verificar a mensagem de erro no que o front tem que ver para o usuario,
> melhorar o layout da topologia e o index/dashboard tem que ser igual UniFi.
> Faz um plano de execução, tudo que eu falei tem uma coisa a ser corrigida."

## 1. Baseline verificado (2026-10-11)

Medido antes de qualquer alteração — é o parâmetro de comparação de todas as
fases.

| Área | Estado | Evidência |
| --- | --- | --- |
| Front — tipos | ✅ verde | `tsc -b` exit 0 |
| Front — testes | ✅ verde | **74 testes em 17 arquivos**, `vitest run` |
| Back — build | ✅ **verde desde a Fase 0** | `dotnet build --nologo` → **0 erros** (antes: 5) |
| Back — unit | ✅ verde | **2157 aprovados · 0 falhas** (`InfraControl.UnitTests`) |
| Back — arch | ⚠️ **1 falha pré-existente** | **122 aprovados · 1 falha** (`EndpointAlignmentTests.Nginx_Should_Proxy_The_Api_Health_Checks`) — divergência da outra sessão, ver §4 |
| Back — integração | ⛔ não executável | precisa Docker/PostgreSQL |
| Back — testes | ✅ executáveis **desde hoje** | antes: nenhum SDK, suíte impossível |
| Git | ⚠️ **merge incompleto** | `MERGE_HEAD` = `9d1e759`; `frontend/InfraControl.Web/tsconfig.tsbuildinfo` em `UU` |
| SDK .NET | ✅ instalado hoje | `10.0.401` (antes: **nenhum** SDK — só runtimes 8/9) |
| Responsividade | ❌ praticamente inexistente | **3 media queries** em todo o CSS (1024/768/720px); PrimeFlex instalado e **não usado** (0 ocorrências de `formgrid`/`col-*`); **nenhuma** lógica de drawer/hambúrguer no `AppLayout` |

### 1.1 Causa raiz do build quebrado (achado da Fase 0)

Duas coisas se combinaram:

1. **Bug no `.gitignore`** — a linha 229 tem `health/`, regra herdada do
   template do Visual Studio ("BeatPulse healthance liveness check"). Ela
   ignora **qualquer** diretório chamado `health/` no repositório, inclusive
   `src/InfraControl.Application/AI/Health/`.
2. **Consequência invisível** — o arquivo `AiRuntimeHealth.cs` nesse
   diretório é **código-fonte invisível para o git**: não está no `HEAD`, não
   aparece no `git status`, nunca foi commitado. Por isso a colisão nunca foi
   vista por ninguém.

O arquivo invisível declara `AiComponentHealth`; o código **commitado**
(`AiRuntimeHealthService.cs` + `IAiRuntimeHealth.cs`) declara e usa
`AiComponentReport`. **Nada no repositório referencia `AiComponentHealth`**
além do próprio arquivo — é um rascunho órfão.

> **Regra derivada**: qualquer regra de `.gitignore` precisa ser **ancorada**
> ou restrita ao diretório de saída pretendido. Uma regra genérica tipo
> `health/` ou `logs/` pode silenciar código-fonte sem que o `git status`
> mostre alguma coisa errada. Ver Fase 0, critério 3.

## 2. Princípios de execução

1. **Nada de requisito inventado.** Onde o pedido for ambíguo, a fase abre
   com uma decisão explícita do usuário — como já aconteceu nas frentes
   anteriores (rotas da landing, Contato sem formulário, seletor por seção).
2. **Vertical slice por entrega.** A cada feature: `slice → teste RED →
   código → verde → docs → commit`. Uma vertical slice por caminho de
   valor, não uma camada inteira de cada vez.
3. **`BaseCommand`/`BaseQuery` obrigatório** em tudo que for novo no back
   (`docs/24-BUILDING-BLOCKS.md`), e `EntityView`/`ViewSwitcher` como base de
   lista no front.
4. **Backend é autoridade.** O front espelha regra; nunca reimplementa
   autorização.
5. **Reutilizável por padrão.** Se algo aparecer pela segunda vez, sobe para
   bloco compartilhado antes de virar a terceira cópia.
6. **Não esconder teste quebrado.** Se a fase não puder ser validada por
   ambiente, fica marcada com ⚠️ e a limitação registrada (§46).

## 3. As fases

A ordem é por **dependência**, não por importância. A Fase 0 é bloqueante de
tudo que for backend; a Fase 1 molda como as 6, 7 e 8 serão construídas.

| # | Fase | Bloqueia / é bloqueada por |
| --- | --- | --- |
| 0 | Destravar o build | bloqueia **tudo** de backend |
| 1 | Blocos reutilizáveis (front + back) | bloqueia 6, 7, 8 |
| 2 | User: nome completo + perfil | precisa de 0 |
| 3 | Login social só com usuário ativo | precisa de 0 (e de 2, se a flag morar em User) |
| 4 | Convites: diagnóstico e correção | precisa de 0 |
| 5 | Mensagens de erro padronizadas no front | independente |
| 6 | Sidebar: conflito de sequência na dropdown | independente |
| 7 | CRUD completo em todas as páginas | precisa de 1 |
| 8 | Responsividade | precisa de 1 e 7 estabilizados |
| 9 | Dashboard e Topologia estilo UniFi | última |

---

## Fase 0 — Destravar o build

**Objetivo**: o backend compila novamente, com a causa raiz corrigida e
registrada — sem tocar no merge de outra sessão.

**Escopo**
- Corrigir a regra do `.gitignore` que ignora código-fonte.
- Retirar o rascunho órfão do caminho da compilação **preservando o conteúdo**
  (nenhum apagamento).
- Confirmar o baseline de build/teste do backend.

**Fora de escopo** (pertence a outra sessão, não é meu para decidir):
- Resolver o merge de `9d1e759`.
- Resolver o conflito `UU` do `tsconfig.tsbuildinfo`.
- Decidir o destino definitivo do `AiRuntimeHealth.cs`.

**Critérios de aceite**
1. `dotnet build` com **0 erros**.
2. Nenhum arquivo de `src/` ignorado por regra do `.gitignore`
   (verificação explícita, não só "o build passou").
3. O conteúdo do rascunho órfão continua existindo no disco.
4. `git status` não mostra nenhum arquivo de `src/` como `??` escondido.

**Testes e evidências**
- `dotnet build --nologo` → exit 0, erros = 0.
- `dotnet test` unit + arch → contagem registrada como baseline.
- Comando de verificação de ignorados em `src/` → vazio.

**Decisão do usuário (2026-10-11)**: autorizou abrir a Fase 0 depois de eu
apontar o bloqueio e as três opções (corrigir + preservar / corrigir +
apagar / aguardar o merge). Adotado **corrigir + preservar**.

---

## Fase 1 — Blocos reutilizáveis (front + back)

**Objetivo**: criar, uma vez, o que as fases 6, 7 e 8 vão usar N vezes.

**Motivação explícita do pedido**: "as páginas do front estão com uma
complexidade linear e vão crescer, usar vertical slice" + "pensar em coisas
que sejam reutilizáveis tanto no front quanto no back".

**Divisão (decisão do usuário, 2026-10-11)**: a task virou **T001A (back)**
e **T001B (front)** — cada metade passa de 2.000 linhas de duplicação sozinha
e um diff único não seria revisável.

### 1A — BACK: paginação e busca compartilhadas ✅

**Levantamento**: o único contrato de paginação era `PagedResult<T>`, e morava
dentro do arquivo de `IAuditReader.cs`. Não existia tipo de parâmetro —
`int Page = 1, int PageSize = 20` aparece **59 vezes em 27 arquivos** —; 28
handlers repetiam `Math.Max(1, request.Page)` + `Math.Clamp(request.PageSize,
1, 100)`; o term-normalizer idêntico aparecia **14 vezes**; e o corpo de
contar/saltar/pegar estava em ~25 repositórios.

**Decisões confirmadas com o usuário**: criar os blocos e adotar em novos
slices + **um piloto** (migração dos 27 restantes = task seguinte); contrato em
`Application/Common`, **BuildingBlocks intocado**.

**Blocos criados**

| Bloco | Onde | Substitui |
| --- | --- | --- |
| `PagedResult<T>` (arquivo próprio) | `Common/Interfaces/PagedResult.cs` | o record que vivia dentro de `IAuditReader.cs` |
| `IPagedRequest` + `Paging` (`DefaultPageSize` 20, `MaxPageSize` 100, `Normalize`) | `Common/Interfaces/PagedRequest.cs` | as duas linhas de clamp em 28 handlers |
| `TermNormalizer` | `Common/Interfaces/TermNormalizer.cs` | o ternário de termo em 14 handlers |
| `EfSearch.PageAsync` | `Infrastructure/Repositories/EfSearch.cs` | contar/saltar/pegar em ~25 repositórios |

**Duas decisões que evitaram quebrar a API**: `IPagedRequest` é **interface**,
não record com objeto embutido — as queries declaram `Page`/`PageSize` como
propriedades próprias e o ASP.NET liga direto da query string (`?page=1`), então
um `Paging { get; }` aninhado mudaria o formato da URL e quebraria o front; e os
limites (20/100) são **os mesmos que as 28 queries já aplicavam**, porque mudar
teto de página é mudar resposta da API.

`PagedResult<T>` mudou de arquivo **sem mudar de namespace**, de propósito: 25
portas e 29 queries o usam, e arrumar endereço seriam 25 edições sem ganho.

**Piloto = Racks** (o slice mais idêntico ao resto — Location/Vlan/Connection
repetem o mesmo esqueleto). Comportamento que **mudou de propósito** e é
exatamente o teste que faz RED: o repositório agora saneia a página sozinho.
Antes ele acreditava em quem chamasse — `pageSize: 0` virava `Take(0)` (lista
vazia, sem erro nenhum) e `page: 0` virava `Skip(-pageSize)`.

**Não feito de propósito**: helper de "não encontrado" (227 ocorrências). As
mensagens variam por entidade e um helper com texto fixo mudaria string visível
ao usuário — isso é decisão de produto, não de refatoração.

**TDD**: RED por compilação (CS0246) → GREEN 25/25. **Evidências**: unit
**2163 → 2188** (0 falhas) · `dotnet build` 0 erros · arch 122 + 1 (falha
pré-existente do ADR-044, inalterada).

### 1B — FRONT: casca de índice e formulário ✅

**Levantamento**: o kit **já existe** em `components/ui/` (20 arquivos) —
`PageHeader` em 32 páginas, `ErrorBanner` em 31, `DataTablePanel` em 18,
`Dialog` próprio em 11, `Pagination` em 5, `SearchInput` em 9, `usePagedQuery`
em `hooks/queries.ts`. O que falta é a **casca** que une tudo.

Concentrações (detalhadas em `TASKS.md`): nada une
cabeçalho+busca+filtro+erro+vazio+paginação (não existe `IndexPage`; o raiz
`flex column gap:16` aparece **33× em 32 páginas**); validação campo-a-campo
com `formError` em **10 páginas/85 ocorrências**; e a camada de dados manual
sobrevive em **14 páginas**, com `messageFor()` reimplementado **13×** e
`PAGE_SIZE = 20` redeclarado **8×**.

**Decisão do usuário**: criar os blocos e adotar num **piloto de 3–4 páginas**;
migração ampla em task seguinte.

**Resultado (concluída 2026-10-11)** — 4 blocos, 4 páginas-piloto.
Suíte front **89 → 118 testes** (22 → 28 arquivos, 0 falhas), `tsc 0,
eslint 0, build OK`.

| Bloco | Onde | O que substitui |
| --- | --- | --- |
| `ListPageShell` + `countLabel` | `components/ui/` | raiz `flex column gap:16` + `PageHeader` de contagem + `ErrorBanner` de retry |
| `useServerSearch` | `hooks/` | `termInput × term × page` em 5 páginas |
| `errorMessage(error, fallback?)` | `hooks/queries.ts` | 16 cópias de `messageFor` |
| `useFormErrors` | `hooks/` | `formError` em 12 páginas/dialogs |

**Três decisões de desenho que evitaram trocar uma duplicação por outra coisa:**

1. **A casca não conhece a tabela.** O conteúdo entra por `children`, então
   serve o `DataTable` do PrimeReact (Clients, Incidents) e a lista própria da
   AlertsPage. Acoplar casca a tabela seria criar um casamento que a próxima
   página não casa — e as 32 páginas casam de formas diferentes.
2. **Slot `notice` explícito.** A ZabbixHostsPage mostrava um `NoticeBanner`
   **acima** do `ErrorBanner`; sem esse slot a casca reordenaria a página e a
   mudança passaria despercebida. Um slot vale mais que uma ordem implícita.
3. **O plural nunca é inferido.** `countLabel(total, "cliente", "clientes")`
   exige os dois: `"usuário" + "s"` = `"usuárioss"`, e uma regra de bolso
   quebraria silenciosamente metade das listas do produto.

**Pilotos**: ClientsPage, AlertsPage, ZabbixHostsPage (casca) + IncidentsPage
(casca + formulário). AlertsPage e IncidentsPage **não tinham nenhum teste**
e receberam teste de caracterização; ClientsPage e ZabbixHostsPage tinham, e
as duas suites antigas passaram **sem alteração nenhuma** — essa é a rede de
segurança real de uma migração.

**Comportamento que mudou de propósito**: no formulário de incidente, a
mensagem de validação saía **2×** (embaixo do campo Título **e** no
`FormError` do rodapé) e agora sai **1×**, no campo que falhou. O `useFormErrors`
é quem faz a associação — antes ela era fixa, então uma falha na *descrição*
saía escrita embaixo do *título*.

**Não feito de propósito** (registrado, não esquecido):
- migração das demais páginas de índice e das 11 com `formError` → task seguinte;
- o botão `"Salvando…" ? : "Salvar"` (**18 ocorrências em 13 arquivos**) foi
  medido e **não** virou bloco: o markup em volta varia (`.uni-login-submit`,
  `Button` do kit, `div` própria) e padronizá-lo mudaria a aparência de 13
  páginas de uma vez — isso é decisão de UI, não de refatoração.

### 1C — FRONT: migração dos blocos (T001C) ⏳

T001C é grande demais para uma task só — são 3 superfícies de risco muito
diferentes. Fatiada por decisão do usuário:

| Slice | Superfície | Risco | Status |
| --- | --- | --- | --- |
| **C-1** `messageFor` → `errorMessage` | 16 declarações (15 cópias + 1 exportação) | baixo | ✅ 2026-10-11 |
| **C-2** `formError` → `useFormErrors` | 11 sites (9 páginas + 2 dialogs) | médio | ✅ **11/11 concluído** |
| **C-3** casca → `ListPageShell` | 26 arquivos com a raiz `flex column gap:16` | alto | ✅ Lote 1 (7/7) + Lote 2 (6/7) · `PdiPage` fora de propósito |

**Resultado do C-1**: 15 cópias migraram para `errorMessage`. A exportação de
`taskCatalogs.ts` morreu junto — o único consumidor era `TaskDetailPage`, que
passou a importar `errorMessage`. Migração por script com backup prévio dos 16
arquivos, porque alguns têm mudanças da outra sessão no índice do merge e
`git checkout` não desfaria um erro meu.

**Achado que justificou o recorte**: o `messageFor` do `LoginPage` **não é
cópia**. Ele manda `401` para o fallback genérico (para o login não revelar se
a conta existe) e deixa `403` mostrar a mensagem do servidor (conta inativa).
Migrá-lo trocaria o que o login revela — comportamento de segurança, não
refatoração. Ficou de fora, e o comentário do `errorMessage` registra por quê.

**Achado de correção**: `KnowledgePage` devolvia `error.message` sem checar se
era vazia, então `new Error("")` virava mensagem em branco na tela.
`errorMessage` devolve o `fallback`.

**C-2 CONCLUÍDO (11/11) — `AuditPage`, `NetworkPage`, `InvitationsPage`,
`LocationsPage`, `SlaPage`, `DevicesPage`, `TasksPage`, `PdiPage`,
`KnowledgePage`, `PdiAccompanimentDialog`, `PdiStructureDialog`**, com teste por página. O padrão é `const { error:
formError, field, fail: failForm, reset: resetFormError, errorFor } =
useFormErrors()`, com `failForm(msg, "campo")` em cada validação, `error=
{errorFor("x")}` no `FormField` e `<FormError message={field ? "" :
(formError ?? "")} />` — o campo acende sozinho e a recusa do servidor (que
vem sem `field`) continua no banner do formulário.

**A regra que a migração estabeleceu: decidir campo a campo, não migrar em
bloco.** Um erro vai para o campo quando a mensagem descreve UM campo; vai
para o formulário quando descreve mais de um, quando é pré-condição de dado ou
quando vem do servidor. Os onze casos registrados no código:

| Site | Vai para o campo | Fica no formulário |
| --- | --- | --- |
| `NetworkPage` | nome, número da VLAN | recusa do servidor |
| `AuditPage` | — | "De" posterior a "Até" (2 campos) |
| `InvitationsPage` | — | grupo de checkboxes sem `FormField` |
| `LocationsPage` | nome | "Cadastre um local antes de criar racks" (pré-condição), recusa do servidor |
| `SlaPage` | nome, serviço do SLO, alvo, janela | "Resposta **e** resolução" (2 campos, a frase não diz qual), recusa do servidor |
| `DevicesPage` | nome, tipo, localização (3 validações, 3 campos) | recusa do servidor |
| `TasksPage` | título (única validação) | recusa do servidor (criação **e** edição) |
| `PdiPage` | usuário, prazo, motivo | "O fim do período não pode ser anterior ao início" (2 campos), recusa do servidor |
| `KnowledgePage` | criação: título, conteúdo · edição: título, conteúdo, motivo · categoria: nome | recusa do servidor (nos 3 forms) |
| `PdiAccompanimentDialog` | as 15 validações, uma por campo, nos 6 modos (progresso, check-in, impedimento, feedback, evidência, revisão) | recusa do servidor |
| `PdiStructureDialog` | título, nome da competência, percentual, motivo da rejeição | "O fim da fase não pode ser anterior ao início" (2 campos), recusa do servidor (nos 3 forms) |

**Oito achados que a migração forçou**: (1) `mutationErrorMessage` devolve
`string | null` e o `fail` do hook exige `string` — o `catch` migrou para
`errorMessage`, que aceita `unknown` (que é o que sai de um `catch`) e sempre
devolve texto; (2) em `SlaPage` a validação de nome vem **antes** de todas as
outras e retorna cedo, então qualquer teste das validações seguintes precisa
preencher o Nome primeiro — senão a de nome dispara e a de alvo/serviço nunca
é alcançada.

(3) **`DevicesPage`**: era a única do recorte **sem um sequer `FormField`** — 7
controles eram `input`/`SelectField` com `placeholder` dentro de um `grid`, e o
erro era um `<div>` solto no pé do form. Três validações de três campos
diferentes saíam todas no mesmo lugar. Ligar erro a campo exigiu **criar a
estrutura de campo**, não só trocar o estado: os 7 controles ganharam
`FormField` (labels visíveis — os inputs ganharam `width: 100%` porque agora
vivem dentro de um `<label>` flex, e os dois `SelectField` receberam
`style={{ width: "100%" }}`, mesmo procedimento da `SlaPage`). Os campos sem
validação nenhuma (IP, Modelo, Fabricante, Descrição) ficam com `error={null}`
documentando que nada os alavanca. O teste é o único que acessa os campos por
`role` (`textbox`/`combobox`) em vez de label — `getByLabelText` exige o
`<label>` que a migração ainda ia criar, e o teste precisa rodar vermelho antes
e verde depois com os mesmos seletores.

(4) **`TasksPage` tinha o bug que o C-2 existe para corrigir.** O `FormField`
do Título já recebia erro, mas com a mensagem **hardcodada sobre o estado
errado**: `error={formError ? "Informe o título da tarefa." : null}`. Qualquer
`formError` — inclusive uma recusa do servidor — fazia o campo Título acender
com "Informe o título da tarefa." (mentindo: o título estava preenchido) e a
mensagem real repetia no `<FormError>` do pé. O vermelho do teste foi
literalmente `Found multiple elements with the text: Informe o título da
tarefa.` — a duplicação existia. **Segunda metade do mesmo achado**: as
mutations de criação e de identificação tinham `onError → setActionError` ao
mesmo tempo que o `catch` de `submitForm` punha a mesma frase no `FormError` —
a mesma recusa aparecia **duas vezes na mesma tela** (banner de página + form).
As duas foram removidas; `stateMutation` e `deleteMutation` mantêm o banner
porque não passam por um form. `DevicesPage` já não tinha esse duplo.

(5) **`PdiPage` é o único site em que uma validação não pode ir para campo
nenhum, e o teste mostrou por quê.** A página tem 4 validações de 4 destinos
diferentes: usuário → campo, prazo → campo, motivo → campo, e **"O fim do
período não pode ser anterior ao início" → formulário**, porque a frase descreve
DOIS campos — pendurar em "Início" ou em "Fim" apontaria metade do problema
(mesma regra do "De posterior a Até" da AuditPage). Dois achados de teste:
(a) o motivo **só existe na edição** — `needsReason` depende de `original`, então
na criação o campo nem é renderizado e o teste dele precisa abrir o plano pela
tabela; (b) o helper que localiza o campo **não pode usar `getByLabelText`** —
quando o erro acende, a mensagem entra dentro do mesmo `<label>` e o
`textContent` deixa de ser "Prazo" (vira "PrazoInforme o prazo do PDI."), então
o match exato do RTL não acha mais nada; o teste casa pelo **texto inicial do
label**, escopado no `<form>`, e o seletor é o mesmo antes e depois da
migração. Achado de debugging: o vermelho dizia `Unable to find an element with
the text` e a depuração mostrou `formErrorNoDom=true` — a mensagem existia, só
estava no pé do form em vez do campo, que era exatamente o que o teste vinha
provar.

(6) **`KnowledgePage` foi o primeiro site com MAIS DE UM form** — e é por isso
que ela não cabe num único `useFormErrors`. A página tem 3 forms abertos ao
mesmo tempo em telas diferentes (criação inline, dialog de edição, dialog de
categoria), e um estado compartilhado apagaria o erro de um ao abrir o outro:
foram **3 instâncias** do hook, uma por form (`formError`/`editError`/
`categoryError` viraram os retornos deles). As 3 validações da **edição** —
título, conteúdo, motivo — eram o caso mais nítido: três campos diferentes
caíam no mesmo `<FormError>` sem dizer qual era. Dois achados de desenho:
(a) o form de criação **não tinha estrutura de campo nenhum** (7 inputs com
`placeholder`, como a `DevicesPage`) e os dialogs usavam `<label>` cru — o
próprio wrapper que `FormField` existe para substituir —, então a migração
criou/unificou as 17 estruturas de campo da página; (b) `statusError` ficou **de
propósito** com `useState`: ele só recebe recusa do servidor, que já vive no
nível do formulário, e migrá-lo seria trocar um estado por um hook que devolve
sempre `field = null`, sem nenhuma mudança de comportamento. Trade-off
registrado: o label âmbar do "Motivo da alteração" (`#b45309`) foi
substituído pelo label padrão do `FormField` em troca da estrutura de erro — o
`*` e a borda âmbar do input continuam marcando o campo.

(7) **`PdiAccompanimentDialog` é o site de maior densidade — 15 validações por
6 modos de form — e o de maior risco de chaves trocadas.** Os modos **reusam
os mesmos nomes de estado**: `form.date` serve para check-in, impedimento E
feedback; `form.type` para impedimento, feedback E evidência;
`form.description` para impedimento E revisão. As chaves do `failForm` são as
do próprio estado (`date`, `type`, `description`) e isso é seguro por três
razões que valem documentar: os blocos de render são mutuamente exclusivos
(`{form.kind === "X" && …}`), `{form && (…)}` esconde a lista enquanto o form
está aberto, e o `kind` só é definido nos `open*Form` — que também zeram o
erro —, então nunca há dois campos com a mesma chave na tela. **Achado de
mapeamento**: "O arquivo excede o tamanho máximo de 10 MB" vai para o campo do
**arquivo**, não do tipo — a validação de tipo vem 3 linhas antes e uma
validação pendurada em "qualquer erro" faria o usuário acreditar que o tipo é
o problema. A migração já tinha tudo o que precisava: a página era a **única
do recorte que já usava `FormField` em todos os 26 controles**; só faltou o
`error={errorFor(…)}` nos 14 campos que têm validação.

(8) **`PdiStructureDialog` fechou a série com 3 estados de erro — os 3 forms
da árvore — e expôs um defeito que a migração não causou.** `formError`
(nó da árvore), `catalogError` (catálogo) e `rejectError` (rejeição de fase)
viraram 3 `useFormErrors()`, pelo mesmo motivo da `KnowledgePage`: os forms
podem ficar abertos em telas diferentes e um estado compartilhado apagaria o
erro de um ao abrir o outro. Duas decisões de nível: (a) **"O fim da fase não
pode ser anterior ao início" fica no formulário** — descreve DOIS campos
(Início e Fim), que ficam **sem `error` de propósito**, com comentário no
código; (b) **"O título é obrigatório" precisa escolher o campo** — o form de
competência valida `form.name` e os demais `form.title`, então a chave é
`form.kind === "competency" ? "name" : "title"` (só um dos dois renderiza por
vez). **Achado de TDD (o 8º)**: `submitCatalog` começa com
`if (!catalogForm) return;` e o estado nasce `null` — com o diálogo recém-aberto,
**"Salvar" não chega na validação e não mostra nenhum aviso**: clique após
clique, silêncio. A validação só é alcançável depois que o usuário toca num
campo (que seta `catalogForm`). **Não corrigi** — é mudança de comportamento
além do escopo do C-2, sob controle humano; o teste acessa o caminho real
(digita e apaga o nome). Recomendação: `submitCatalog` deve validar o nome
antes do guarda, ou o guarda deve ser removido.

**C-2 concluído — 11/11.** Restam do §1C: `F29-T001C-3` (migrar a casca para
`ListPageShell`, recorte = "quem tem `PageHeader` com subtítulo de contagem").

**C-3 não é "26 páginas"**: a raiz `flex column gap:16` é o *layout de página*
do produto — `AccountPage`, `SettingsPage`, `LogsPage`, `HealthPage` e
`DashboardPage` a usam como layout, não como lista de índice. O recorte real é
"quem tem `PageHeader` com subtítulo de contagem". Migração cega dessas 5
estragaria a página delas.

---

## C-3 — Lote 1 concluído (7/7)

Migrações com o texto do cabeçalho **idêntico** ao de produção; cada uma com
teste que tranca a contagem.

| Página | Testes | Encaixe |
| --- | --- | --- |
| `KnowledgePage` | 13 → 15 | contagem pura |
| `DevicesPage` | 4 → 6 | contagem + nota `·` condicional |
| `SlaPage` | 4 → 6 | contagem `·` nota `·` nota |
| `NetworkPage` | 3 → 6 | contagem + sufixo no singular/plural |
| `UsersPage` | novo (2) | contagem `·` nota literal |
| `SwitchesPage` | novo (2) | contagem sem regra de plural |
| `RemotePage` | novo (2) | contagem `·` nota literal |

Suíte do front: **195/195 em 43 arquivos** (era 177/177 em 39), `tsc` 0,
`eslint` 0.

**Correção que mudou o recorte.** O separador do `ListPageShell` é `·`
(`ListPageShell.tsx:67`), não `–`. Eu tinha classificado `SessionsPage`,
`RemotePage`, `UsersPage`, `SwitchesPage` e `NetworkPage` como "exigem decisão"
por causa de um caractere que só existia na saída distorcida do console do
PowerShell. Todos, menos `SessionsPage`, voltaram para o lote limpo.

**Dois erros de gramática preservados de propósito.** `NetworkPage` diz
"1 VLAN cadastradas" e `SwitchesPage` diz "1 switches". Corrigir muda texto
visível, então os dois entraram com o singular já no plural
(`singular="switches"`) e com um comentário no JSX explicando por quê — e há
um teste em cada página trançando o texto errado. **Corrigir exige autorização.**

**Lote 2 — 7 páginas, cada uma com um motivo concreto:**

| Página | Motivo |
| --- | --- |
| `AuditPage` | dois `ErrorBanner` (`formError` sem retry + `error` com retry); o shell aceita um |
| `WiFiPage` | idem: `wifiError` e `error` separados, com retries distintos |
| `LocationsPage` | segunda linha estilizada no subtítulo (`display:block`); o `note` do shell é texto na mesma linha |
| `TasksPage` | dois sufixos condicionais com separadores diferentes (`—` e `·`); o shell tem um `note` só |
| `InvitationsPage` | ` de 10` sem separador; o shell sempre põe `·` |
| `PdiPage` | o ramo não-admin não tem contagem no subtítulo | **fora** — a casca sempre imprime `N <plural>`; migrar obrigaria a `"0 planos · Meu Plano…"` |
| `SessionsPage` | banner na cor legada `#dc2626` (o default do shell é `#b91c1c`) |

**Fora do recorte**: `NotificationsPage` (o ramo "3 de 5 canais ativos" não é
contagem de coleção) e `LoginPage` (401/403 — mesma razão registrada no C-1).

**Única mudança de posição**: em `SlaPage` as abas passaram a vir depois dos
banners, porque a ordem padronizada do shell é cabeçalho → notice → erro →
conteúdo. O texto do subtítulo é idêntico; só a posição do banner mudou.

## C-3 — Lote 2 concluído (6 de 7)

Executado em 2026-10-14, com TDD (teste escrito antes, RED confirmado,
GREEN em seguida) e com a decisão do usuário sobre o dropdown do sidebar já
aplicada no mesmo dia. **6 páginas migradas, 1 ficou de fora de propósito.**

| Página | O que mudou | Testes |
| --- | --- | --- |
| `SessionsPage` | só a cor do banner (`#dc2626` → default `#b91c1c`); contagem + nota já casavam | 3 → 5 |
| `InvitationsPage` | ` de 10` virou `1 convite · de 2`; dois `ErrorBanner`/`InlineMessages` entraram na ordem do shell | 3 → 4 |
| `TasksPage` | o travessão `" — exibindo as N primeiras"` virou `·`; nota passou a ser `exibindo… · filtros ativos` no `note` | 3 → 6 |
| `AuditPage` | **arquivo de teste novo**; os dois `ErrorBanner` adjacentes fundidos num só (`formError ?? error`) | 0 → 3 |
| `WiFiPage` | o subtítulo de sondagem ao vivo virou `contagem · nota` byte a byte; os dois banners fundidos | 6 → 8 |
| `LocationsPage` | a 2ª linha `"N registro(s) nesta aba"` saiu — ela repetia o mesmo número da 1ª linha; virou `contagem · nota` | 3 → 5 |

**`PdiPage` ficou de fora, e é decisão registrada.** O subtítulo dela tem dois
ramos: admin vê `"3 planos · Plano de Desenvolvimento Individual"`, e o usuário
comum vê **só** `"Meu Plano de Desenvolvimento Individual"`, sem contagem — não
é esquecimento, é o desenho (quem tem um plano só não está lendo uma lista). A
casca **sempre** imprime `N <plural>`, então migrar obrigaria a mostrar
`"0 planos · Meu Plano…"` na tela de todo mundo. Forçar isso seria trocar um
texto verdadeiro por um número inventado. Fica com `PageHeader` até existir um
caso legítimo de contagem para o não-admin.

**Duas decisões tomadas durante a execução, ambas registradas nos testes:**

1. **Erro fundido = segunda mensagem descartada.** A casca tem UM slot de
   `error`. Em `AuditPage` e `WiFiPage` havia duas fontes de erro com retries
   distintos; fundir significa que só uma mensagem aparece (a da fonte da
   sondagem/formulário, na mesma ordem `a ?? b` de sempre). O teste tranca
   isso explicitamente — inclusive que a segunda **não** aparece. Se um dia
   precisar das duas, o lugar que muda é a casca, não a página.
2. **`LocationsPage` perdeu uma linha visível.** `"3 registro(s) nesta aba"`
   era o mesmo `3` de `"3 locais"`, escrito duas vezes. A saída semântica é
   deixar de escrevê-lo — mas é a única mudança de texto visível do lote, e
   está dita aqui.

**TDD**: 13 testes novos RED→GREEN, sendo 3 em um arquivo que não existia
(`AuditPage.test.tsx`). Suíte do front **200 → 215 em 44 arquivos**, `tsc` 0,
`eslint` 0.

**Achado durante o lote (não mexido)**: `TasksPage.tsx` mudou sob a gente —
no meio da execução o arquivo já tinha `totalCount`/`truncated`/`hasFilters`
em vez do `openCount`/`noticeText`/`filtered` que o plano descrevia. O commit
`004c6c6` (2026-10-13) já trazia essa forma e o arquivo está limpo no `git
status`. Os testes do lote foram reescritos contra o código real, e o texto do
subtítulo preservado (`3 tarefas`, `3 tarefas · filtros ativos`,
`3 tarefas · exibindo as 1 primeiras`).

---

## Fase 2 — User: nome completo + perfil

**Objetivo**: o usuário editar o próprio nome completo pela interface, com
validação nos dois lados, sem migração e sem tocar no domínio.

**Escopo**
- Back: `PUT /auth/profile` com re-emição do cookie (§2 — o campo já existe).
- Front: `AccountPage` passa a ter perfil editável + a troca de senha já
  existente (F26-T001), em vez de virar uma tela nova.

**Decisões que precisei tomar (nenhuma é requisito inventado — todas saem do
código existente)**
1. **`User.DisplayName` exposto como está.** A alternativa (separar
   `FirstName`/`LastName`) exige migração e toca registro/login/claims/admin →
   ADR. Adotada a opção sem migração.
2. **Método no `IIdentityAuthenticationService`, não no admin service.**
   `AuthenticationController` tem construtor de 3 argumentos instanciado em
   **8 pontos de teste**; injetar `IIdentityAdministrationService` ali quebraria
   3 arquivos alheios. `UpdateProfileAsync` foi declarado ao lado do
   `ChangePasswordAsync` — zero mudança de construtor e mesmo contrato
   (`OperationResult`).
3. **O cookie é re-emitido, o `/auth/me` não muda.** O nome exibido vem da
   claim do cookie; trocar o endpoint de leitura mudaria o boot do `SessionGate`
   sem necessidade. A re-emição preserva `SessionIdClaim` e as roles.
4. **Validação de nome não se duplica no cliente.** O `required` nativo cobre o
   vazio; o que passar dele vai para o servidor (`RequireDisplayName` é a
   autoridade). Guarda local duplicada fica inalcançável e faz o teste de
   envelope não significar nada.

**Critérios de aceite**
1. Usuário autenticado altera o nome e vê o valor novo **na mesma sessão**
   (sidebar/cabeçalho, sem F5).
2. A sessão **não** é derrubada — `SessionIdClaim` e roles preservadas.
3. Nome vazio/rejeitado → motivo pt-BR na tela, estado da sessão inalterado.
4. E-mail não editável nesta rota (troca é operação de admin).
5. Nenhuma migração; nenhum domínio novo.

**Testes e evidências (RED → GREEN)**
- Backend `AuthenticationProfileControllerTests` — 6 testes: envia o próprio
  `userId`; re-emite com o nome novo; preserva sessão + roles + e-mail; **não**
  re-emite quando o serviço recusa; mesmo contrato de falha do
  `ChangePassword` (400, não 404); `Unauthorized` sem identidade.
  RED real: 3 erros de compilação (construtor de 4 args, `UpdateProfileViewModel`
  inexistente, método inexistente).
- Front `AccountPage.test.tsx` — 4 testes novos. RED real: 4 falhas por falta
  do rótulo "Nome completo".
- Contagens: unit **2157 → 2163** · front **74 → 78** · `tsc 0` · `lint 0` ·
  build front OK · `dotnet build` 0 erros · arch **122 + 1** (falha
  pré-existente do ADR-044, inalterada).

---

O levantamento no código real **mudou a natureza de 3 itens do pedido**. Está
registrado aqui para não reabrir discussão por suposição.

### 2. User: nome completo + perfil — **o campo já existe**

`User` (`src/InfraControl.Domain/entities/User.cs` L11–15) tem
`DisplayName : string` (`RequireDisplayName`, L125–131), varchar(200) desde a
migração `20260921223416_AddIdentityFoundation`. **Não existe** `FullName`,
`FirstName` ou `LastName` — `DisplayName` **é** o nome completo, e é a origem
do nome exibido (`IdentityAuthenticationService.ToResult` L456).

O que **não** existe é tela de autoedição: a `/account` (F26-T001) só troca
senha e mostra o e-mail. Quem edita nome hoje é só o admin, em
`UsersPage` → `PUT /admin/users/{id}`.

> **Decisão pendente**: "implementar o nome completo na entidade" pode ser
> (a) expor o `DisplayName` num perfil de autoedição — sem migração; ou
> (b) separar em `FirstName`/`LastName` — exige migração, toca registro,
> login, claims e admin. **(a) é a leitura fiel do código**; (b) muda
> arquitetura de dados e precisa de ADR.

### 3. Login social só com usuário ativo — **já está implementado**

`IdentityAuthenticationService.LoginWithGoogleAsync` (L395) já recusa:
`if (identityUser is null || domainUser is null || !domainUser.IsActive)`
→ `ExternalLoginFailure.AccountUnavailable` (comentário anti-enumeração
L393–394). O mesmo guard existe no login por senha (L85) e no refresh (L124).
O controller devolve 403 "Conta não encontrada ou inativa. Fale com o
administrador." (`AuthenticationController` L90–92) e o front já traduz essa
mensagem (`LoginPage.messageFor` L25–32).

> **Não é task.** Ficará marcado como já existente, como já foi feito na
> auditoria prévia da Fase 26. Se o sintoma observado for outro (ex.: botão
> do Google aparecendo para conta inativa), precisa do sintoma relatado.

### 4. Convites — **os endpoints existem todos**

`GET/POST/PUT/DELETE /admin/invitations` (policy Administer),
`GET /invitations/me` + accept/reject (`[Authorize]`) e
`POST /auth/invitations/{id}/{accept|set-password|reject}` (AllowAnonymous).
Nenhum path divergente, nenhuma rota backend inexistente.

Dois problemas **concretos** achados:
1. `/invitations` **não está em `ROUTE_ACCESS`** (`app/router/access.ts`
   L13–29) → vira `read` por curinga (L33). Viewer vê "Convites" no menu, a
   página carrega, mas só chama `/invitations/me` (branch `isAdmin`,
   `InvitationsPage` L70–85).
2. **Criar convite não está na página de convites** — vive na `UsersPage`
   (L130–150). Criar em Usuários, gerenciar em Convites.

> **Decisão pendente**: qual é o **sintoma** de "não está funcionando"? O
> levantamento não achou quebra de rota/endpoint. Sem o sintoma eu estaria
> adivinhando.

### 6. Sidebar — **causa raiz encontrada**

Não existe dropdown expansível: `AppLayout` renderiza duas **listas planas**
(`visibleNavigation` L270–286, `visibleSecondaryNavigation` L291–307). A
navegação é por **label**, não por rota (`navigate(item.label)` L276; mapa
label→rota em `router/index.tsx` L73–75).

**O conflito é o label `"Zabbix"` duplicado** — `navigation` L13 **e**
`secondaryNavigation` L36. Como a chave é o texto, três coisas quebram ao
mesmo tempo:
- `labelRouteMap` (`Object.fromEntries`) fica com o **último** → clicar em
  "Zabbix" do menu primário (`/zabbix`) **navega para `/integrations/zabbix`**;
- `getNavItem` (`AppLayout.tsx` L168) pega o **primeiro** → ícone/aba errados;
- `activeItem === item.label` (L275, L296) **destaca os dois itens**.

**Correção estrutural** (não só renomear): chavear navegação por **rota**, não
por label — senão o próximo label duplicado repete o bug.

#### Resultado (Fase 4 — concluída 2026-10-11)

Aplicada exatamente essa correção, em quatro movimentos:

1. **`src/app/router/routeLabels.ts` (novo)** — concentra a identidade da
   navegação: `routeLabelMap` (rota→rótulo, injetivo por construção) e
   `labelForRoute(pathname)`, que absorve as rotas de detalhe (`/devices/:id`,
   `/tasks/:id`) que antes eram `startsWith` embutidos no router. Uma única
   fonte para "qual rótulo esta rota tem", usada pelo router e pelo layout.
2. **`navigationSlice`** — removidos `activeItem` e `setActiveItem`. Era o
   estado paralelo ao `location` (só alimentava o fallback da trilha) e
   justamente ele dessincronizava. `recentTabs` passa a guardar a **rota**;
   `resolveStoredRoute` migra o formato antigo (rótulo) do `localStorage` e
   descarta o que não resolve mais.
3. **`AppLayout`** — prop `activeRoute` (rota) no lugar de `activeItem`
   (rótulo); destaque, `key` e `getNavItem` comparam `item.route`; o item
   corrente ganhou `aria-current="page"`; trilha e texto da aba saem de
   `labelForRoute(route)`.
4. **`router/index.tsx`** — `handleNavigate(route)` virou `navigate(route)`
   puro: saíram `labelRouteMap`, `allNav`, `dispatch(setActiveItem)` e o
   `activeItemRedux` do store.

**Restou uma função rótulo→rota** (`resolveStoredRoute`), declarada em
comentário como exclusiva de migração de `localStorage` — **navegar nunca
passa por ela**; se um dia passar, o bug de duplicado volta. Os labels
permanecem duplicados no `dashboardData` **de propósito**: com a rota como
chave, dois itens com o mesmo texto são inofensivos, e renomear para esconder
o duplicado seria a "correção" que não corrige.

**TDD**: `AppLayout.test.tsx` — 3 testes, RED→GREEN (RED: `onNavigate`
recebia `"Zabbix"` e **nenhum** item ficava ativo quando a rota era passada;
GREEN: 3/3). Contagens: front **86 → 89** testes (21 → 22 arquivos),
`tsc` 0, `eslint` 0, `build` OK. Backend inalterado.

**Achado novo** registrado na §4: `<button>` aninhado dentro de `<button>` na
barra de abas (o React avisa durante o teste).

#### Resultado (2026-10-14 — o submenu virou dropdown inline)

O `<Menu>` POPUP flutuante do submenu morreu. Duas opções foram oferecidas
(inline com o visual atual × trocar a navegação por `PanelMenu` do
PrimeReact); o usuário escolheu **manter o visual atual e expandir inline**, e
**o pai continuar navegando** para a própria rota ao abrir — contrato
F29-T005 preservado.

Mudanças em `AppLayout`:
- saíram `navMenuRef`, `navMenuModel`, `handleNavDropdownClick`,
  `navItemToMenuItem` e o `<Menu model={navMenuModel} popup />`; entrou
  `expandedRoute`, cuja chave é a **rota** e não o label — os dois "Zabbix" não
  podem dividir o estado de expandido, que é o mesmo bug do F29-T005;
- os dois `.map` duplicados dos grupos primário e secundário viraram uma
  `renderNavItem(item)` só;
- os filhos viram `.uni-nav-subitem` dentro de `.uni-nav-sublist` (com filete
  de árvore) e a seta gira para cima com `.uni-nav-item.expanded`;
- o grupo **já abre** quando a rota corrente é de um filho (`hasActiveChild`),
  e nesse caso só o filho fica `.active`;
- os filhos passam pelo mesmo filtro de role dos pais (`canSee`).

**Com o sidebar recolhido (60px, só ícones) o submenu não abre** — não cabe;
o pai continua navegando e o usuário expande o sidebar para alcançar os filhos.
Registrado no CSS (`.uni-sidebar.collapsed .uni-nav-sublist { display: none }`)
e no comentário do `renderNavItem`.

**TDD**: 5 testes novos RED→GREEN (abrir inline sem popup, navegar E expandir
no mesmo clique, recolher no segundo clique, abrir sozinho quando a rota atual
é filho, e trocar de filho sem fechar o grupo). Suíte do front **195 → 200** em
43 arquivos, `tsc` 0, `eslint` 0.

**Achado**: clicando na aba recém-criada, o React avisa "button cannot be a
descendant of button" — o fecho da aba é `<button>` dentro do `<button>` da
aba. Pré-existente, já registrado na §4; não consertado aqui.

### 5. Erros no front — base sólida, 5 divergências

O padrão **já é consistente**: `ErrorBanner` em **27 páginas**. Divergências
concretas:
- `ProjectsPage` L294–296 e `ProjectDetailPage` L188–190: `<div>` vermelho
  artesanal reimplementando o `ErrorBanner`;
- `DashboardPage` L668–671 (erro de auditoria em div simples) e L313–335
  (aviso de convite em div verde, paralelo ao `NoticeBanner`);
- `TopologyPage` L308–318: erro inicial vira estado vazio, sem banner;
- **`ZabbixOverviewPage` L113: erro exibido com `NoticeBanner` (verde/check)**
  — tom errado;
- páginas de auth usam `uni-login-alert` próprio (aceitável, contexto
  diferente).

**Executado como Fase 5 (F29-T004, 2026-10-11)** — as 5 divergências acima
foram corrigidas, e o trabalho achou **3 problemas que a lista não trazia**:

1. **`ProjectDetailPage` — "Projeto não encontrado" para falha de rede.** O
   bloco de erro só era alcançável depois de um carregamento bem-sucedido;
   quando a carga falhava, `project` ficava null e a página caía no retorno
   `if (!project)`, exibindo "Projeto não encontrado". Não existe é diferente
   de não ter conseguido perguntar. Hoje: título "Não foi possível carregar o
   projeto" + `ErrorBanner` com retry.
2. **`DashboardPage` — o aviso de convite era código morto.** O painel só
   montava com `pendingInvitations.length > 0 || invitationsError`; aceitar ou
   recusar o **último** convite esvaziava a lista e desmontava o painel junto
   com o próprio aviso. A confirmação nunca chegou a ser vista. O teste que
   cobre o aviso só ficou verde depois desta correção — o RED inicial era
   exatamente esse sumiço.
3. **Retry inexistente.** Tanto o `div` da auditoria quanto os `div`s
   vermelhos dos Projetos não tinham forma de tentar de novo sem recarregar a
   página inteira; ao virarem `ErrorBanner`, todos ganharam `onRetry` (o da
   auditoria exigiu extrair o efeito para `useCallback`).

**Evidências**: 7 testes novos (RED → GREEN), todos falhando por
`role="alert"` ausente, nunca por harness; front **78 → 86 testes** (17 → 21
arquivos), `tsc 0`, `lint 0`, build OK. Slice só de front.

### 7. Dashboard e Topologia — já há base UniFi

O tema já é "UNIFI LIGHT THEME — Exact Copy" (`styles.css` L1–20). O Dashboard
já tem 5 `MiniStats` "estilo UniFi" (L404–445) e dados reais da API. A
Topologia é **SVG escrito à mão** (L387–512), **sem lib de grafo** — não há
react-flow/d3/cytoscape no `package.json`. Já existe
`components/ui/UniFiTopologyMap.tsx`, usado só pela `UniFiLivePage`.

> **Decisão pendente**: "melhorar o layout" pode ser refinar o SVG atual
> (sem dependência nova) ou adotar lib de grafo (dependência nova + ADR).

### 8. CRUD — mapa completo

**CRUD completo (6)**: Devices, Locations, Network (Vlan), Tasks, Sla,
Knowledge.
**Parcial (11)**: Incidents, Users (cria via convite), Invitations (sem
criar), Pdi, TaskDetail, Integrations, Remote, ZabbixSettings,
Notifications, Sessions, ProjectDetail.
**Só leitura (13)**: Alerts, Clients, Audit, Switches, WiFi, DeviceDetail,
Insights, UniFiLive, ZabbixOverview, ZabbixHosts (sync é a única escrita),
Projects, Logs/Health/Settings (estáticas).

**Código morto**: `createProject` (`services/api/projects.ts` L85) e
`updateProject` (L109) **não são usados por nenhuma página** — não existe UI
de criar/editar projeto.

> **Decisão pendente**: "todas as páginas têm CRUD" precisa de recorte. Alerts,
> Clients, Audit, WiFi, Insights e UniFiLive são **índices de leitura de
> dados que vêm de fora** (Zabbix/UniFi/descoberta) — criar/editar/excluir
> ali não tem fonte de verdade. Provável recorte: CRUD onde há agregado
> próprio de domínio; leitura onde a fonte é externa.

---

## 4. Registro de divergências (regra 11 da revisão F19/F20/F21)

| Divergência | Tratamento |
| --- | --- |
| `.gitignore` ignora diretório de código-fonte (`src/**/Health/`) | **Corrigido na Fase 0** e registrado aqui |
| `AiRuntimeHealth.cs` é fonte não rastreada e não compilável | Preservado no disco como `.cs.obsoleto`, fora do build; destino a decidir pelo dono do merge |
| `tsconfig.tsbuildinfo` rastreado e em conflito (`UU`) | Não é meu resolver; é artefato de build e não deveria estar versionado |
| **Arquitestest `Nginx_Should_Proxy_The_Api_Health_Checks` falha** | **Regra 11 aplicada — PARAR e registrar.** O teste (L100) exige a string literal `http://api:8080`; o trabalho **ADR-044 (escala horizontal)** da outra sessão trocou o proxy direto por `upstream api_backend` (`nginx.conf` L42–44, `proxy_pass http://api_backend` em L101/111/126/142) e **não atualizou a asserção**. A *intenção* do teste continua atendida (health checks são proxy da API, via upstream); só o literal ficou velho. **Não consertei**: mexer no teste é decidir sobre o desenho de escala horizontal de outra sessão. Correção provável = assertar `api_backend` + `server api:8080`, mas precisa do dono do ADR-044. |
| **`<button>` dentro de `<button>` na barra de abas** (`AppLayout.tsx`, `uni-tab` > `uni-tab-close`) | **Achado da Fase 4 (F29-T005)** — HTML inválido; o React avisa durante o teste ("In HTML, `<button>` cannot be a descendant of `<button>`"). O clique funciona porque o botão interno usa `stopPropagation`. **Não corrigi**: sai do escopo da Task (navegação por rota) e a correção real é estrutural — separar o rótulo do fechamento em dois controles irmãos —, o que mexe no CSS das abas e precisa verificação visual. Registrar aqui para não sumir. |
| **A documentação citava um tipo que não existe**: `IAuthoredRequest` | **Corrigido na Fase 1 (F29-T001A)** — o tipo real é `IAuthorizedRequest` (`MediatorContracts.cs:9`); `IAuthoredRequest` aparecia **3×** em `docs/24-BUILDING-BLOCKS.md` (L59, L66) e `ADR-016` (L98), e o `AuthorizationBehavior` age mesmo sobre `IAuthorizedRequest`. Só o nome foi corrigido — nenhuma decisão de ADR mudou. Deixar assim faria o próximo slice ser escrito contra um tipo de fundo de quintal. |

## 5. Pendências de ambiente que atravessam todas as fases

- **Push**: commits locais aguardam `git push origin main` autenticado.
- **Merge incompleto** (`9d1e759`): enquanto aberto, qualquer commit meu
  entra em cima de um estado do índice instável.
- **Integração com Docker**: fases que precisarem de banco real (migração de
  `User`, validação de convite) ficam com evidência ⚠️ até haver ambiente.
