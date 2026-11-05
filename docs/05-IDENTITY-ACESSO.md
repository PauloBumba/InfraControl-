# Identity e Acesso

## Base

ASP.NET Core Identity.

### F1-T001 — Fundação implementada

- `InfraControl.Domain.Identity.User` é o agregado de negócio e possui estado ativo, email e roles;
- `UserRole` define `Admin`, `Operator` e `Viewer`, com atribuição controlada pelo agregado (`User` foi renomeado para `Operator` no ADR-016);
- `UserSession` é o agregado responsável por expiração, refresh token hash e revogação de sessão;
- eventos de domínio cobrem criação, ativação, desativação, role e ciclo de sessão;
- `InfraControlUser` é somente o adaptador técnico de credenciais do ASP.NET Identity;
- `AppDbContext` herda de `IdentityDbContext` e mantém Outbox, auditoria e agregados de domínio;
- `UserManager` e roles store são registrados no módulo de Infrastructure;
- senha exige no mínimo 12 caracteres, número, maiúscula, minúscula e caractere não alfanumérico;
- migration `AddIdentityFoundation` cria as tabelas padrão do Identity no PostgreSQL;
- endpoints, convites e login social permanecem evoluções futuras; os endpoints de autenticação, cookie de sessão e refresh já estão implementados.

### F1-T002 — Bootstrap Admin

O bootstrap é um caso de uso da camada `Application` e usa uma porta (`IAdminIdentityProvisioner`) para solicitar a persistência ao adaptador de `Infrastructure`. Ele é desativado por padrão e só executa quando `BootstrapAdmin:Enabled=true`.

Os valores devem ser fornecidos por configuração segura, preferencialmente variáveis de ambiente:

```text
BootstrapAdmin__Enabled=true
BootstrapAdmin__Email=admin@example.com
BootstrapAdmin__DisplayName=InfraControl Admin
BootstrapAdmin__Password=<secret>
```

O processo é idempotente: se já existir qualquer administrador ou se o email configurado já estiver cadastrado, nenhuma duplicação é criada. A senha é entregue ao `UserManager` para hash e nunca é escrita em logs. A criação do usuário técnico, role `Admin` e agregado de domínio ocorre em transação única.

### F1-T003 — Roles e RBAC

O gerenciamento de roles tem **um caminho só** desde o F18-T004: `Admin/UsersController` (`api/v1/admin/users`, `POST/DELETE {id}/roles/{role}`) delega a `IIdentityAdministrationService`, cuja implementação (`Infrastructure/Auth/IdentityAdministrationService`) persiste direto no agregado `User` — as invariantes (último admin ativo, papel obrigatório) ficam no domínio e no serviço, verificadas antes do `SaveChanges`. O slice CQRS de papéis (`AssignUserRoleCommand`/`RemoveUserRoleCommand`, a porta `IUserRoleRepository` e o adaptador `IdentityUserRoleRepository`, que sincronizava papéis com o ASP.NET Identity) e a rota `api/identity/users` do `RolesController` foram **removidos**: rota fora do padrão v1, DELETE duplicado do `UsersController`, sem consumidor no front desde o F17-T006 — e a autorização não lê mais roles do Identity (o login monta as claims do cookie a partir do domínio, `IdentityAuthenticationService.ToResult`). A migração dos testes de cobertura expôs e corrigiu o bug `ValueGeneratedOnAdd` na chave de `UserRoleAssignment` (filha nova anexada como `Modified` → `DbUpdateConcurrencyException`/500 em atribuir ou remover papel — nunca houve teste de rota); `IdentityAdministrationServiceTests` é o teste de regressão.

### ADR-016 — autorização por roles (fonte única)

A **role é a única unidade de acesso**. Não existem permissões: o enum
`Permission`, a matriz `RolePermissions`, as claims `permission` e as policies
HTTP por permissão foram removidos. A hierarquia vive em um lugar só,
`RoleAccess` (`Domain/Enums/RoleAccess.cs`), e é consumida pelos dois pontos de
checagem:

