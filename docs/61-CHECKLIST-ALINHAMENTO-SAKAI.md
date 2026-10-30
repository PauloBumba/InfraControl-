# Checklist - Alinhamento Layout Sakai PrimeReact

Objetivo: deixar `header`, `sidebar`, `footer` e estrutura geral mais próximos de https://sakai.primereact.org/

## 1) Estrutura do Layout
- [x] Adicionar `.layout-mask` ao `AppLayout.tsx` (igual ao template Sakai)
- [x] Manter `.layout-sidebar-overlay` por compatibilidade
- [x] Garantir hierarquia correta de camadas

## 2) Topbar
- [x] Criar `.layout-topbar-logo-container` com botão do menu + logo InfraControl
- [x] Mover botão hambúrguer para dentro do `logo-container`
- [x] Trocar `.layout-topbar-actions` por `.layout-topbar-menu`
- [x] Ajustar breadcrumb

## 3) CSS
- [x] Adicionar regras para `.layout-mask`
- [x] Adicionar regras para `.layout-topbar-logo-container`, `.layout-topbar-logo`, `.layout-topbar-menu`
- [x] Ajustar `.layout-menu-button`
- [x] Ajustar z-index mobile para casar com `.layout-mask`
- [x] Manter compatibilidade com `.layout-sidebar-overlay`

## 4) Build e Deploy
- [x] Build do frontend
- [x] Copiar `dist/` para container `infracontrol--frontend-1`
- [x] `nginx -s reload`
- [x] Limpar caches (Redis + API)

## Validação
- [x] Build sem erros
- [x] `make front-update` executado com sucesso
- [ ] Testar no browser: abrir/fechar sidebar mobile, clicar fora (mask) fecha, header acessível

**Data:** 2025-10-30
**Status:** Em execução
