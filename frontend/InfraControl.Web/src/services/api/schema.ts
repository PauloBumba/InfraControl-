import { api } from "./client";

/** Dicionário de dados dinâmico (GET /operations/schema, Administer). */
export interface SchemaProperty {
  name: string;
  clrType: string;
  isNullable: boolean;
  isPrimaryKey: boolean;
  foreignKeyTarget: string | null;
}

export interface SchemaEntity {
  entityName: string;
  tableName: string;
  properties: SchemaProperty[];
}

export async function getSchemaCatalog(): Promise<SchemaEntity[]> {
  const data = await api.get<SchemaEntity[] | null>("/operations/schema");
  return data ?? [];
}
