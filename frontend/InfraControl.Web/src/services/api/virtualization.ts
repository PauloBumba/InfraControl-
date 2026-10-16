import { api } from "./client";

/**
 * F31-T004 — contrato do read-model de virtualização
 * (`GET /api/v1/inventory/virtualization`, F31-T003): totais, grupos com o
 * nome resolvido e a listagem das VMs. Espelha `VirtualizationOverviewDto`;
 * a grafia `vCenter`/`totalVCenters` é o camelCase de `VCenterId`/`TotalVCenters`.
 */

export interface VirtualizationGroupItem {
  deviceId: string;
  name: string;
  virtualMachineCount: number;
}

export interface VirtualMachineOverviewItem {
  id: string;
  name: string;
  ip: string | null;
  hypervisorId: string | null;
  hypervisorName: string | null;
  vCenterId: string | null;
  vCenterName: string | null;
  isOrphan: boolean;
}

export interface VirtualizationOverview {
  totalVirtualMachines: number;
  totalVCenters: number;
  orphanVirtualMachines: number;
  byHypervisor: VirtualizationGroupItem[];
  byVCenter: VirtualizationGroupItem[];
  virtualMachines: VirtualMachineOverviewItem[];
}

/** Visão agregada — read-model em memória, sem paginação. */
export async function getVirtualizationOverview(): Promise<VirtualizationOverview> {
  const data = await api.get<VirtualizationOverview>(
    "/inventory/virtualization",
  );
  return data ?? {
    totalVirtualMachines: 0,
    totalVCenters: 0,
    orphanVirtualMachines: 0,
    byHypervisor: [],
    byVCenter: [],
    virtualMachines: [],
  };
}
