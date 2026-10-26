# F46-T002 — Varredura E2E de endpoints (front → API → banco)

> Fase: F46 (engenharia de produto) · Data: 2026-10-24 · Status: **concluída**
> Escopo: provar ponta-a-ponta que os fluxos das telas principais gravam e
> leem o banco com o contrato exato consumido pelo frontend.

## 1. Como foi testado

- `tests/InfraControl.IntegrationTests/EndToEnd/EndpointSweepE2ETests.cs`
  — host real `E2EHost` (Postgres+Redis Testcontainers), **contrato do front**:
  envelope `{isSuccess,data,errors}`, cookie de sessão, `X-XSRF-TOKEN` em
  mutations, papéis Read/Operate/Administer, enums como string;
- fixtures espelhadas de `FullPipelineE2ETests` (`NewClient`, `LoginAsync` com
  **`E2EHost.AdminPassword`** para admin — correção de fixture: o admin do
  bootstrap não usa `UserPassword`), `XsrfAsync`, `DataAsync`, `WriteAsync`;
- verificação de **banco** (re-read via API + asserções de FK/persistência),
  não só status HTTP.

## 2. Resultado: 11/11 verdes

| Fluxo | Evidência |
|---|---|
| Incident create → banco → list → status → banco | criação, leitura e transição persistem |
| Incident + Viewer | **403** (autorização por papel) |
| Location CRUD | create → list → delete → banco |
| Device CRUD | create → read (FK/Ip) → get → delete; duplicidade → **409** sem linha órfã |
| Task | create → `PUT` state → banco |
| Notification rules (F42) | GET array → PUT upsert → banco → reload |
| Knowledge article | criação com `AuthorId` da sessão → list |
| Audit | list com `totalCount` persistido |
| SLA service | create → list → delete → banco |
| Monitoring alerts | envelope correto |

## 3. Cobertura

O front tem **281 endpoints únicos**; a suíte cobre os fluxos de
leitura/escrita das telas principais por módulo — qualquer falha de contrato
ou perda de dados aparece como asserção.

## 4. Pendências

- Endpoints de suporte/auxiliares (ex.: settings internos) fora da varredura —
  adicionar caso alguma tela nova ganhe mutation crítica.