| Nível | HTTP | MediatR (`RequiredRoles`) | Roles |
|---|---|---|---|
| Ler | `[Authorize]` | `RoleAccess.Read` | `Viewer`, `Operator`, `Admin` |
| Operar | `[Authorize(Policy = AuthorizationPolicies.Operate)]` | `RoleAccess.Operate` | `Operator`, `Admin` |
| Administrar | `[Authorize(Policy = AuthorizationPolicies.Administer)]` | `RoleAccess.Administer` | `Admin` |

O controller HTTP apenas converte a requisição em comando e envia pelo MediatR;
`AuthorizationBehavior` é a 2ª camada, dentro dos handlers, e vale também para
workers que não passam pelo HTTP. Ele exige usuário **autenticado** mesmo sem
role declarada e responde `Roles ausentes: …` (ordenado, para ser testável).

A alteração de role é autorizada por `Administer`; o último administrador ativo
não pode perder a role `Admin` e um usuário não pode ficar sem role.

> **Histórico:** o ADR-008 removeu as policies por permissão, mas os slices
> F6/F7/F8 as reintroduziram e 12 slices passaram a usar
> `[Authorize(Policy = "<Permission>")]`. Passaram a coexistir dois estilos —
> 46 literais de role e 80 policies por permissão. O ADR-016 remove os dois e
> fixa a role como fonte única; `RoleAuthorizationStandardizationTests` impede a
> reintrodução de literal ou de policy por permissão.

> **Migração de dados:** `UserRole` é persistido como string, então
> `20260929010000_RenameUserRoleToOperator` renomeia `'User'` → `'Operator'` em
> `DomainUserRoles.Role` e `AspNetRoles.Name` (o `AspNetUserRoles.RoleId` não
> muda), esvazia `UserSessions` e rotaciona o `SecurityStamp`. Um cookie emitido
> antes carrega a role antiga, que deixa de ser válida: as policies passam a
> **negar** (fail-closed) e o usuário entra de novo — não há escalada de
> privilégio.

## API de autenticação e dashboard

O backend expõe os fluxos de Identity inspirados no Sentinel:

| Endpoint | Função | Acesso |
|---|---|---|
| `POST /api/v1/auth/register` | Cria usuário com role inicial `Viewer` | Anônimo |
| `POST /api/v1/auth/login` | Valida credenciais, cria cookie e sessão refresh | Anônimo |
| `POST /api/v1/auth/refresh` | Rotaciona refresh token e reemite cookie | Anônimo |
| `POST /api/v1/auth/logout` | Revoga sessão e cookie | Autenticado |
| `POST /api/v1/auth/forgot-password` | Gera reset sem revelar existência do email | Anônimo |
| `POST /api/v1/auth/reset-password` | Redefine senha com token Redis single-use | Anônimo |
| `POST /api/v1/auth/change-password` | Troca senha e revoga sessões | Autenticado |
| `POST /api/v1/auth/google` | Valida `id_token` do Google e cria sessão para conta já ativa (fallback GSI, F1-T014) | Anônimo |
| `GET /api/v1/auth/google` | Start do authorization code: state CSRF em cookie + 302 ao consent screen (F43) | Anônimo |
| `GET /api/v1/auth/google/callback` | Troca `code` por token no servidor + sessão; falha = 302 `/login?error=google`; conta indisponível = 302 `/login?error=google&reason=account` (F43) | Anônimo |
| `GET /api/v1/auth/google/failure?reason=` | Mensagem de erro do login social (texto do backend pela razão); anônimo (F43) | Anônimo |
| `GET /api/v1/auth/me` | Retorna usuário, nome de exibição, roles e permissões | Autenticado |
| `GET /api/v1/auth/mfa/status` | Estado do MFA da conta (configurado/ativo/desde quando) | Autenticado |
| `POST /api/v1/auth/mfa/setup` | Emite secret TOTP novo (pendente) + URI otpauth p/ QR | Autenticado |
| `POST /api/v1/auth/mfa/confirm` | Valida TOTP e ativa o MFA; devolve 10 recovery codes (1× só) | Autenticado |
| `POST /api/v1/auth/mfa/disable` | Desativa e apaga o MFA (código TOTP **ou** senha) | Autenticado |
| `GET /api/v1/auth/sessions` | Lista as sessões ativas do próprio usuário (com marcador da atual) | Autenticado |
| `DELETE /api/v1/auth/sessions/{sessionId}` | Revoga uma sessão; se for a atual, assina o cookie para fora | Autenticado |
| `DELETE /api/v1/auth/sessions/others` | "Sair de todos os outros" (mantém a sessão atual) | Autenticado |

