import { useMemo } from "react";
import type { SchemaEntity, SchemaProperty } from "@/services/api/schema";

/**
 * DER (diagrama entidade-relacionamento) da entidade selecionada:
 * caixa central + vizinhança de 1º nível (FKs de saída e de entrada),
 * arestas rotuladas com `coluna → alvo.pk`. SVG puro — sem deps.
 */

interface Edge {
  from: SchemaEntity;
  to: SchemaEntity;
  fromProp: string;
  toProp: string;
}

interface Node {
  entity: SchemaEntity;
  x: number;
  y: number;
  w: number;
  h: number;
}

const BOX_W = 230;
const HEADER_H = 30;
const ROW_H = 16;
const MAX_ROWS = 9;
const CENTER_X = 0;
const CENTER_Y = 0;

function boxHeight(e: SchemaEntity): number {
  const rows = Math.min(e.properties.length, MAX_ROWS);
  const extra = e.properties.length > MAX_ROWS ? 1 : 0;
  return HEADER_H + rows * ROW_H + extra * ROW_H;
}

function parseFkTarget(target: string): { entity: string; prop: string } {
  const idx = target.lastIndexOf(".");
  return idx < 0
    ? { entity: target, prop: "" }
    : { entity: target.slice(0, idx), prop: target.slice(idx + 1) };
}

function buildGraph(entities: SchemaEntity[], center: SchemaEntity) {
  const byName = new Map(entities.map((e) => [e.entityName, e]));
  const neighbors = new Map<string, SchemaEntity>();
  const edges: Edge[] = [];

  // FKs de saída: coluna da central aponta para outra entidade.
  for (const prop of center.properties) {
    if (!prop.foreignKeyTarget) continue;
    const target = parseFkTarget(prop.foreignKeyTarget);
    const entity = byName.get(target.entity);
    if (!entity || entity.entityName === center.entityName) continue;
    neighbors.set(entity.entityName, entity);
    edges.push({ from: center, to: entity, fromProp: prop.name, toProp: target.prop });
  }

  // FKs de entrada: outras entidades apontam para a central.
  for (const entity of entities) {
    if (entity.entityName === center.entityName) continue;
    for (const prop of entity.properties) {
      if (!prop.foreignKeyTarget) continue;
      const target = parseFkTarget(prop.foreignKeyTarget);
      if (target.entity !== center.entityName) continue;
      neighbors.set(entity.entityName, entity);
      edges.push({ from: entity, to: center, fromProp: prop.name, toProp: target.prop });
    }
  }

  const list = [...neighbors.values()];
  const nodes: Node[] = [
    { entity: center, x: CENTER_X, y: CENTER_Y, w: BOX_W, h: boxHeight(center) },
  ];

  if (list.length > 0) {
    const radius = Math.max(300, list.length * 95);
    list.forEach((entity, i) => {
      const angle = (i / list.length) * Math.PI * 2 - Math.PI / 2;
      nodes.push({
        entity,
        x: Math.cos(angle) * radius,
        y: Math.sin(angle) * radius,
        w: BOX_W,
        h: boxHeight(entity),
      });
    });
  }

  return { nodes, edges };
}

function anchor(node: Node, towardX: number, towardY: number) {
  const cx = node.x;
  const cy = node.y;
  const dx = towardX - cx;
  const dy = towardY - cy;
  const hw = node.w / 2;
  const hh = node.h / 2;
  if (dx === 0 && dy === 0) return { x: cx, y: cy - hh };
  const scale = Math.min(
    dx !== 0 ? hw / Math.abs(dx) : Infinity,
    dy !== 0 ? hh / Math.abs(dy) : Infinity,
  );
  return { x: cx + dx * scale, y: cy + dy * scale };
}

function edgePath(a: Node, b: Node) {
  const p1 = anchor(a, b.x, b.y);
  const p2 = anchor(b, a.x, a.y);
  const mx = (p1.x + p2.x) / 2;
  const my = (p1.y + p2.y) / 2;
  return { d: `M ${p1.x} ${p1.y} Q ${mx} ${my} ${p2.x} ${p2.y}`, mid: { x: mx, y: my } };
}

