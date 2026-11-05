# PLANO MESTRE - MELHORIAS GLOBAIS

Data: 2026-10-30
Objetivo: Executar todas as melhorias solicitadas sem deixar nada passar

## 1) HOME/INDEX
- [ ] Filtrar incidentes resolvidos (não aparecerem na tela principal)
- [ ] Filtrar alertas resolvidos (não aparecerem na tela principal)
- [ ] Filtrar tarefas: mostrar apenas em andamento/abertos na home. Concluídas vão para "Ver concluídas"
- [ ] Botão "Ver tarefas concluídas" / "Ver incidentes resolvidos" / "Ver alertas resolvidos"
- [ ] Botão para excluir item concluído/resolvido direto da listagem

## 2) TAREFAS
- [ ] Adicionar campo "O que foi feito" (descrição de execução/conclusão)
- [ ] Botão "Play/Iniciar" -> muda estado para "Em andamento"
- [ ] Botão "Finalizar/Concluir" -> marca como feita, preenche "O que foi feito" se vazio
- [ ] Ao concluir, tarefa some da tela principal (Home/Tasks lista padrão)
- [ ] Manter histórico de execução
- [ ] Botão "Ver concluídas" na página de Tarefas
- [ ] Permitir excluir tarefa concluída

## 3) INCIDENTES
- [ ] Não exibir incidentes com status `Resolved` na tela principal
- [ ] Criar aba/filtro "Resolvidos"
- [ ] Permitir visualizar e excluir incidentes resolvidos
- [ ] Garantir consistência com tarefas (comportamento igual)

## 4) ALERTAS
- [ ] Não exibir alertas resolvidos na tela principal
- [ ] Criar filtro/aba "Resolvidos"
- [ ] Permitir visualizar e excluir alertas resolvidos

## 5) PDI
- [ ] Melhorar tabela com PrimeReact DataTable (igual à tela de Projetos)
- [ ] Visão mais organizada (filtros, colunas, ações)
- [ ] Melhorar experiência para perfil Admin
- [ ] Remover botão "Relatório" da tela PDI
- [ ] Dados não devem ficar lentos ao carregar (revisar chamadas/cache)

## 6) RELATÓRIOS
- [ ] Criar módulo de relatórios centralizado
- [ ] Permitir gerar relatório personalizado por tipo (Tarefas, Incidentes, Alertas, PDI, etc.)
- [ ] Imprimir com logo da empresa
- [ ] Incluir campo "O que foi feito" nos relatórios
- [ ] Filtros por período, status, responsável
- [ ] Opção de exportar/visualizar antes de imprimir
- [ ] Admin define quais dados devem compor o relatório

## 7) GRID X KANBAN
- [ ] Analisar telas com lista: definir quais ficam em Grid (DataTable) e quais em Kanban
- [ ] Padronizar alternância entre visualizações onde fizer sentido
- [ ] Manter consistência visual com Sakai

## 8) TABELAS GERAIS
- [ ] Padronizar com PrimeReact DataTable
- [ ] Paginação, ordenação, filtros
- [ ] Ações organizadas
- [ ] Responsividade
- [ ] Aplicar em todas as telas com listagem

## 9) CACHE / PERFORMANCE
- [ ] Investigar lentidão ao carregar dados (PDI e outras telas)
- [ ] Revisar React Query/RTK Query/cache keys
- [ ] Revisar Redis (API)
- [ ] Otimizar chamadas em lote
- [ ] Adicionar/ajustar invalidações

## 10) SLA/SLO - RETENÇÃO CONFIGURÁVEL
- [ ] Criar configuração gerenciável (não hardcoded)
- [ ] Permitir admin definir retenção por tabela (dias)
- [ ] Job/serviço para limpeza conforme configuração
- [ ] Interface no Admin para configurar
- [ ] Log de execução da limpeza
- [ ] Garantir segurança (não apagar dados críticos sem regra)

## 11) CAÇA-BUGS
- [ ] Rodar revisão em todas as alterações
- [ ] Corrigir bugs encontrados
- [ ] Validar build frontend + backend
- [ ] Testar fluxos críticos
- [ ] Não deixar nada pendente
