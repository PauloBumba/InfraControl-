# Regras de Banco

- PostgreSQL é source of truth.
- Usar constraints para integridade.
- Evitar FK polimórfica quando relações específicas forem possíveis.
- Migrations versionadas.
- Transactions para mudanças relacionadas.
- `FOR UPDATE SKIP LOCKED` para workers concorrentes quando aplicável.
- Índices devem ser criados com base em consultas reais.
- Não apagar histórico sem política de retenção.