function EntityBox({ node, isCenter, onSelect }: {
  node: Node;
  isCenter: boolean;
  onSelect: (e: SchemaEntity) => void;
}) {
  const { entity, x, y, w, h } = node;
  const rows = entity.properties.slice(0, MAX_ROWS);
  const hidden = entity.properties.length - rows.length;
  return (
    <g
      onClick={() => !isCenter && onSelect(entity)}
      style={{ cursor: isCenter ? "default" : "pointer" }}
    >
      <rect
        x={x - w / 2}
        y={y - h / 2}
        width={w}
        height={h}
        rx={8}
        fill={isCenter ? "#eff6ff" : "#ffffff"}
        stroke={isCenter ? "#3b82f6" : "#cbd5e1"}
        strokeWidth={isCenter ? 2 : 1.2}
      />
      <rect
        x={x - w / 2}
        y={y - h / 2}
        width={w}
        height={HEADER_H}
        rx={8}
        fill={isCenter ? "#3b82f6" : "#f1f5f9"}
      />
      <rect
        x={x - w / 2}
        y={y - h / 2 + HEADER_H - 8}
        width={w}
        height={8}
        fill={isCenter ? "#3b82f6" : "#f1f5f9"}
      />
      <text
        x={x}
        y={y - h / 2 + 19}
        textAnchor="middle"
        fontSize={12}
        fontWeight={700}
        fill={isCenter ? "#ffffff" : "#1e293b"}
      >
        {entity.tableName}
      </text>
      {rows.map((p: SchemaProperty, i: number) => {
        const ry = y - h / 2 + HEADER_H + i * ROW_H + 12;
        return (
          <g key={p.name}>
            {p.isPrimaryKey && (
              <rect x={x - w / 2 + 6} y={ry - 9} width={14} height={11} rx={2} fill="#dbeafe" />
            )}
            {p.isPrimaryKey && (
              <text x={x - w / 2 + 13} y={ry} textAnchor="middle" fontSize={7} fontWeight={700} fill="#1d4ed8">PK</text>
            )}
            <text
              x={x - w / 2 + 24}
              y={ry}
              fontSize={10}
              fill={p.foreignKeyTarget ? "#b45309" : "#475569"}
              fontWeight={p.foreignKeyTarget ? 600 : 400}
            >
              {p.name}
              {p.foreignKeyTarget ? " ⧉" : ""}
            </text>
            {!p.isPrimaryKey && (
              <text x={x + w / 2 - 8} y={ry} textAnchor="end" fontSize={8} fill="#94a3b8">
                {p.clrType}
              </text>
            )}
          </g>
        );
      })}
      {hidden > 0 && (
        <text x={x} y={y + h / 2 - 6} textAnchor="middle" fontSize={9} fill="#94a3b8">
          +{hidden} colunas
        </text>
      )}
    </g>
  );
}

export function ErDiagram({ entities, center, onSelect }: {
  entities: SchemaEntity[];
  center: SchemaEntity;
  onSelect: (e: SchemaEntity) => void;
}) {
  const { nodes, edges } = useMemo(() => buildGraph(entities, center), [entities, center]);
  const nodeByEntity = useMemo(
    () => new Map(nodes.map((n) => [n.entity.entityName, n])),
    [nodes],
  );

  const paths = edges
    .map((edge) => {
      const a = nodeByEntity.get(edge.from.entityName);
      const b = nodeByEntity.get(edge.to.entityName);
      if (!a || !b) return null;
      const { d, mid } = edgePath(a, b);
      const label = `${edge.fromProp} → ${edge.toProp || "?"}`;
      return { d, mid, label };
    })
    .filter((p): p is NonNullable<typeof p> => p !== null);

  const bounds = nodes.reduce(
    (acc, n) => ({
      minX: Math.min(acc.minX, n.x - n.w / 2 - 60),
      maxX: Math.max(acc.maxX, n.x + n.w / 2 + 60),
      minY: Math.min(acc.minY, n.y - n.h / 2 - 40),
      maxY: Math.max(acc.maxY, n.y + n.h / 2 + 40),
    }),
    { minX: Infinity, maxX: -Infinity, minY: Infinity, maxY: -Infinity },
  );
  const vbW = Math.max(400, bounds.maxX - bounds.minX);
  const vbH = Math.max(300, bounds.maxY - bounds.minY);

  return (
    <div style={{ overflow: "auto", maxHeight: 620, background: "#f8fafc", borderRadius: 8 }}>
      <svg
        viewBox={`${bounds.minX} ${bounds.minY} ${vbW} ${vbH}`}
        style={{ width: "100%", minWidth: 480, display: "block" }}
        role="img"
        aria-label={`Diagrama entidade-relacionamento de ${center.entityName}`}
      >
        <defs>
          <marker id="er-arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
            <path d="M0,0 L8,4 L0,8 z" fill="#94a3b8" />
          </marker>
        </defs>
        {paths.map((p, i) => (
          <g key={i}>
            <path d={p.d} fill="none" stroke="#94a3b8" strokeWidth={1.4} markerEnd="url(#er-arrow)" />
            <text
              x={p.mid.x}
              y={p.mid.y - 4}
              textAnchor="middle"
              fontSize={9}
              fill="#64748b"
              style={{ paintOrder: "stroke", stroke: "#f8fafc", strokeWidth: 3 }}
            >
              {p.label}
            </text>
          </g>
        ))}
        {nodes.map((n) => (
          <EntityBox
            key={n.entity.entityName}
            node={n}
            isCenter={n.entity.entityName === center.entityName}
            onSelect={onSelect}
          />
        ))}
      </svg>
    </div>
  );
}