### Recuperação de senha (fluxo completo)

```text
Usuário → ForgotPasswordPage (/forgot-password)
→ POST /auth/forgot-password {email}
→ IIdentityEmailSender.SendPasswordResetAsync (token de uso único, 30 min)
→ Email (link /reset-password?email=&token=)
→ ResetPasswordPage (/reset-password)
→ POST /auth/reset-password {email, token, newPassword} → 204
```

O link e-maiado nasce na PÁGINA pública `/reset-password` do dashboard
(entregue em 2026-10-10, fechando a pendência da auditoria front×back de
01/10): antes apontava para a própria API — GET em rota só-POST respondia
`405` e não havia tela, então o fluxo nascia na UI e morria no e-mail. A
origem do link segue a MESMA regra do convite (`Frontend:BaseUrl` com
fallback à origem da própria request — ver a seção **Convite**); sem origem
o link sai relativo. O token fica em `identity:password-reset:{hash}` no
cache (30 min), é conferido contra o e-mail e removido no sucesso, e
redefinir derruba todas as sessões (`SessionsRevoked`). A
`ResetPasswordPage` lê `email`/`token` da query, tem estado honesto para
link incompleto (sem chamar a API com credencial incompleta — botão para
pedir um link novo), mostra o motivo pt-BR do envelope quando o backend
recusa (token expirado, política de senha) e em sucesso leva ao `/login`.
Escrita anônima sem antiforgery (F1-T015/ADR-017).

### MFA TOTP (F1-T016a, ADR-019)

O `POST /api/v1/auth/login` aceita `mfaCode` opcional. Quando a conta tem MFA
ativo, a senha é validada primeiro e só então o segundo fator é exigido: sem
código a resposta é `401 {"errors":["Código MFA obrigatório."]}` (a UI abre o
campo), código errado responde `Código MFA inválido.` — nenhum dos dois cria
sessão. O secret fica cifrado com AES-256-GCM (`ICredentialProtector`, chave
`RemoteAccess:CredentialEncryption:Key`), a tolerância é de ±1 período de 30s
(`Mfa:ToleranceSteps`) e os 10 recovery codes (formato `XXXX-XXXX`) são
guardados apenas como hash SHA-256 de uso único. Os 4 endpoints de MFA são
auto-serviço: qualquer role autenticada mexe na própria conta (`[Authorize]`
sem exigir Operate/Administer — um Viewer também precisa proteger o próprio
acesso).

Ligar e desligar o MFA gravam evento no audit existente (`MfaEnabled` e
`MfaDisabled`, resource `Mfa` — F1-T016c).

F1-T016d (UI): a tela de login do dashboard tem os 2 passos — o `401
"Código MFA obrigatório."` troca a tela para o campo "Código de verificação"
(`autoComplete="one-time-code"`; aceita TOTP de 6 dígitos **ou** recovery code
no mesmo campo, como o contrato manda, com hint dizendo os dois), com
"Voltar" para o passo da senha. A senha fica só no estado local do componente
(nunca persistida) e é limpa no sucesso e na falha do passo 1 — no passo 2
ela fica porque o retry reenvia email+senha+código. No passo do código o
Google e o "esqueceu a senha" somem. Detalhe de contrato: a mensagem pt-BR é
o que leva a UI ao campo (constantes `MFA_REQUIRED_MESSAGE`/`isMfaRequired`
em `services/api/auth.ts` — trocar a string do controller quebra a tela).
As recusas do login mandam `anonymousExpected` no client: o 401 da tela de
login não dispara mais o interceptor global de `signedOut`.

A área administrativa possui `GET /api/v1/admin/users`, alteração de status e atribuição/remoção de roles. Os endpoints autorizam por role: leitura com `Roles = "Admin,User"`, gestão com `Roles = "Admin"`. O sender de email é `SmtpIdentityEmailSender` (F41, MailKit) — mesmo SMTP configurado em Integrações → Email; sem config, cai no adaptador de logging (`LoggingIdentityEmailSender`). A porta `IIdentityEmailSender` permanece o contrato — Application não muda.

