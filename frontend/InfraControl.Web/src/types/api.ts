export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/**
 * Envelope padrão da API InfraControl (BuildingBlocks ApiResponse<T>).
 * Toda resposta com corpo do backend usa { isSuccess, data, errors }.
 * Sucesso sem conteúdo continua sendo HTTP 204 sem corpo.
 */
export interface ApiResponse<T> {
  isSuccess: boolean;
  data: T | null;
  errors: string[];
}
