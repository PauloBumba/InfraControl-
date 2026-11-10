# ADR-066 — Políticas de acesso dinâmicas por módulo (role × módulo × nível)

## Status

Aceito — 2026-11-07.

## Contexto

ADR-016 fixou três níveis canônicos (`Read`/`Operate`/`Administer`) resolvidos
estaticamente por `RoleAccess`: `Viewer` lê tudo, `Operator` opera, `Admin`
administra. O usuário solicitou que o administrador gerencie **pela plataforma**
quais roles visualizam/operam cada área do sistema, sem alterar código —
ex.: "Viewer não vê Projetos", "Operator administra SLA".

## Decisão

Introduzir uma matriz de grants persistida que **sobrepõe** os defaults do
ADR-016, sem alterar nenhum `RequiredRoles`/`[Authorize(Policy=...)]`:

- **`AccessPolicyRule`** (entidade): `Role` (UserRole) + `ModuleKey` +
  `AccessLevel` (`None=0, Read=1, Operate=2, Administer=3`). Tabela esparsa:
  ausência de linha = default histórico (`Viewer→Read`, `Operator→Operate`,
  `Admin→Administer`), portanto o comportamento atual é preservado até o admin
  editar.
- **`AccessModuleCatalog`** (domínio, estático): mapa único módulo → rótulo
  pt-BR, prefixos de rota HTTP e raízes de namespace MediatR. Módulo não
  catalogado → fallback para a checagem legada de roles (fail-safe).
- **Nível exigido**:
  - HTTP: a própria policy (`Operate`→2, `Administer`→3); `[Authorize]` simples
    → Read(1) via middleware.
  - MediatR: derivado de `RequiredRoles` (=={Admin}→3; ⊆{Operator,Admin}→2;
    demais→1).
- **Dois pontos de enforcement, mesma fonte**:
  1. `DynamicAccessHandler` (AuthorizationHandler) nas policies HTTP
     `Operate`/`Administer` — resolve o módulo pelo segmento de rota
     `/api/v1/{modulo}`.
  2. `AuthorizationBehavior` (MediatR) — resolve o módulo pelo primeiro
     segmento do namespace `InfraControl.Application.{Modulo}`.
  3. `AccessPolicyMiddleware` pós-`UseAuthorization` — nega Read para módulos
     com grant `None` (cobre `[Authorize]` simples).
- **Semântica**: qualquer role do usuário com nível ≥ exigido autoriza (OR,
  igual ao overlap atual). `AccessLevel.None` bloqueia até leitura.
- **Cache**: `AccessPolicyService` cacheia o conjunto de regras via
  `ICacheService` (tag `access-policies`); PUT invalida a tag.
- **API admin**: `GET/PUT /api/v1/admin/access-policies` (Administer) —
  matriz módulos × roles com nível efetivo e flag de override. Salvaguarda:
  é proibido rebaixar `Admin` abaixo de `Administer` no módulo `admin`
  (impediria o próprio admin de reverter a mudança pela UI).

## Alternativas consideradas

- **Permissões granulares por ação** (ex.: `projects.delete`): rejeitado —
  multiplicaria o catálogo e exigiria anotar cada request; os 3 níveis já
  cobrem a necessidade declarada ("o que cada role vê/faz por área").
- **Substituir `RequiredRoles` por `RequiredPermission` em cada slice**:
  rejeitado — centenas de requests, alto risco de divergência HTTP×MediatR.
- **Override somente de negação** (admin só revoga): rejeitado — o usuário
  pediu gestão completa (conceder e revogar).

## Consequências

- Comportamento padrão idêntico ao ADR-016 (tabela vazia = defaults).
- Grants excessivos (ex.: `Operator→Administer` em `admin`) passam a valer de
  verdade nos DOIS pontos de enforcement — antes eram impossíveis.
- Módulo desconhecido (rota/namespace fora do catálogo) cai no fallback de
  roles — nunca abre acesso além do legado.
- Workers e demais chamadores MediatR respeitam a mesma matriz.