### Sessões (F1-T016b, ADR-020)

Os 3 endpoints de sessão são auto-serviço (`[Authorize]`, qualquer role — o
usuário mexe nas PRÓPRIAS sessões, mesma lógica do MFA). A lista mostra só
as ativas do dono, da mais recente para a mais antiga, com `isCurrent`
apontando o `sid` do cookie e `lastSeenAt` mantido por touch sob throttle de
5min no `ActiveSessionReader` (sem isso o dashboard, que não usa refresh,
mostraria a hora do login). Tentar revogar sessão inexistente **ou** de outra
pessoa responde a MESMA mensagem `Sessão não encontrada.` (`404`) — o cliente
não vira oráculo do sid.

A revogação é sempre **duas escritas**: a entidade no banco e o check
`Revoked` em `active-session:{sid}` no cache distribuído (Redis primário,
fallback memória — ADR-010). A segunda derruba o cookie da outra instância
na hora, sem esperar o `CheckInterval`; por isso `logout`,
`change-password`/`reset-password` e os próprios endpoints também gravam o
check. Com o Redis fora, o fallback local mantém a janela de 30s (logado) —
degradação, não falha.

Toda revogação também vira evento no audit (`SessionRevoked` para uma sessão,
`SessionsRevoked` para "sair de todos os outros" e para troca/reset de senha,
`Logout` para o logout), além do `LoginWithMfa` do login que passou pelo 2º
fator (F1-T016c). Os eventos são gravados pelos serviços de identidade fora do
pipeline MediatR — `IAuditWriter` no mesmo `SaveChanges` da operação — só em
sucesso e só com metadados (action, resource, outcome, actor = e-mail do dono,
correlation = TraceId); nenhum código, senha ou secret entra na linha.

Prazos e política ficam em `Session` no appsettings: `SlidingLifetime`,
`AbsoluteLifetime`, `CheckInterval` e `RoleAbsoluteLifetimes` (cap por role
que só encurta o teto, aplicado no login e validado na startup).

F1-T016d (UI): rota `/sessions` ("Sessões" no menu, antes de Configurações) —
`SessionsPage` consome o `GET /auth/sessions` real (badge "Este dispositivo"
em `isCurrent`, criada em, última atividade, expira em + teto absoluto) com
revogação por linha e "Sair de todos os outros", `window.confirm` antes de
cada ação e estados de carregamento/erro/retry. Revogar a sessão atual derruba
o cookie no backend e a tela acompanha na hora (`signedOut` → `/login`) em vez
de esperar o 401 do próximo request. É a primeira página do dashboard que
consome API real (as demais seguem o mock do F14) e o antiforgery dos DELETE
vem do client (ADR-017).

## Modelo

```text
User → Role → nível de acesso (RoleAccess)
```

## Recursos

- Bootstrap Admin.
- Invitation.
- Accept/Reject.
- Password Change.
- Password Reset.
- Disable/Enable.
- External Login.
- Audit.
- Session control.

## Convite

```text
Admin
→ Invitation (POST /admin/invitations)
→ IIdentityEmailSender
→ Email (link de aceite, token de uso único)
→ Accept/Reject
```

F1-T017 (decisão do usuário, 2026-09-29): o convite é enviado por e-mail
pelo MESMO canal do forgot-password — o handler chama
`IIdentityEmailSender.SendInvitationAsync` após salvar. A origem do link vem
da config `Frontend:BaseUrl` (decisão do usuário, 2026-10-02): é o servidor
que declara a origem, nunca o cliente — um `BaseUrl` vindo do request de um
anônimo permitiria apontar o token para um site atacante. Vazia, a origem
cai para `Request.Scheme://Request.Host` (produção: o nginx serve SPA e API
no mesmo host, então a origem da request é também a do frontend); preenchida
em `appsettings.Development.json` para `http://localhost:3000`, porque a API
vista pelo proxy do vite é `localhost:7237`, que não serve o SPA. O
adaptador atual é o de logging (`LoggingIdentityEmailSender`); trocar por um
provider real (SMTP/API) é pendência separada que não toca Application. O
link é `{origem}/invitations/{id}?token=` — a página pública
`/invitations/{id}` do dashboard (`InvitationAcceptancePage`) lê o token da
query e executa accept → set-password | reject (o link antigo batia na API:
GET em rota só-POST respondia 405 e não existia página). Token URL-safe
(base64url), o mesmo que continua na resposta da API (copy-link). Falha de
envio desfaz a linha (TransactionBehavior) e o admin pode recriar sem
esbarrar em "convite pendente". O caminho Outbox → RabbitMQ → Notification
Worker segue sendo o das notificações (F9): o convidado ainda não é usuário,
então o convite não passa por lá. Pendência restante desta frente: o
provider real de e-mail.

