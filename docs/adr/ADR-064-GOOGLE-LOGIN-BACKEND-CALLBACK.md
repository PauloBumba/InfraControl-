# ADR-064 — Login Google via authorization code com callback no backend

- **Status**: **aceito pelo usuário em 2026-10-24 (F43); implementação em andamento**
- **Revisões**:
  - 2026-10-24 — **rev. 1**: falha "conta indisponível" deixa de ser
    indistinguível. O callback leva `reason=account` e a UI exibe a
    mensagem do backend via `GET /auth/google/failure` (decisão do usuário:
    o usuário precisa saber que deve falar com o administrador). Os demais
    motivos continuam genéricos; a mensagem de conta indisponível continua
    única para conta inexistente e inativa (anti-enumeração preservada).
- **Data**: 2026-10-24
- **Relacionado**: F1-T014 (decisão anterior GSI id_token), ADR-017, ADR-018, ADR-019; docs/05 §F1-T014

## Contexto

O login social Google existe desde a F1-T014 no fluxo **Google Identity
Services (id_token)**: o popup do Google devolve um `id_token` assinado, o
frontend envia para `POST /auth/google` e o backend valida assinatura
(JWKS), audiência, issuer e `email_verified` antes de criar a sessão. O
projeto foi desenhado **sem Client Secret** (docs/05: "a API nunca troca
authorization code por token").

Em 2026-10-24 o usuário pediu para **habilitar o login social e trocar o
callback para o backend**, como prática preferida para app com sessão no
servidor: a troca de credenciais deve acontecer fora do navegador e o
backend é quem emite a sessão.

## Proposta de decisão

Migrar o fluxo principal para **authorization code (OAuth 2.0) com
callback no backend**, mantendo o endpoint antigo como fallback:

1. `GET /auth/google` (start) — emite `state` CSRF (32 bytes aleatórios,
   base64url) em cookie **HttpOnly + SameSite=Lax + Secure** (10 min) e
   redireciona para o consent screen (`response_type=code`, scope
   `openid email profile`).
2. `GET /auth/google/callback` — valida o `state` contra o cookie, troca
   `code` por token **somente no servidor** (`https://oauth2.googleapis.com/token`
   com o Client Secret), valida o `id_token` resultante com o **mesmo**
   validador JWKS (aud/iss/exp/email_verified/allow-list de domínio) e
   chama o **mesmo** `LoginWithGoogleAsync` — regra de conta ativa e
   anti-enumeração idênticas (F1-T013). Sucesso emite a sessão e redireciona
   ao frontend; falha redireciona com `?error=google` (sem detalhar).
   **Rev. 1**: falha `AccountUnavailable` acrescenta `&reason=account`; a UI
   busca o texto em `GET /auth/google/failure?reason=` (a mensagem não vive
   no SPA).
3. `POST /auth/google` (GSI id_token) **permanece** como fallback/rollback
   — continua validado pelo mesmo serviço.
4. Configuração nova em `Authentication:Google`: `ClientSecret` e
   `RedirectUri` (cadastrada no Console do Google). Segredo **somente** em
   `.env`/ambiente do container — nunca em log, URL ou bundle.

## Consequências

- **O browser não participa da troca de credenciais**; a página deixa de
  carregar a biblioteca do Google para o botão (vira redirect simples).
- O cookie de `state` precisa ser `SameSite=Lax` (não `Strict`): o Google
  volta num redirect top-level cross-site e `Strict` não seria enviado.
- O Client ID continua público (é o identificador da aplicação); o bundle
  só o usa como *flag* de visibilidade do botão.
- O Console do Google exige a redirect URI
  `https://<host>/api/v1/auth/google/callback` cadastrada exatamente.
- Se o projeto Google estiver em modo "acesso restrito à organização", o
  login de fora da organização exige publicação/verificação da tela de
  consentimento (dependência externa, não do código).
