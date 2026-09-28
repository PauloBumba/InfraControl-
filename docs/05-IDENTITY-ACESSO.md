# Identity e Acesso

## Base

ASP.NET Core Identity.

### F1-T001 — Fundação implementada

- `InfraControl.Domain.Identity.User` é o agregado de negócio e possui estado ativo, email e roles;
- `UserRole` define `Admin`, `User` e `Viewer`, com atribuição controlada pelo agregado;
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

O gerenciamento de roles segue Vertical Slice/CQRS. Os comandos `AssignUserRoleCommand` e `RemoveUserRoleCommand`, com seus handlers, ficam em `Application/Identity/Roles`; o domínio mantém as invariantes do agregado `User`; e `IUserRoleRepository` é a porta usada pelos handlers. O `IdentityUserRoleRepository` é o adaptador técnico em `Infrastructure`, responsável por sincronizar a role do domínio com o ASP.NET Core Identity em uma transação.

A autorização nos controllers combina dois mecanismos: roles diretas
(`[Authorize(Roles = "...")]`, mapeado da matriz `RolePermissions` —
ADR-008) e **policies HTTP nomeadas por permissão**
(`[Authorize(Policy = "InventoryRead")]` etc.). O controller HTTP apenas
converte a requisição em comando e envia pelo MediatR. A alteração de role
é autorizada por `Roles = "Admin"`, o último administrador ativo não pode
perder a role `Admin` e um usuário não pode ficar sem role.

> **Achado F6-T003 (2026-09-26):** os controllers das fases 2/3/5 já usavam
> `[Authorize(Policy = "InventoryRead")]`, mas só existiam as claims de
> permissão — as policies nomeadas **não estavam registradas**
> (`AddAuthorization()` puro), então esses endpoints falhariam em runtime.
> Corrigido: `PermissionPolicies.AddPermissionPolicies()` registra uma
> policy por `Permission`, exigindo a claim `PermissionClaim.Type` (emitida
> no login). Coberto por `PermissionPolicyTests`.

### F1-T004 — Permissions

O catálogo de permissões fica em `Domain/Authorization/Permission.cs`, sem dependência de ASP.NET Core. A matriz padrão é definida em `RolePermissions`: `Viewer` recebe permissões de leitura; `User` herda as permissões de leitura e recebe permissões de gestão operacional; `Admin` recebe o catálogo completo. A união de múltiplas roles não produz duplicatas.

O adaptador `IdentityUserRoleRepository` sincroniza as claims `permission` após cada alteração de role. O bootstrap do administrador inicial também grava todas as claims de permissão. As claims alimentam o `AuthorizationBehavior` do pipeline MediatR (2ª camada nos handlers) **e** as policies HTTP nomeadas (achado F6-T003 acima); os controllers também podem autorizar por role direta (ADR-008). A claim deve ser renovada quando a sessão for reemitida; auditoria e concessões customizadas continuam fora do escopo desta Task.

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
| `POST /api/v1/auth/google` | Valida `id_token` do Google e cria sessão para conta já ativa | Anônimo |
| `GET /api/v1/auth/me` | Retorna usuário, nome de exibição, roles e permissões | Autenticado |

A área administrativa possui `GET /api/v1/admin/users`, alteração de status e atribuição/remoção de roles. Os endpoints autorizam por role: leitura com `Roles = "Admin,User"`, gestão com `Roles = "Admin"`. O sender de email atual é um adaptador de logging; SMTP/provider deve ser conectado por `IIdentityEmailSender` sem alterar Application.

## Modelo

```text
User → Role → Permission
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
→ Invitation
→ Outbox
→ RabbitMQ
→ Notification Worker
→ Email
→ Accept/Reject
```

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

Fluxo escolhido: **Google Identity Services (GSI)** no browser, com o `id_token`
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
| `ClientId`/`ClientSecret` ausentes | `503` | Login com Google não está configurado. |
| `id_token` inválido, expirado, assinatura/Aud/emissor/e-mail verificado inválidos | `401` | Credencial do Google inválida. |
| E-mail sem conta ou conta inativa | `403` | Conta não encontrada ou inativa. Fale com o administrador. |

Regras de segurança aplicadas:

- **Anti-enumeração**: e-mail desconhecido e conta inativa produzem a mesma falha
  (`AccountUnavailable`) e a mesma mensagem; nada de token, e-mail ou motivo é
  registrado em log.
- **Sem Client Secret no frontend**: o segredo vive só em
  `Authentication:Google:ClientSecret` (variável de ambiente/secret manager) e é
  usado apenas para registrar o handler `AddGoogle`; a validação do `id_token`
  não depende dele.
- **Frontend nunca é fonte de verdade**: roles e `email_verified` do token não
  autorizam nada; quem autoriza é o backend (claims emitidas na sessão + policies).
- **Entrada não confiável**: `GoogleLoginViewModel` limita o `id_token` a 8192
  caracteres e exige preenchimento (erro em pt-BR, F0-T023).
- **Rate limit** na categoria `Login` e auditoria de sucesso com o `UserId` (sem e-mail).
- `AddGoogle` só é registrado quando `ClientId` **e** `ClientSecret` existem, e o
  claim `displayName` foi adicionado ao principal (claim própria — `ClaimTypes.Name`
  continua sendo o e-mail, usado por `ICurrentUserContext`/auditoria).

Configuração (ver `.env.example` e `src/InfraControl.Api/appsettings.json`):

```bash
Authentication__Google__ClientId=...            # público, igual ao VITE_GOOGLE_CLIENT_ID
Authentication__Google__ClientSecret=...         # segredo, somente backend
# Authentication__Google__AllowedDomains__0=empresa.com.br   # opcional
```

Testes: `tests/InfraControl.UnitTests/Identity/GoogleExternalLoginTests.cs`
(19 casos) cobrem provider não configurado, token inválido, conta desconhecida
sem provisionamento, conta inativa, conta ativa, normalização de e-mail,
mapeamento HTTP sem enumeração, cookie/envelope de sucesso, allow-list de
domínio (incluindo entrada vazia) e limite de tamanho da credencial.

### F14-T008 — Autenticação do dashboard implementada

- Tela de Login no estilo UniFi com alternância **Senha** / **Google**
  (`LoginPage.tsx`): o botão só aparece quando `VITE_GOOGLE_CLIENT_ID` está
  definido, e o provedor do Google é montado por página (`useOneTap={false}`).
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

Google implementado na F1-T014 (ver seção acima): GSI + validação de `id_token`
no servidor, somente para conta já ativa. Microsoft fica como adaptador futuro
seguindo a mesma porta (`IGoogleIdentityTokenValidator` → equivalente), sem
alterar a camada Application.

## Segurança

Nunca armazenar senha em texto puro.

O último administrador ativo não deve ser removido ou perder a função administrativa sem existir outro administrador válido.