Status:

- Pending
- Accepted
- Rejected
- Expired
- Cancelled
- Activated

Token deve ser aleatório, temporário, single-use e armazenado somente como hash.

### F1-T013 — Guard de ativação implementada

Regra: login, login social (quando existir) e recuperação de senha só são permitidos para usuários **ativos**. O único passo que ativa uma conta criada por convite é a definição de senha.

```text
Auto-registro (POST /register) ─► conta INATIVA ─► (ativação manual do admin)
Convite Pending ─► Accept ─► provisiona conta INATIVA ─► set-password ─► conta ATIVA (Activated)
                  └► Reject ─► nenhuma conta criada
```

- `User.CreatePending` cria o agregado com `IsActive = false`; `User.Create` continua ativo (bootstrap do Admin).
- `AcceptInvitationCommand` chama a porta `IInvitationAccountPort.ProvisionInactiveAccountAsync` e provisiona domain + Identity **sem senha**, inativamente.
- `POST /api/v1/auth/invitations/{id}/set-password` (`SetInvitationPasswordCommand`) valida o token via `Invitation.EnsureActivatable`, define a senha, chama `User.Enable` e move o convite para `Activated` (single-use).
- `RequestPasswordResetAsync` e `ResetPasswordAsync` verificam `IsActive`; a resposta anti-enumeração permanece idêntica para email desconhecido e inativo.
- `LoginAsync` e `RefreshAsync` já exigiam `IsActive` (mantido).
- O login social Google (F1-T014) reutiliza a mesma regra: só entra quem já tem
  conta ativa com o e-mail informado, sem provisionar e sem vincular identidade
  externa automaticamente.

### F1-T014 — Login social Google implementado

> **Desde a F43 (ADR-064)** o fluxo PRINCIPAL é authorization code com
> callback no backend — ver a próxima seção. O Google Identity Services (GSI)
> com `id_token` continua registrado no `POST /api/v1/auth/google` como
> **fallback/rollback** do fluxo novo.

Fluxo original (GSI): **Google Identity Services** no browser, com o `id_token`
enviado ao backend. Não há redirect de authorization code para a API, portanto
também não existe callback de sessão: o "callback" é o próprio `POST /api/v1/auth/google`,
que só cria sessão depois de validar a credencial no servidor.

```text
Browser: GoogleLogin (GSI, useOneTap=false) → id_token
   → POST /api/v1/auth/google { "idToken": "..." }
   → GoogleIdentityTokenValidator (GoogleJsonWebSignature)
        · assinatura contra as chaves públicas do Google (JWKS)
        · aud == Authentication:Google:ClientId
        · iss = accounts.google.com | https://accounts.google.com
        · exp/nbf com tolerância de 1 minuto
        · email_verified == true, email e sub obrigatórios
        · allow-list opcional de domínio (Authentication:Google:AllowedDomains)
   → e-mail normalizado → UserManager.FindByEmailAsync
        · e-mail desconhecido OU DomainUser ausente OU !IsActive → AccountUnavailable
        · sem provisionamento, sem autoativação, sem vínculo externo automático
   → StartSessionAsync (mesmo cookie + refresh session do login por senha)
   → { "user": { "id", "email", "displayName", "roles" }, "refreshToken", "expiresAt" }
```

Respostas do endpoint (todas com o envelope `ApiResponse`):

| Situação | Status | Mensagem |
|---|---|---|
| `ClientId` ausente | `503` | Login com Google não está configurado. |
| `id_token` inválido, expirado, assinatura/Aud/emissor/e-mail verificado inválidos | `401` | Credencial do Google inválida. |
| E-mail sem conta ou conta inativa | `403` | Conta não encontrada ou inativa. Fale com o administrador. |

