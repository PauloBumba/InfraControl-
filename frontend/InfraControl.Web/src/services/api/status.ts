import { api } from "./client";

/** Estado de um container no parque (caminho barato — sem stats). */
export interface StatusContainer {
  id: string;
  name: string;
  image: string;
  state: string;
  status: string;
  memoryUsageBytes: number | null;
  cpuPercent: number | null;
  startedAt?: string | null;
  restartCount?: number | null;
  healthStatus?: string | null;
}

/** `GET /api/v1/status/containers` — leitura (qualquer role autenticada). */
export interface DockerStatus {
  available: boolean;
  containers: StatusContainer[];
}

export async function getStatusContainers(): Promise<DockerStatus> {
  const data = await api.get<DockerStatus>("/status/containers");
  return data ?? { available: false, containers: [] };
}
