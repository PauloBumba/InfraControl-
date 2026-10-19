# AGENTS.md — Regras para agentes de IA

## Regra principal

A IA é aceleradora. A decisão arquitetural continua sob controle humano.

## Antes de qualquer alteração

Ler:

1. `README.md`
2. `PROJECT_STATE.md`
3. `TASKS.md`
4. ADRs relevantes
5. documentação do módulo
6. requisitos da Task

## Processo obrigatório

```text
Ler contexto
→ confirmar Task
→ verificar dependências
→ dividir se estiver grande
→ apresentar plano
→ criar/ajustar teste
→ implementar pequena parte
→ testar
→ revisar diff
→ validar
→ documentar
→ atualizar estado
→ parar no checkpoint
```

## Proibições

- Não inventar requisitos.
- Não alterar arquitetura sem ADR.
- Não executar comando destrutivo sem confirmação.
- Não usar credenciais reais.
- Não esconder testes quebrados.
- Não fazer `git push` automaticamente.
- Não implementar várias Tasks sem autorização.
- Não apagar histórico sem política explícita.
- Não reinventar o Shared Kernel: seguir `docs/24-BUILDING-BLOCKS.md` e o código já existente (Location, PDI, Tasks).

## CQRS / BuildingBlocks (obrigatório em slices novos)

Fonte: `docs/24-BUILDING-BLOCKS.md` + `BuildingBlocksStandardizationTests`. Não inventar contrato novo.

| Elemento | Padrão |
| --- | --- |
| Command | `BaseCommand` / `BaseCommand<T>` + `IAuthorizedRequest` (`RoleAccess`) — **nunca** `IRequest` / `ICommand` cru |
| Query | `BaseQuery<T>` + `IAuthorizedRequest` — **nunca** `IQuery` cru (cache via `CachingBehavior` / `ICacheableQuery`) |
| Invalidação | Via `CacheKeysToInvalidate` do `BaseCommand` + `ICacheInvalidatingRequest` quando precisar limpar cache após mutation — os valores são **tags** resolvidas por `InvalidateTagAsync` (`tagidx:{tag}`); a query cacheável declara a mesma tag em `CacheTags` (docs/08 §Invalidação por tag) — nunca chave bruta e nunca prefix wipe |
| Persistência | Porta `I*Repository` + `*Repository` (auto-registro F0-T024); migração **sem** `--no-build` |
| API | Envelope `ApiResponse`; policies/`RoleAccess` alinhados ao módulo |

Espelhar o módulo irmão (Projetos ↔ PDI, ADR-043) no domínio/fluxo; no CQRS de slices **novos**, usar sempre `BaseCommand` + `BaseQuery`.

## Dividir para conquistar

Se uma Task não puder ser explicada com objetivo, escopo, critérios, testes e evidências claros, ela está grande demais e deve ser dividida.

## Após alterações

Sempre apresentar:

- resumo;
- arquivos alterados;
- testes executados;
- resultado;
- pendências;
- próximo passo.