Regras de segurança aplicadas:

- **Anti-enumeração**: e-mail desconhecido e conta inativa produzem a mesma falha
  (`AccountUnavailable`) e a mesma mensagem; nada de token, e-mail ou motivo é
  registrado em log.
- **Client Secret**: o fluxo GSI em si não usa (superfície de segredo zero no
  caminho antigo). Desde a F43 o `Authentication:Google:ClientSecret` existe,
  mas **só** na troca `code → token` dentro do servidor (ver seção F43).
- **Frontend nunca é fonte de verdade**: roles e `email_verified` do token não
  autorizam nada; quem autoriza é o backend (claims emitidas na sessão + policies).
- **Entrada não confiável**: `GoogleLoginViewModel` limita o `id_token` a 8192
  caracteres e exige preenchimento (erro em pt-BR, F0-T023).
- **Rate limit** na categoria `Login` e auditoria de sucesso com o `UserId` (sem e-mail).
- O claim `displayName` foi adicionado ao principal (claim própria —
  `ClaimTypes.Name` continua sendo o e-mail, usado por `ICurrentUserContext`/auditoria).

Configuração (ver `.env.example` e `src/InfraControl.Api/appsettings.json`):

```bash
Authentication__Google__ClientId=...            # público, igual ao VITE_GOOGLE_CLIENT_ID
# Authentication__Google__AllowedDomains__0=empresa.com.br   # opcional
```

Testes: `tests/InfraControl.UnitTests/Identity/GoogleExternalLoginTests.cs`
(19 casos) cobrem provider não configurado, token inválido, conta desconhecida
sem provisionamento, conta inativa, conta ativa, normalização de e-mail,
mapeamento HTTP sem enumeração, cookie/envelope de sucesso, allow-list de
domínio (incluindo entrada vazia) e limite de tamanho da credencial.

### F43 — Login Google via authorization code com callback no backend (ADR-064)

Fluxo principal desde a F43 (pedido do usuário em 2026-10-24: trocar o
callback para o backend). A troca de credenciais acontece **inteiramente no
servidor**; o browser só navega.

```text
GET /api/v1/auth/google                      (start, rate limit Login)
   → state: 32 bytes aleatórios em base64url
   → cookie .InfraControl.GoogleState (HttpOnly, Secure, SameSite=Lax, 10 min)
   → 302 consent screen (response_type=code, scope openid email profile)

GET /api/v1/auth/google/callback?code&state  (atrás do nginx location /api)
   → state conferido com o cookie (FixedTimeEquals) e DELETADO (one-time)
   → GoogleTokenClient: POST form-urlencoded no TokenEndpoint do Google
        · code + client_id + client_secret + redirect_uri + grant_type
        · extrai só o `id_token` (corpo/log: apenas status HTTP)
   → LoginWithGoogleAsync — MESMA porta do fluxo GSI:
        · GoogleIdentityTokenValidator (JWKS/aud/iss/exp/email_verified/AllowedDomains)
        · conta já existe e está ativa (sem provisionamento, F1-T013)
   → sucesso: SignIn cookie Application → 302 {Frontend:BaseUrl}/
   → falha operacional (state/troca/provider/credencial):
        302 {Frontend:BaseUrl}/login?error=google
        · genérico de propósito: browser não distingue o motivo
          (anti-enumeração) e não vê code/token
   → conta indisponível (inexistente OU inativa):
        302 {Frontend:BaseUrl}/login?error=google&reason=account
        · na URL vai só o CÓDIGO — texto livre seria injetável
        · a tela chama GET /api/v1/auth/google/failure?reason=account e
          exibe a mensagem do backend:
          "Conta não encontrada ou inativa. Fale com o administrador."
        · mensagem única para os dois casos: não revela se a conta existe
```

`GET /api/v1/auth/google/failure?reason=` (anônimo) é a **fonte única** dos
textos de falha do login social: `account` → mensagem de conta indisponível;
qualquer outro/ausente → mensagem genérica. A UI não mantém catálogo de
texto (só fallback para falha de transporte).

Regras de segurança:

