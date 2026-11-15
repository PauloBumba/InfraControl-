import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { FullErDiagram } from "./FullErDiagram";
import type { SchemaEntity } from "@/services/api/schema";

const device: SchemaEntity = {
  entityName: "Device",
  tableName: "devices",
  properties: [
    { name: "Id", clrType: "Guid", isNullable: false, isPrimaryKey: true, foreignKeyTarget: null },
    { name: "LocationId", clrType: "Guid", isNullable: false, isPrimaryKey: false, foreignKeyTarget: "Location.Id" },
  ],
};

const location: SchemaEntity = {
  entityName: "Location",
  tableName: "locations",
  properties: [
    { name: "Id", clrType: "Guid", isNullable: false, isPrimaryKey: true, foreignKeyTarget: null },
  ],
};

describe("FullErDiagram", () => {
  it("renderiza todas as tabelas e o relacionamento FK", () => {
    render(<FullErDiagram entities={[device, location]} />);
    expect(screen.getByText("devices")).toBeInTheDocument();
    expect(screen.getByText("locations")).toBeInTheDocument();
    expect(screen.getByText("LocationId→Id")).toBeInTheDocument();
    expect(screen.getByText(/2 tabelas · 1 relacionamentos/)).toBeInTheDocument();
  });
});
