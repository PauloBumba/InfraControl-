import { navigation, secondaryNavigation } from "@/data/dashboardData";

/**
 * F29-T005 — a **rota** é a identidade da navegação.
 *
 * Antes, o sidebar identificava item por RÓTULO (texto). Como "Zabbix" existe
 * duas vezes (`navigation` → `/zabbix` e `secondaryNavigation` →
 * `/integrations/zabbix`), três coisas quebravam juntas:
 *
 *  - `labelRouteMap` (`Object.fromEntries`) ficava com o ÚLTIMO → clicar no
 *    "Zabbix" do menu primário levava para `/integrations/zabbix`;
 *  - `getNavItem` acha o PRIMEIRO → ícone/aba de um item errado;
 *  - `activeItem === item.label` acendia OS DOIS ao mesmo tempo.
 *
 * Corrigir renomeando um label não resolve: o próximo duplicado repetiria o
 * bug. Daqui em diante tudo se compara por `item.route`.
 */

/** Achata todos os itens (incluindo sub-itens) para um array de NavItem. */
function flattenItems(): { label: string; route: string }[] {
  const result: { label: string; route: string }[] = [];
  for (const group of [...navigation, ...secondaryNavigation]) {
    for (const item of group.items) {
      result.push({ label: item.label, route: item.route });
      if (item.items) {
        for (const child of item.items) {
          result.push({ label: child.label, route: child.route });
        }
      }
    }
  }
  return result;
}

const allItems = flattenItems();

/** Rota → rótulo (injetivo: nenhuma rota é chave de mais de um item). */
const routeLabelMap: Record<string, string> = Object.fromEntries(
  allItems.map((item) => [item.route, item.label]),
);
routeLabelMap["/"] = "Início";
routeLabelMap["/dashboard"] = "Visão Geral";
// F26-T001 — rota fora do menu (entra pelo card do usuário).
routeLabelMap["/account"] = "Minha Conta";

/** Rótulo → rota. Só para MIGRAR abas antigas; navegar nunca passa por aqui. */
const legacyLabelRouteMap: Record<string, string> = Object.fromEntries(
  allItems.map((item) => [item.label, item.route]),
);

/**
 * Rótulo de trilha para qualquer rota, inclusive as de detalhe (que não estão
 * no menu). É o que aparece em `InfraControl › …` e no texto das abas.
 */
export function labelForRoute(pathname: string): string {
  if (pathname.startsWith("/devices/")) return "Detalhes do Dispositivo";
  if (pathname.startsWith("/tasks/")) return "Detalhe da Tarefa";
  return routeLabelMap[pathname] ?? "Visão Geral";
}

/**
 * Converte o valor guardado no `localStorage` das abas recentes em rota.
 * O formato antigo guardava o RÓTULO; o novo guarda a rota. `null` = não
 * resolve (rótulo que deixou de existir) → o item é descartado na leitura.
 */
export function resolveStoredRoute(value: string): string | null {
  if (typeof value === "string" && value.startsWith("/")) return value;
  return legacyLabelRouteMap[value] ?? null;
}