- **State CSRF (RFC 6749 §10.12)**: cookie `SameSite=Lax` de propósito — o
  Google volta num redirect top-level cross-site e `Strict` NÃO seria enviado;
  comparação em tempo fixo; `state` é de um só uso (deletado no callback).
- **Client Secret nunca sai do servidor**: não aparece em URL, resposta ou
  bundle (`VITE_*` é público; o segredo só em `.env`/env do container).
- **Sessão continua sendo do backend**: o id_token trocado passa pela MESMA
  validação JWKS e pela regra de conta ativa do F1-T013 — nada de confiar no
  redirect do Google.
- `GET /auth/google` e `GET /auth/google/callback` respondem redirect (não
  envelope `ApiResponse`): são navegações de browser, não chamadas de API.
- Sem configuração completa (`ClientId`+`ClientSecret`+`RedirectUri`) o start
  responde `503` (mesmo contrato do POST) e o POST GSI continua disponível.

Configuração:

```bash
VITE_GOOGLE_CLIENT_ID=...        # público (flag de visibilidade do link + ClientId da API)
GOOGLE_CLIENT_SECRET=...         # SOMENTE ambiente — nunca commitar
GOOGLE_REDIRECT_URI=https://<host>/api/v1/auth/google/callback   # idêntica no Console do Google
# vira: Authentication__Google__ClientSecret / Authentication__Google__RedirectUri (compose)
```

Testes: `tests/InfraControl.UnitTests/Identity/GoogleCodeFlowTests.cs`
(14 casos — options, URL de autorização, state, 503 sem config, redirect +
cookie do start; callback: state divergente/ausente, sem code, sem config,
troca falhando, 3 falhas de login indistinguíveis, sucesso com sessão).

### F14-T008 — Autenticação do dashboard implementada

- Tela de Login no estilo UniFi com alternância **Senha** / **Google**
  (`LoginPage.tsx`): o link só aparece quando `VITE_GOOGLE_CLIENT_ID` está
  definido. Desde a F43 é um `<a href="/api/v1/auth/google">` (o fluxo é do
  backend — ver seção F43); o popup GSI saiu da tela.
- `ForgotPasswordPage.tsx` para recuperação de senha, exibindo sempre a mesma
  resposta (anti-enumeração) e pedindo o e-mail, sem dizer se existe conta.
- `authSlice` (`app/store/slices/authSlice.ts`) com `status`
  (`checking`/`authenticated`/`anonymous`/`unavailable`) em vez de
  `isAuthenticated`: o estado de sessão nunca é persistido.
- `redux-persist` com whitelist apenas de `user` (nome, e-mail, roles/permissões
  para exibição) — nenhum token, refresh token ou credencial no `localStorage`.
- `SessionGate` revalida com `GET /api/v1/auth/me` a cada boot (após o
  `PersistGate`) e trata falha de rede como `unavailable` com botão "Tentar
  novamente"; rotas privadas dependem do `status`, nunca do `localStorage`.
- `services/api/auth.ts`: `credentials: "include"`, `AuthApiError` com mensagem
  genérica para erro de rede, e **descartar** o `refreshToken` da resposta (o
  cookie HttpOnly é o que mantém a sessão).
- `AppLayout` exibe o usuário real, a role principal e o botão "Sair" (revoga
  a sessão no backend e limpa o estado local).

## Web

Cookie HttpOnly/Secure/SameSite.

## API

A autenticação HTTP atual usa cookie `HttpOnly`, `Secure` e `SameSite=Strict`, junto de refresh sessions persistidas com hash. JWT pode ser adicionado como outro adapter quando houver necessidade de clients não-browser; não é requisito para o dashboard web atual.

## Social login

Google implementado na F1-T014 e migrado ao fluxo **authorization code com
callback no backend** na F43 (ADR-064, ver seções acima): troca `code → token`
somente no servidor + validação de `id_token` no servidor, somente para conta
já ativa; o GSI com `id_token` permanece como fallback. Microsoft fica como
adaptador futuro seguindo a mesma porta (`IGoogleIdentityTokenValidator` →
equivalente), sem alterar a camada Application.

## Segurança

Nunca armazenar senha em texto puro.

O último administrador ativo não deve ser removido ou perder a função administrativa sem existir outro administrador válido.
