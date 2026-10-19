# 56 — Checklist de Validação (Caça-bugs Front — Fase 33)

> Data: 2026-10-19 · Ambiente: containers dev (`https://localhost:8445`)
> API: rebuildada com F33 (T001–T003) · Credencial smoke: `admin@example.com`
> Status: **12/12 fixes confirmados** (7 altos + 5 médios do lote); M5 pendência; 34 `manual-review` pendentes de verificação.

## Método

- **H1–H3, H6**: smoke via API real no ambiente de dev (mesmo contrato JSON que o browser usa), incluindo *controle negativo* (payload numérico legado deve ser rejeitado).
- **M6**: checagem no bundle JS servido pelo container rebuildado + testes vitest.
- **H4, H5, M1–M4, M7**: cobertos por testes automatizados (vitest) já verdes em F33-T002/T003 — não repetidos no browser.

## Checklist

### H1 — Convites: editar roles (antes `400`)
- [x] `POST /admin/invitations` com `roles:["Viewer"]` → `isSuccess:true`
- [x] `PUT /admin/invitations/{id}` com `roles:["Admin","Viewer"]` → `200`, roles salvas
- [x] Controle negativo: `roles:[2]` (numérico) → rejeitado (erro de conversão `System.String`)
- [x] `GET` devolve `roles` como **strings**
- [x] Convite de teste deletado (limpeza)

### H2 — Projeto: evidências Validar/Rejeitar (antes `400`)
- [x] Upload de evidência (`multipart`, `type=TestReport` por nome) → `200`
- [x] `PUT .../review` com `{"decision":"Validated"}` → `200`; response devolve `status:"Validated"` (**string**, ADR-061)
- [x] Controle negativo: evidência já revisada rejeitada corretamente
- [x] Evidência de teste deletada (limpeza)

### H3 — Knowledge: trocar status (antes `400`)
- [x] `POST /knowledge/articles` cria artigo (`Draft`)
- [x] `POST .../status` com `{"status":"InReview"}` → `200` (transição por nome do enum)
- [x] Controle negativo: `{"status":2}` (numérico) → rejeitado
- [x] Observação: response ainda devolve `status` **numérico** (legado) — pendência de lote próprio (ADR-061 §4)
- [x] Artigo de teste deletado (limpeza)

### H4 — Devices: loop infinito ao trocar de dispositivo
- [x] Corrigido em F33-T002 (`EMPTY_ITEMS` com identidade estável em `hooks/queries.ts`)
- [x] Coberto por teste automatizado (`DevicesPage.test.tsx`)

### H5 — TaskDetail: `:id` stale / excluir tarefa errada
- [x] Corrigido em F33-T002 (`TaskDetailRoute` com `key={id}` no router)
- [x] Coberto por teste automatizado (`TaskDetailPage.test.tsx`)

### H6 — Switches: botão de porta deve refletir `enabled`, não `linkUp`
- [x] Endpoint ao vivo `GET /network/switches/{id}/unifi-ports` devolve campo **`enabled`** (novo) em 26 portas do switch real (`USW Enterprise 24 PoE`)
- [x] `enabled` e `linkUp` independentes no payload real (porta 2: `enabled:true, linkUp:true`; porta 1: `enabled:true, linkUp:false`)
- [x] Front usa `port.enabled` para `disabled`/rótulo (F33-T002)
- [x] Coberto por teste automatizado (`SwitchesPage.test.tsx` ×2)

### H7 — Incidents: painel cruzado (corrida de estado)
- [x] Corrigido em F33-T002 (`investigatingIdRef` síncrono no guard onSuccess/onError)
- [x] Coberto por teste automatizado (corrida em `IncidentsPage.test.tsx`)

### M1 — DeviceDetail: datasets de abas não-zereados no troca de `:id`
- [x] Corrigido em F33-T003 (`loadDevice` zera `requested.current` + todos os estados de aba)
- [x] Coberto por teste automatizado (`DeviceDetailPage.test.tsx`: results de A somem com B pendente)

### M2 — TaskDetail: `run()` engolia erro (diálogo fechava em falha)
- [x] Corrigido em F33-T003 (`after?: () => void` executado **só no sucesso**; 11 call sites)
- [x] Coberto por teste automatizado (falha preserva diálogo + texto digitado)

### M3 — AiProviders: "Listar modelos" poluía o form errado
- [x] Corrigido em F33-T003 (`models.onSuccess` só escreve se `id === editId`)
- [x] Coberto por teste automatizado (corrida A×B)

### M4 — Notifications: toggle invertido com GET falho
- [x] Corrigido em F33-T003 (inverte o valor **efetivo**, base `?? true`)
- [x] Coberto por teste automatizado (GET falho → clique desliga → `setPreference(channel,false)`)

### M5 — Zabbix "Testar conexão" usa config salva, não a digitada
- [ ] **FORA DO LOTE** (ADR-061 §4): exige endpoint aceitar payload opcional (muda contrato API; credenciais). Task própria pendente.

### M6 — Settings: toggle "Modo Escuro" morto
- [x] Ocultado em F33-T003 (valor continua round-trip na API; theming exige ADR próprio)
- [x] Bundle servido pelo container rebuildado: **0** ocorrências de `Modo Escuro` / `toggleTheme` / `darkMode`
- [x] Coberto por teste automatizado (`SettingsPage.test.tsx`)

### M7 — Dashboard: KPI "em aberto" conta só 5 itens (`pageSize: 5`)
- [ ] Confirmado, **não corrigido** (fora do escopo da T003). Fix barato — candidato a T004.

## Validação técnica dos containers (2026-10-19)

| Item | Resultado |
|---|---|
| `docker compose build api frontend` | ✓ imagens reconstruídas com F33 |
| containers `api`/`frontend` | healthy |
| `POST /auth/login` (admin) | ✓ roles Admin/Operator/Viewer |
| Unit backend (Release) | build 0 erros |

## Pendências remanescentes

1. **M5** — task própria (payload opcional no teste de conexão Zabbix).
2. **M7** — KPI Dashboard (candidato a F33-T004).
3. **34 achados `manual-review`** — verificação 1:1 ainda não feita (loop `--loop` do Bug Hunter).
4. **Response-side legado** — enums numéricos em DTOs de resposta (`KnowledgeArticleDto.Status`, `AdminUserSummary.roles`…): lote próprio (ADR-061 §4).
5. **Commit** — tudo ainda não commitado (F32 renumeração + enum-as-string + docs 54/55/56 + ADR-061 + F33); aguarda autorização.
