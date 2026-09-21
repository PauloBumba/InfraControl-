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
