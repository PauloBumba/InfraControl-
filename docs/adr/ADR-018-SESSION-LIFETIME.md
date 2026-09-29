# ADR-018 — Teto de sessão e verificação server-side do cookie

## Status
Accepted (2026-09-29) — cobre F1-T016(b)

## Contexto

A sessão do dashboard é um **cookie auto-contido**: `AddCookie` sem
`ITicketStore` significa que o servidor **confia nas claims sem consultar
nada**. Em paralelo existe a tabela `UserSessions`, com refresh token, sliding
lifetime de 30 dias e revogação — mas ela só é lida no `POST /auth/refresh` e
no logout. O dashboard nem usa refresh (o cookie é a sessão), então na prática:

- **revogar sessão não derruba cookie**: desabilitar usuário, trocar senha,
  "sair de todos os outros" — nada disso tira a sessão de quem já está com o
  cookie na mão, porque nenhuma requisição consulta `UserSessions`;
- **não há teto**: `ExpiresAt` é esticado a cada `Refresh`, de modo que uma
  sessão ativa nunca expira sozinha;
- **dois prazos divergentes**: o cookie vive 14 dias (default do `AddCookie`) e
  o registro de sessão, 30. O registro sobrevive ao cookie;
- **o cookie não carrega qual sessão é**: as claims são usuário e roles, sem
  identificador de sessão. Não há como perguntar "esta sessão ainda vale?".

Critério de aceite da F1-T016: sessão expirada derruba o cookie no backend e
revogação tem efeito imediato.

## Decisão

A sessão deixa de ser um bearer auto-contido sem autoridade: o cookie passa a
carregar **qual** sessão é, e cada request autenticado consulta o estado dessa
sessão, com cache curto.

**1. Claim `sid`** — `UserSession.Id` vai para o cookie. `CreatePrincipal`
ganha a claim; os três pontos de emissão (login, `/auth/google`, `/auth/refresh`)
passam aonger o `SessionId`, que vem em `AuthenticatedUser`.

**2. Teto absoluto** — `UserSessions.AbsoluteExpiresAt`: escrito no `Start`
(`now + AbsoluteLifetime`), **nunca** movido pelo `Refresh`. `IsActive` passa a
exigir os dois prazos:

```csharp
public bool IsActive => RevokedAt is null
                      && ExpiresAt > now          // deslizante (30 dias)
                      && AbsoluteExpiresAt > now; // teto (12 horas)
```

Renovar o token estende o deslizante e não corre o teto: sessão antiga morre
mesmo com o usuário ativo nela. Re-login abre janela nova.

**3. Verificação por request** — middleware depois de `UseAuthentication` e
antes de `UseAuthorization` (401 antes de qualquer lógica de endpoint):

| Situação | Resultado |
|---|---|
| anônimo | passa (não há sessão a checar) |
| sem claim `sid` | **401** +cookie apagado |
| sessão não encontrada / revogada / expirada / de outro usuário | **401** +cookie apagado |
| sessão ativa | passa, estado cacheado por `CheckInterval` |

**Cookie sem `sid` é rejeitado (fail-closed).** Os cookies emitidos antes desta
mudança não têm `sid`; deixá-los passaria preserva o buraco que a task quer
fechar. O custo é um re-login — o mesmo custo que o rename `User`→`Operator` da
ADR-016 já impôs.

**4. Cache curto** — `IMemoryCache`, chave `active-session:{sid}`, TTL
`CheckInterval` (30s). Escolha conscious: uma consulta ao banco por request em
toda API de inventário/monitoramento é custo que não se paga para fechar uma
janela de 30 segundos. **Consequência aceita e declarada: revogação leva até
30s para valer em outra request** — o logout da própria request é imediato
(o cookie é apagado no navegador). A 016(c) estreita isso.

O cache é por processo: com mais de uma instância, cada uma tem o seu. A
janela não piora (é o mesmo TTL), mas a revogação precisa alcançar todas as
instâncias para ser imediata — mais um motivo para a 016(c) olhar Redis.

**5. Valores são configuração**, em `Session` no `appsettings.json`:

| Chave | Default | Papel |
|---|---|---|
| `SlidingLifetime` | `30.00:00:00` | deslizante, e o `ExpireTimeSpan` do cookie passa a usar o mesmo valor (hoje divergem: 14d vs 30d) |
| `AbsoluteLifetime` | `12:00:00` | teto |
| `CheckInterval` | `00:00:30` | TTL do cache de verificação |

`AbsoluteLifetime` menor que `SlidingLifetime` é rejeitado na startup: teto
maior que deslizante é configuração sem sentido.

## Alternativas descartadas

- **`ITicketStore` no Redis** — revogação O(1) sem janela, mas troca o modelo
  de autenticação inteiro. Aproxima-se depois, se a janela de 30s incomodar.
- **Guardar `SecurityStamp` na claim e comparar com o banco** — mesma consulta
  por request, e ainda traria o race do stamp. `sid` é mais direto: identifica a
  sessão, não o usuário.
- **Confiar em `IsActive` do lado do navegador** — o cliente não decide
  autorização.
- **Rotacionar o `SecurityStamp` e reemitir cookie a cada request** — o cookie
  é auto-contido justamente para não exigir isso.

## Consequências

### Positivas

- Desabilitar usuário, trocar senha e revogar sessão **passam a matar a sessão
  cookie** — a tabela `UserSessions` deixa de ser decorativa.
- Teto absoluto fecha a sessão eternal.
- Cookie e registro passam a ter o mesmo prazo.

### Negativas

- Uma consulta ao banco a cada `CheckInterval` por sessão ativa.
- Re-login único para quem já tinha cookie.
- A janela de 30s é real e está documentada, não escondida.

## Critérios de aceite

- [x] Cookie sem `sid` não passa
- [x] Sessão revogada, expirada (deslizante ou teto) ou de outro usuário → 401
      e cookie apagado
- [x] `Refresh` não corre o teto
- [x] Usuário desabilitado derruba a sessão (via revogação existente)
- [x] Anônimo não paga consulta ao banco
- [x] Prazos configuráveis, com `AbsoluteLifetime >= SlidingLifetime` validado

## Pendências (não é esta task)

- **Não há cobertura e2e do 401**: o repositório não tem `WebApplicationFactory`.
  O middleware é testado com um `IActiveSessionReader` falso (a decisão de
  política) e o leitor é testado contra Postgres de verdade (a consulta).
- Janela de 30s na revogação (ver 4).
- Política por role (`a política é configurável por role`, F1-T016): aqui há um
  prazo único configurável; o corte por role fica para a 016(c).
- 016(a) TOTP, 016(c) lista/revogação, 016(d) auditoria, 016(e) UI.

## Referências

- `src/InfraControl.Domain/Entities/UserSession.cs` — teto
- `src/InfraControl.Api/Middleware/ActiveSessionMiddleware.cs` — a verificação
- `src/InfraControl.Application/Identity/Sessions/Ports/IActiveSessionReader.cs` — a porta
- `src/InfraControl.Api/Configuration/SessionLifetimeOptions.cs` — prazos
- ADR-016 — a role como unidade de acesso (é o que a sessão carrega)
- ADR-017 — a proteção que a sessão precisa ter
