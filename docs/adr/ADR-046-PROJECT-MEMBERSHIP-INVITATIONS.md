# ADR-046: Convites e autoria de membros do Projeto

- **Status**: aceito
- **Data**: 2026-10-10
- **Tasks**: F28 (Módulo de Projetos)
- **Origem**: decisões do usuário em 2026-10-10

## Contexto

O agregado `Project` já mantém `ProjectMember`, e o módulo Identity já oferece
convites por e-mail com token single-use, onboarding e aceite autenticado.
Faltava relacionar um convite ao projeto e registrar quem pode convidar
participantes sem misturar papéis globais do Identity com papéis da equipe.

## Decisões

1. `Project` passa a persistir `CreatedById`, capturado do usuário autenticado
   no comando de criação. Projetos legados podem manter o campo nulo; neles,
   somente um Administrador do sistema pode criar convites.
2. O convite de projeto reutiliza o agregado `Invitation`, acrescentando o
   contexto do projeto e o `ProjectMemberRole` sem alterar o significado das
   roles globais já existentes.
3. Apenas o criador persistido do projeto ou um Administrador do sistema pode
   enviar convites para sua equipe.
4. Uma conta criada pelo onboarding de convite recebe somente a role global
   `Viewer`, além do papel de equipe selecionado no projeto. O aceite por uma
   conta ativa não altera suas roles globais.
5. Para contas ativas, o e-mail conduz à caixa de convites e o usuário aceita
   autenticado dentro do InfraControl. O aceite confere o e-mail exato da conta.
   Para contas inexistentes ou inativas, o fluxo existente de token e definição
   de senha é reutilizado; o vínculo `ProjectMember` ocorre após a ativação.
6. O `ProjectId` no convite é uma referência entre agregados, sem exclusão
   automática do histórico de convites. O aceite valida que o projeto ainda
   existe antes de associar um membro.
7. O vínculo de projeto não cria uma nova policy global nem altera a leitura
   de projetos, que continua sujeita às roles/policies existentes (ADR-016).
8. Convites pendentes são únicos por e-mail e projeto; o mesmo e-mail pode
   receber convites simultâneos para projetos diferentes.

## Consequências

- Um usuário pode pertencer a múltiplos projetos; cada associação tem papel
  independente (`Leader`, `Member` ou `Reviewer`).
- Aceitar convite para conta ativa não concede nem remove roles globais.
- O convite guarda o estado e a proveniência necessários para concluir o
  onboarding ou o aceite autenticado sem conceder roles globais elevadas.
- Projetos legados sem `CreatedById` permanecem utilizáveis; somente a
  autorização de convite fica restrita ao Admin até que haja autoria registrada.
- A remoção de um projeto não transforma um convite histórico em convite
  global; convites pendentes para projetos removidos não podem ser aceitos.
