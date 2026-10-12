import { useCallback, useState } from "react";

/**
 * F29-T001B — a máquina de estados da busca no servidor.
 *
 * `termInput` + `term` + `page`, com "aplicar no Enter e voltar para a página 1",
 * estava reimplementada em 5 páginas (Clients, Knowledge, ZabbixHosts, Audit,
 * Tasks) — o mesmo par de estados e os mesmos dois handlers, copiados.
 *
 * A distinção que existe aqui é o motivo de existir o hook: o texto digitado
 * **não é** o termo aplicado. Ele vira filtro quando o usuário manda
 * (Enter/Filtrar); se fosse reativo, cada tecla dispararia uma consulta no
 * servidor.
 *
 * `applyTerm` aceita um valor de fora para as páginas que aplicam busca junto
 * com outros filtros (ex.: ZabbixHosts manda termo + estado + ausência de uma
 * vez só).
 */
export function useServerSearch(initialTerm = "") {
  /** O que está no campo agora (ainda não aplicado). */
  const [input, setInput] = useState(initialTerm);
  /** O que foi aplicado — é o que vai para a API e entra na chave de query. */
  const [term, setTerm] = useState(initialTerm);
  const [page, setPage] = useState(1);

  /** Aplica o texto do campo (Enter/Filtrar) e volta para a primeira página. */
  const applyInput = useCallback(() => {
    setTerm(input.trim());
    setPage(1);
  }, [input]);

  /** Aplica um termo vindo de fora (filtros aplicados juntos) e volta para a primeira página. */
  const applyTerm = useCallback((value: string) => {
    setTerm(value.trim());
    setPage(1);
  }, []);

  /** Limpa campo, termo e página — quem busca não quer mais paginação velha. */
  const clear = useCallback(() => {
    setInput("");
    setTerm("");
    setPage(1);
  }, []);

  return {
    input,
    setInput,
    term,
    page,
    setPage,
    applyInput,
    applyTerm,
    clear,
    hasTerm: term !== "",
  };
}
