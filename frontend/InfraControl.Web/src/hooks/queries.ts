import {
  useMutation,
  useQuery,
  useQueryClient,
  type QueryKey,
  type UseQueryOptions,
} from "@tanstack/react-query";
import type { ApiError } from "@/services/api/client";
import { ApiError as ApiErrorClass } from "@/services/api/client";

/**
 * ADR-030 Fase 5 — camada React Query do dashboard.
 *
 * As páginas deixam de orquestrar `useState + useEffect + load()` na mão e
 * passam a declarar:
 *
 * ```tsx
 * const list = usePagedQuery(["locations"], () => listLocations());
 * const create = useEntityMutation(["locations"], () => listLocations(), (input) => createLocation(input));
 * ```
 *
 * Convenções:
 * - a chave de query SEMPRE começa pelo domínio da entidade
 *   (`["locations"]`, `["devices", deviceId]`) — as mutations invalidam
 *   pelo prefixo, então criar/editar/excluir dispara refetch automático de
 *   TUDO que der para a mesma entidade;
 * - `error` é `ApiError` quando veio do envelope HTTP (a página mostra a
 *   mensagem em `ErrorBanner`); falha de transporte vira `GENERIC_ERROR`.
 */

/** Espelha o `Paged<T>` dos services (items + totalCount). */
export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

type PagedQueryOptions<T> = Omit<
  UseQueryOptions<Paged<T>, Error, Paged<T>, QueryKey>,
  "queryKey" | "queryFn"
>;

/** Lista paginada com cache/chave React Query. */
export function usePagedQuery<T>(
  queryKey: QueryKey,
  fetcher: () => Promise<Paged<T>>,
  options?: PagedQueryOptions<T>
) {
  const query = useQuery<Paged<T>, Error>({
    queryKey,
    queryFn: fetcher,
    ...options,
  });

  return {
    ...query,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
  };
}

type ListQueryOptions<T> = Omit<
  UseQueryOptions<T[], Error, T[], QueryKey>,
  "queryKey" | "queryFn"
>;

/** Lista simples (sem paginação do backend). */
export function useListQuery<T>(
  queryKey: QueryKey,
  fetcher: () => Promise<T[]>,
  options?: ListQueryOptions<T>
) {
  const query = useQuery<T[], Error>({ queryKey, queryFn: fetcher, ...options });

  return {
    ...query,
    items: query.data ?? [],
  };
}

type ObjectQueryOptions<T> = Omit<
  UseQueryOptions<T, Error, T, QueryKey>,
  "queryKey" | "queryFn"
>;

/**
 * Objeto único (snapshot/agregado que não é lista) — ex.:
 * `useObjectQuery(["unifi-insights"], getUnifiInsights)`.
 */
export function useObjectQuery<T>(
  queryKey: QueryKey,
  fetcher: () => Promise<T>,
  options?: ObjectQueryOptions<T>
) {
  return useQuery<T, Error>({ queryKey, queryFn: fetcher, ...options });
}

interface EntityMutationOptions<TData, TVars> {
  /** Prefixo da chave invalidada após o sucesso (ex.: `["locations"]`). */
  invalidate: QueryKey;
  mutationFn: (vars: TVars) => Promise<TData>;
  onSuccess?: (data: TData, vars: TVars) => void;
  onError?: (error: Error, vars: TVars) => void;
}

function useEntityMutationBase<TData, TVars>({
  invalidate,
  mutationFn,
  onSuccess,
  onError,
}: EntityMutationOptions<TData, TVars>) {
  const queryClient = useQueryClient();

  return useMutation<TData, Error, TVars>({
    mutationFn,
    onSuccess: (data, vars) => {
      void queryClient.invalidateQueries({ queryKey: invalidate });
      onSuccess?.(data, vars);
    },
    onError: (error, vars) => onError?.(error, vars),
  });
}

/** Create com invalidação automática da lista. */
export function useCreateMutation<TData, TVars>(
  options: EntityMutationOptions<TData, TVars>
) {
  return useEntityMutationBase(options);
}

/** Update (PUT/PATCH) com invalidação automática da lista. */
export function useUpdateMutation<TData, TVars>(
  options: EntityMutationOptions<TData, TVars>
) {
  return useEntityMutationBase(options);
}

/** Delete com invalidação automática da lista. */
export function useDeleteMutation<TData, TVars>(
  options: EntityMutationOptions<TData, TVars>
) {
  return useEntityMutationBase(options);
}

/** Mensagem pt-BR amigável para o ErrorBanner. */
export function mutationErrorMessage(error: Error | null): string | null {
  if (!error) return null;
  return error.message || "Não foi possível falar com o servidor. Tente novamente.";
}

/** `ApiError` quando o erro veio do envelope HTTP (útil p/ ramificar 403). */
export function asApiError(error: Error | null): ApiError | null {
  return error instanceof ApiErrorClass ? error : null;
}
