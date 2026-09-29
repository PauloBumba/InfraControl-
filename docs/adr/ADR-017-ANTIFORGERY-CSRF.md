# ADR-017 — Antiforgery (CSRF) nos endpoints com cookie

## Status
Accepted (2026-09-29)

## Contexto

A sessão do dashboard é um **cookie HttpOnly auto-contido** (`AddCookie` com
`ClaimsPrincipal` próprio). O token nunca passa pelo JavaScript, o que é
bom, mas significa que **o navegador anexa a credencial a toda requisição para
a origem** — inclusive a que um site atacante conseguiria induzir o usuário a
disparar. SameSite=Strict é a primeira barreira e já estava configurado
(F1-T015 registrado em 2026-09-28 como "protegido por SameSite, sem
implementação"), mas não é o suficiente para fechar a task:

- SameSite é defense-in-depth, não a política. A proteção real não pode
  depender de um atributo de cookie que o navegador pode afrouxar em
  navegadores móveis, nem de `Secure`/`HttpOnly`.
- same-site ≠ same-origin. Uma aplicação em `*.exemplo` (ou um takeover de
  subdomínio) é "same-site" para o navegador e continuaria enviando o cookie
  num POST cross-origin que a política de SOP não impede de ser *enviado* —
  só de ser lido.
- F1-T014 ampliou a superfície: `POST /auth/google` troca `id_token` por
  sessão, e o frontend usa `credentials: "include"`.

Critério de aceite da task: **nenhum método de escrita autenticado por cookie
passa sem token válido**.

## Decisão

Double-submit do ASP.NET Core (`IAntiforgery`), com a configuração em um lugar
único (`AntiforgeryCookieSetup`) usada tanto pelo `Program.cs` quanto pelos
testes — assim o teste não valida uma configuração diferente da que roda.

| Peça | Valor | Por quê |
|---|---|---|
| Cookie | `XSRF-TOKEN` | nome convencional; o JS lê pelo `document.cookie` |
| `HttpOnly` | **false** | é o ponto: o navegador precisa que o JS leia e devolva o valor no header. É isso que torna a SOP a barreira |
| `Secure` | `Always` | token não trafega em claro |
| `SameSite` | `Strict` | mantém a primeira barreira |
| Header | `X-XSRF-TOKEN` | o valor do cookie, devolvido pelo JS |

O token **não** é o par `user|token` cru: é o *request token* protegido pelo
Data Protection e vinculado ao usuário autenticado. O atacante cross-site não
consegue ler o cookie (SOP) e portanto não consegue montar o header.

**Emissão** — `GET /auth/me` chama `GetAndStoreTokens`, então o cookie nasce
junto com a sessão. O frontend já faz esse call no boot (`SessionGate`), o que
evita endpoint novo e round trip extra. O `/auth/me` é autenticado, o que é
obrigatório: o token é vinculado à identidade.

**Validação** — um middleware global (`AntiforgeryCookieMiddleware`), ligado
depois de `UseAuthentication` e antes de `UseAuthorization`:

- método de escrita (`POST`/`PUT`/`PATCH`/`DELETE`) **e** `User.Identity.IsAuthenticated`
  → `ValidateRequestAsync`;
- leitura nunca é validada (nenhuma requisição de leitura muda estado);
- **anônimo não é validado**: login, registro, aceite de convite, reset de
  senha e `POST /auth/google` não têm cookie de sessão para explorar — no
  login ainda não existe token. Validá-los exigiria emitir token para
 visitante anônimo, o que abre superfície sem fechar nada.

**Falha** — `AntiforgeryValidationException` vira **400** no envelope
`ApiResponse` via `GlobalExceptionHandler`, com mensagem genérica: não
distingue "faltou token" de "token inválido", para não virar oráculo de
qual token está em uso. Nunca 403 (o usuário está autenticado) nem 500.

**Frontend** — `client.ts` lê o cookie uma vez por request de escrita e
acrescenta o header. Login e boot não enviam (anônimos).

## Alternativas descartadas

- **Só SameSite=Strict** — status quo; não fecha a task e não é política.
- **Token em campo de form** (`RequestVerificationToken`) — é o padrão MVC, mas
  a API é JSON com `[ApiController]`: o middleware interno do ASP.NET só valida
  endpoints com metadado de antiforgery e, na prática, form posts. Exigiria
  metadado em ~180 endpoints, ou seja, um caminho de bypass por controller novo.
- **Cookie HttpOnly + token só no header** — não funciona: sem o valor do
  cookie o JS não tem o que mandar.
- **Comparar string aleatória em memória** — reimplementa o que o Data
  Protection já resolve, com mais chance de errar.

## Consequências

### Positivas

- A política é **global e por padrão deny**: um controller novo não pode
  "esquecer" a proteção, porque ela não está no controller.
- O frontend tem uma regra só, no cliente HTTP único.
- `HttpOnly: false` é uma superfície conhecida e aceita (é o mesmo trade-off
  do Angular); a proteção real é a SOP + token vinculado ao usuário.

### Negativas e limites

- **Não protege contra XSS**: script na mesma origem lê o cookie.fora do
  escopo desta task (CSP/ sanitation é o que fecha isso).
- O cookie legível é um segredo de menor valor: só serve se vier no header
  de um request que o navegador faz à própria origem.
- O repositório **não tem harness HTTP** (os testes de integração montam o
  pipeline MediatR, não a pipeline HTTP). A validação é testada no nível do
  middleware usando o `IAntiforgery` real + `DefaultHttpContext`, e a
  emissão em `/auth/me` por teste do handler. Um `WebApplicationFactory`
  daria a cobertura e2e que falta — registrado como pendência em `TASKS.md`,
  não inventado aqui.
- Clientes não-browser (curl, testes) precisam mandar o header em escrita.
  É o preço do double-submit e o mesmo que toda SPA paga.

## Critérios de aceite

- [x] Nenhum método de escrita autenticado por cookie passa sem token válido
- [x] Falha responde 400 no envelope `ApiResponse`, mensagem genérica pt-BR
- [x] Leitura e requisição anônima não exigem token
- [x] O cliente HTTP do dashboard envia o header automaticamente
- [x] Testes: sem token → nega; com token válido → passa; token de outro
      usuário → nega; anônimo/GET → passa

## Referências

- `src/InfraControl.Api/Security/AntiforgeryCookieSetup.cs` — nomes e opções
- `src/InfraControl.Api/Middleware/AntiforgeryCookieMiddleware.cs` — a política
- `src/InfraControl.Api/Middleware/GlobalExceptionHandler.cs` — 400 pt-BR
- `src/InfraControl.Api/Controllers/Identity/AuthenticationController.cs` — emissão em `/auth/me`
- `frontend/InfraControl.Web/src/services/api/client.ts` — envio do header
- ADR-016 — autorização por roles (a sessão é a credencial que o CSRF protege)
