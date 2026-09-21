// API client placeholder
export const apiClient = {
  get: async <T>(_url: string): Promise<T> => {
    throw new Error("Not implemented");
  },
  post: async <T>(_url: string, _data?: unknown): Promise<T> => {
    throw new Error("Not implemented");
  },
  put: async <T>(_url: string, _data?: unknown): Promise<T> => {
    throw new Error("Not implemented");
  },
  delete: async <T>(_url: string): Promise<T> => {
    throw new Error("Not implemented");
  },
};
