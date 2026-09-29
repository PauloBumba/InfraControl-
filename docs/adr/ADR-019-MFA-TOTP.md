# ADR-019 — MFA TOTP

## Status

Accepted (2026-09-29) — cobre F1-T016(a)

## Contexto

A F1-T016 pede "MFA TOTP como segundo fator no login por senha — chave
criptografada em repouso, códigos de recuperação de uso único, janela de
tolerância curta". Hoje o login é senha única (`IdentityAuthenticationService.
LoginAsync`): senha válida + usuário ativo → `StartSessionAsync` → cookie.

O Credential Store (F11-T006) já estabeleceu o precedente de segredo cifrado
com AES-256-GCM e chave só em env (`AesGcmCredentialProtector`); a sessão já
carrega `sid` e é verificada por request (ADR-018).

## Decisão

**1. Biblioteca `Otp.NET`** — TOTP RFC 6238 (6 dígitos, período 30s), padrão
de mercado da Google Authenticator/Authy. Não rolar HMAC/Base32 próprio: a
biblioteca é pequena, testada e o código próprio aqui é risco de segurança, não
economia.

**2. Tolerância de 1 período (±30s)** por padrão, configurável em
`Mfa:ToleranceSteps`. "Curta" = um passo; relógio desatualizado de usuário não
deve precisar de mais que isso, e tolerância larga abre janela de replay.

**3. Secret cifrado em repouso** — reusa `AesGcmCredentialProtector` (mesma
chave `RemoteAccess:CredentialEncryption:Key`, prefixo `v1:`). O TOTP só é
verificável por quem tem a chave; em DB vazado o secret não serve.

**4. Persistência — tabela `user_mfa` (1:1 do usuário):**

| coluna | papel |
|---|---|
| `user_id` | PK, FK → users (cascade) |
| `secret_encrypted` | AES-GCM do secret Base32 |
| `enabled` | `false` até o `confirm` — setup incompleto não exige código |
| `recovery_codes` | JSON: `[{hash, usedAt?}]` — SHA-256 do código, uso único |
| `confirmed_at` | quando virou obrigatório |

Sem migração de linha para usuários existentes: quem não tem linha `user_mfa`
não tem MFA (opt-in).

**5. Fluxo de enrollment (3 comandos):**

```text
Setup    → secret novo (sempre re-emite, descarta confirm pendente)
           retorna otpauth:// URI + secret p/ QR; enabled=false
Confirm  → valida 1 código TOTP contra o secret pendente → enabled=true
           + gera 10 códigos de recuperação (devolvidos UMA vez)
Disable  → valida código TOTP OU senha → apaga a linha (opt-out completo)
```

`Confirm` com secret ainda não confirmado é a única forma de ligar o MFA —
não existe estado "obrigatório sem estar provisionado".

**6. Login com MFA** — `LoginAsync` ganha `MfaCode?` opcional no
`LoginRequest`:

| situação | resultado |
|---|---|
| sem `user_mfa` / `enabled=false` | fluxo inalterado (senha → sessão) |
| MFA on + senha ok + sem código | falha `"Código MFA obrigatório."` — **sem sessão** |
| MFA on + senha ok + código válido | sessão |
| MFA on + senha ok + código inválido | `"Código MFA inválido."` |
| código é recovery code válido | sessão + marca o código como usado |

Senha inválida continua respondendo `null` como hoje (sem dizer se a conta
existe). A mensagem de "código obrigatório" só aparece **depois** da senha
validada — é o que permite à UI mostrar o campo MFA no passo 2.

**7. Códigos de recuperação** — 10 códigos de 8 caracteres
(`XXXX-XXXX`, Base32), guardados como hash SHA-256; conferência compara por
hash; uso marca `usedAt` (não apaga — auditoria da T016c quer saber quantos
foram gastos).

**8. Endpoints (auto-serviço, qualquer role autenticada — é a conta própria):**

```text
POST /api/v1/auth/mfa/setup      → { secret, otpauthUri }
POST /api/v1/auth/mfa/confirm    → { recoveryCodes[10] }
POST /api/v1/auth/mfa/disable    → body { code? , password } (1 dos dois)
GET  /api/v1/auth/mfa/status     → { enabled, confirmedAt }
```

Os comandos usam `ICurrentUserContext.UserId` (mesmo padrão de
`AddTaskCommentCommand`); sem userId → 401 pt-BR.

## Alternativas descartadas

- **Rolando TOTP próprio** — 30 linhas de RFC 6238 são exatamente o tipo de
  código que parece simples e falha em offset/replay/endianness.
- **Email/SMS como segundo fator** — sem infra de entrega confiável (F9-T005
  Email ainda não existe) e mais lento; TOTP é offline.
- **WebAuthn/passkeys** — mais forte, mas exige hardware/browser flow e não é
  o que a task pede; fica registrado como evolução.
- **Secret em texto no DB** — incompatível com o padrão já estabelecido pelo
  F11-T006.

## Consequências

### Positivas

- Login deixa de ser senha única sem trocar o modelo de sessão.
- Segredo protegido com a mesma chave/política do Credential Store.
- Setup incompleto (`enabled=false`) não destrava ninguém nem quebra login.

### Negativas

- Mais uma tabela + migração no aggregate de identidade.
- O passo extra no login exige mudança na UI (F1-T016d) — enquanto a UI não
  manda `mfaCode`, usuário com MFA habilitado não consegue logar (esperado:
  T016a é backend; habilitar MFA é decisão consciente).

## Critérios de aceite

- [x] Setup → Confirm com TOTP válido liga o MFA; código errado não liga
- [x] Login com MFA on exige código; válido cria sessão, inválido não cria
- [x] Recovery code de uso único: segunda tentativa do mesmo código falha
- [x] Secret nunca aparece em texto em resposta de leitura (só no setup)
- [x] Disable apaga a linha e volta ao login por senha
- [x] Usuário sem `user_mfa` não é afetado (regressão do login atual)

## Pendências (não é esta task)

- (c) auditoria de MFA ativado/desativado/login 2º fator → F1-T016c
- (e) UI de login com campo MFA + tela de sessões → F1-T016d
- Rate limit do endpoint de login com código (o `HttpRateLimit` de login já
  cobre o passo 1)

## Referências

- `src/InfraControl.Infrastructure/Auth/IdentityAuthenticationService.cs` — login
- `src/InfraControl.Infrastructure/RemoteAccess/AesGcmCredentialProtector.cs` — cifra
- `src/InfraControl.Domain/Entities/UserSession.cs` — sessão (ADR-018)
- TASKS.md F1-T016 — escopo de onde este ADR sai
