# ADR-020 — Sessões: lista, revogação e janela imediata

## Status
Accepted (2026-09-29) — cobre F1-T016b

## Contexto

A ADR-018 deu autoridade ao cookie (claim `sid`, teto absoluto, verificação por
request) mas deixou três coisas para cá:

- **janela de 30s**: o estado "sessão ativa" fica em cache em `IMemoryCache`,
  que é por instância. Revogar uma sessão atualiza o banco e a instância local,
  mas as outras instâncias só repercebem no próximo `CheckInterval`. Em
  multi-instância, "revogação tem efeito imediato" não era verdade;
- **sem auto-serviço**: não existia como listar as próprias sessões, revogar
  uma delas ou "sair de todos os outros". Revogação em massa só existia como
  efeito colateral de trocar a senha;
- **política única**: um prazo para todos, sem corte por role.

Dois desvios apareceram no caminho:

1. a ADR-018 dizia que os prazos eram "configurável (appsettings `Session`)",
   mas `IdentityAuthenticationService` ainda usava **constantes estáticas**
   (30d/12h) — o appsettings só movia o cookie e o middleware;
2. `LastSeenAt` só andava no `Refresh`, e o dashboard não usa refresh — uma
   lista de sessões mostraria a hora do login como "visto por último",
   inútil para reconhecer dispositivo.

## Decisão

**1. O cache do check passa a ser compartilhado.** O `ActiveSessionMiddleware`
troca `IMemoryCache` por `ICacheService` (cache híbrido da ADR-010: Redis
primário, fallback em memória). Revogação (qualquer caminho) grava no cache a
entrada `active-session:{sid}` com `ActiveSessionCheck(Revoked)` — **gravar,
não apagar**: apagar deixaria o fallback local de outra instância servindo o
positivo velho; o negativo explícito é lido pelo Redis em todas as instâncias.

Isto **reverte um descarte consciente da ADR-018** ("hop de Redis por request"
era visto como custo alto demais). O preço é aceito porque é o único jeito de
revogação valer entre instâncias sem pub/sub: 1 GET de Redis por request
autenticado. Com o Redis fora, o híbrido cai no fallback local e a janela de
30s volta, com log — degradação documentada, não falha.

**2. Auto-serviço em cima da `UserSessions`.** Port `ISessionService` na
Application (`ListAsync`, `RevokeAsync`, `RevokeOthersAsync`) e
`SessionsController` em `api/v1/auth/sessions` — qualquer role autenticada
mexe nas PRÓPRIAS sessões (mesma lógica do MFA: `[Authorize]` simples).

- `GET /sessions` → só as ativas do dono, com marcador `isCurrent` (o `sid`
  do cookie);
- `DELETE /sessions/{sessionId}` → revogação individual; se for a sessão
  atual, o cookie é assinado para fora na resposta;
- `DELETE /sessions/others` → "sair de todos os outros", mantendo a atual;
- sessão inexistente **ou** de outra pessoa respondem a MESMA mensagem
  "Sessão não encontrada." — o cliente não vira oráculo do `sid`, igual à
  política do middleware.

**3. Invalidação em toda revogação.** `LogoutAsync`, `RevokeAllSessionsAsync`
(troca/reset de senha) e os dois comandos do `SessionService` gravam o check
`Revoked` no cache compartilhado. Revogar sem gravar deixaria o cookie da
outra instância vivo até o intervalo.

**4. Prazos viram options de verdade.** `SessionLifetimeOptions` sai de
`Api/Configuration` para `Application/Identity/Sessions` (Infrastructure não
referencia Api) e `IdentityAuthenticationService` passa a lê-las — as
constantes somem. Cura o desvio 1 acima: o que a ADR-018 prometia em
appsettings passa a valer para o login, o refresh e o middleware.

**5. Política por role.** `Session:RoleAbsoluteLifetimes` mapeia role → teto
(ex.: `Admin` → `08:00:00`). No login, o teto efetivo é o **menor** entre o
`AbsoluteLifetime` configurado e os caps das roles do usuário:

- cap só encurta, nunca amplia — para ampliar, mexa no teto base;
- caps devem ser `> 0` e nomear role conhecida (`Admin`/`Operator`/`Viewer`),
  validado na startup junto do resto;
- sem efeito retroativo: sessões existentes mantêm o teto do login; troca de
  role vale no próximo login;
- o deslizante não é afetado (o `UserSession.Start` continua exigindo teto ≤
  deslizante).

**6. `LastSeenAt` sob throttle.** O `ActiveSessionReader`, ao concluir que a
sessão está ativa e o `LastSeenAt` tem mais de 5 minutos, escreve o novo valor
com `ExecuteUpdate` (sem change tracker). Custo: no máximo um UPDATE a cada
5min por sessão ativa, embutido na consulta que o cache miss já faz — e a
lista de sessões ganha "visto por último" honesto.

## Consequências

- 1 GET (+ eventual SET a cada `CheckInterval`) de Redis por request
  autenticado; compensação aceita pela revogação imediata;
- `SessionLifetimeOptions` mudou de namespace — `Api/Configuration` deixou de
  existir; testes que a referenciavam foram ajustados;
- middleware agora depende de `ICacheService`; os testes usam um fake com TTL;
- o efeito no cookie destruído pela revogação da sessão atual continua sendo
  o `401` do middleware na próxima navegação (a resposta da revogação em si é
  200 — o frontend redireciona);
- auditoria ("sessão revogada") é objeto da F1-T016c, não desta task.

## Critérios de aceite

- [x] `GET /sessions` lista apenas as sessões ativas do próprio usuário, com
  `isCurrent` apontando a sessão do cookie;
- [x] revogação individual e "sair de todos os outros" gravam o check
  `Revoked` no cache compartilhado — a outra instância derruba o cookie sem
  esperar `CheckInterval`;
- [x] logout e troca/reset de senha também gravam o check;
- [x] tentar revogar sessão inexistente ou alheia responde
  "Sessão não encontrada." sem alterar nada;
- [x] cap por role aplicado no login (teto = menor valor) e validado na
  startup;
- [x] `LastSeenAt` só anda fora do throttle e só para sessão ativa;
- [x] suíte unitária (1181) + arquitetura (84) verdes.
