import { render, screen, fireEvent } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ErDiagram } from "./ErDiagram";
import type { SchemaEntity } from "@/services/api/schema";

const device: SchemaEntity = {
  entityName: "Device",
  tableName: "devices",
  properties: [
    { name: "Id", clrType: "Guid", isNullable: false, isPrimaryKey: true, foreignKeyTarget: null },
    { name: "LocationId", clrType: "Guid", isNullable: false, isPrimaryKey: false, foreignKeyTarget: "Location.Id" },
    { name: "Name", clrType: "String", isNullable: false, isPrimaryKey: false, foreignKeyTarget: null },
  ],
};

const location: SchemaEntity = {
  entityName: "Location",
  tableName: "locations",
  properties: [
    { name: "Id", clrType: "Guid", isNullable: false, isPrimaryKey: true, foreignKeyTarget: null },
    { name: "Name", clrType: "String", isNullable: false, isPrimaryKey: false, foreignKeyTarget: null },
  ],
};

const rack: SchemaEntity = {
  entityName: "Rack",
  tableName: "racks",
  properties: [
    { name: "Id", clrType: "Guid", isNullable: false, isPrimaryKey: true, foreignKeyTarget: null },
    { name: "DeviceId", clrType: "Guid", isNullable: true, isPrimaryKey: false, foreignKeyTarget: "Device.Id" },
  ],
};

describe("ErDiagram", () => {
  it("renderiza a entidade central e as vizinhas com arestas FK", () => {
    render(
      <ErDiagram entities={[device, location, rack]} center={device} onSelect={() => {}} />,
    );
    expect(screen.getByText("devices")).toBeInTheDocument();
    expect(screen.getByText("locations")).toBeInTheDocument();
    expect(screen.getByText("racks")).toBeInTheDocument();
    // aresta de saída: LocationId → Id
    expect(screen.getByText("LocationId → Id")).toBeInTheDocument();
    // aresta de entrada: DeviceId → Id
    expect(screen.getByText("DeviceId → Id")).toBeInTheDocument();
  });

  it("clique na vizinha dispara onSelect para recentralizar", () => {
    const onSelect = vi.fn();
    render(
      <ErDiagram entities={[device, location, rack]} center={device} onSelect={onSelect} />,
    );
    fireEvent.click(screen.getByText("locations"));
    expect(onSelect).toHaveBeenCalledWith(location);
  });
});
