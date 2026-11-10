import { api } from "./client";

/** Configuração institucional da empresa (spec evolução §3). */
export interface CompanyDepartmentItem {
  id: string;
  name: string;
  responsible: string | null;
}

export interface CompanySettingsItem {
  id: string;
  name: string;
  legalName: string | null;
  cnpj: string | null;
  logoPath: string | null;
  street: string | null;
  number: string | null;
  complement: string | null;
  district: string | null;
  city: string | null;
  state: string | null;
  zipCode: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  departments: CompanyDepartmentItem[];
  updatedAt: string | null;
}

export interface CompanySettingsInput {
  name: string;
  legalName?: string | null;
  cnpj?: string | null;
  logoPath?: string | null;
  street?: string | null;
  number?: string | null;
  complement?: string | null;
  district?: string | null;
  city?: string | null;
  state?: string | null;
  zipCode?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  departments?: { name: string; responsible?: string | null }[] | null;
}

export async function getCompanySettings(): Promise<CompanySettingsItem | null> {
  return api.get<CompanySettingsItem | null>("/company-settings");
}

export async function saveCompanySettings(
  input: CompanySettingsInput,
): Promise<CompanySettingsItem | null> {
  return api.put<CompanySettingsItem | null>("/company-settings", input);
}
