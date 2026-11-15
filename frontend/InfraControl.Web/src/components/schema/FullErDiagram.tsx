import { useMemo, useState } from "react";
import dagre from "@dagrejs/dagre";
import { Button } from "@/components/ui/Button";
import type { SchemaEntity } from "@/services/api/schema";
import { ErEntityTip, useErTooltip } from "./ErTooltip";

/**
 * DER completo estilo MySQL Workbench: TODAS as entidades e TODOS os
 * relacionamentos FK num único grafo em camadas (dagre, rankdir=LR).
 * Arestas são agrupadas por par de entidades — o rótulo lista as colunas
 * FK → PK envolvidas. Zoom por botões + scroll nativo do container.
 */

const BOX_W = 240;
const HEADER_H = 30;
const ROW_H = 15;
const MAX_ROWS = 14;

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

interface PlacedNode {
  entity: SchemaEntity;
  x: number;
  y: number;
  w: number;
  h: number;
}

interface GroupedEdge {
  from: string;
  to: string;
  labels: string[];
}

export function FullErDiagram({ entities }: { entities: SchemaEntity[] }) {
  const [zoom, setZoom] = useState(1);
  const { tip, handlers: tipHandlers, boxRef } = useErTooltip();

  const { nodes, edges, width, height } = useMemo(() => {
    const byName = new Map(entities.map((e) => [e.entityName, e]));
    const g = new dagre.graphlib.Graph();
    g.setGraph({ rankdir: "LR", nodesep: 24, ranksep: 90, marginx: 20, marginy: 20 });
    g.setDefaultEdgeLabel(() => ({}));

    for (const e of entities) {
      g.setNode(e.entityName, { width: BOX_W, height: boxHeight(e) });
    }

    // Aresta por PAR de entidades (FKs múltiplas entre o mesmo par = 1 linha).
    const edgeMap = new Map<string, GroupedEdge>();
    for (const e of entities) {
      for (const p of e.properties) {
        if (!p.foreignKeyTarget) continue;
        const target = parseFkTarget(p.foreignKeyTarget);
        const dest = byName.get(target.entity);
        if (!dest || dest.entityName === e.entityName) continue;
        const key = `${e.entityName}→${dest.entityName}`;
        const existing = edgeMap.get(key);
        const label = `${p.name}→${target.prop || "?"}`;
        if (existing) existing.labels.push(label);
        else edgeMap.set(key, { from: e.entityName, to: dest.entityName, labels: [label] });
      }
    }

    const grouped = [...edgeMap.values()];
    for (const edge of grouped) {
      if (g.hasNode(edge.from) && g.hasNode(edge.to)) {
        g.setEdge(edge.from, edge.to, { weight: edge.labels.length });
      }
    }

    dagre.layout(g);

    const placed: PlacedNode[] = entities.map((e) => {
      const pos = g.node(e.entityName);
      return { entity: e, x: pos.x, y: pos.y, w: BOX_W, h: boxHeight(e) };
    });

    const graph = g.graph();
    return {
      nodes: placed,
      edges: grouped,
      width: graph.width ?? 1200,
      height: graph.height ?? 800,
    };
  }, [entities]);

  const nodeByName = useMemo(
    () => new Map(nodes.map((n) => [n.entity.entityName, n])),
    [nodes],
  );

  const PAD = 40;

  return (
    <div>
      <div className="flex align-items-center gap-2" style={{ marginBottom: 8 }}>
        <Button variant="ghost" onClick={() => setZoom((z) => Math.min(2, +(z + 0.25).toFixed(2)))} disabled={zoom >= 2}>
          <i className="pi pi-search-plus" /> Zoom +
        </Button>
        <Button variant="ghost" onClick={() => setZoom((z) => Math.max(0.25, +(z - 0.25).toFixed(2)))} disabled={zoom <= 0.25}>
          <i className="pi pi-search-minus" /> Zoom −
        </Button>
        <Button variant="ghost" onClick={() => setZoom(1)}>Ajustar</Button>
        <span style={{ fontSize: 11, color: "#94a3b8" }}>
          {entities.length} tabelas · {edges.length} relacionamentos · role para navegar
        </span>
      </div>
      <div
        ref={boxRef}
        style={{
          position: "relative",
          overflow: "auto",
          maxHeight: 640,
          background: "#f8fafc",
          borderRadius: 8,
          border: "1px solid #e5e7eb",
        }}
      >
        <ErEntityTip tip={tip} />
        <svg
          width={(width + PAD * 2) * zoom}
          height={(height + PAD * 2) * zoom}
          viewBox={`0 0 ${width + PAD * 2} ${height + PAD * 2}`}
          style={{ display: "block" }}
          role="img"
          aria-label="Diagrama entidade-relacionamento completo"
        >
          <defs>
            <marker id="full-er-arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
              <path d="M0,0 L8,4 L0,8 z" fill="#94a3b8" />
            </marker>
          </defs>
          <g transform={`translate(${PAD},${PAD})`}>
            {/* arestas primeiro (ficam atrás das caixas) */}
            {edges.map((edge) => {
              const a = nodeByName.get(edge.from);
              const b = nodeByName.get(edge.to);
              if (!a || !b) return null;
              const x1 = a.x + a.w / 2;
              const y1 = a.y;
              const x2 = b.x - b.w / 2;
              const y2 = b.y;
              const mx = (x1 + x2) / 2;
              return (
                <g key={`${edge.from}-${edge.to}`}>
                  <path
                    d={`M ${x1} ${y1} C ${mx} ${y1}, ${mx} ${y2}, ${x2} ${y2}`}
                    fill="none"
                    stroke="#94a3b8"
                    strokeWidth={1.2}
                    markerEnd="url(#full-er-arrow)"
                  />
                  <text
                    x={mx}
                    y={(y1 + y2) / 2 - 4}
                    textAnchor="middle"
                    fontSize={8.5}
                    fill="#64748b"
                    style={{ paintOrder: "stroke", stroke: "#f8fafc", strokeWidth: 3 }}
                  >
                    {edge.labels.slice(0, 3).join(", ")}
                    {edge.labels.length > 3 ? ` +${edge.labels.length - 3}` : ""}
                  </text>
                </g>
              );
            })}
            {/* caixas por cima */}
            {nodes.map((n) => {
              const rows = n.entity.properties.slice(0, MAX_ROWS);
              const hidden = n.entity.properties.length - rows.length;
              return (
                <g key={n.entity.entityName} {...tipHandlers(n.entity)}>
                  <rect
                    x={n.x - n.w / 2}
                    y={n.y - n.h / 2}
                    width={n.w}
                    height={n.h}
                    rx={6}
                    fill="#ffffff"
                    stroke="#cbd5e1"
                    strokeWidth={1}
                  />
                  <rect
                    x={n.x - n.w / 2}
                    y={n.y - n.h / 2}
                    width={n.w}
                    height={HEADER_H}
                    rx={6}
                    fill="#f1f5f9"
                  />
                  <rect
                    x={n.x - n.w / 2}
                    y={n.y - n.h / 2 + HEADER_H - 6}
                    width={n.w}
                    height={6}
                    fill="#f1f5f9"
                  />
                  <text
                    x={n.x}
                    y={n.y - n.h / 2 + 19}
                    textAnchor="middle"
                    fontSize={11}
                    fontWeight={700}
                    fill="#1e293b"
                  >
                    {n.entity.tableName}
                  </text>
                  {rows.map((p, i) => {
                    const ry = n.y - n.h / 2 + HEADER_H + i * ROW_H + 12;
                    return (
                      <g key={p.name}>
                        {p.isPrimaryKey && (
                          <rect x={n.x - n.w / 2 + 5} y={ry - 8} width={13} height={10} rx={2} fill="#dbeafe" />
                        )}
                        {p.isPrimaryKey && (
                          <text x={n.x - n.w / 2 + 11.5} y={ry} textAnchor="middle" fontSize={6.5} fontWeight={700} fill="#1d4ed8">PK</text>
                        )}
                        <text
                          x={n.x - n.w / 2 + 22}
                          y={ry}
                          fontSize={9.5}
                          fill={p.foreignKeyTarget ? "#b45309" : "#475569"}
                          fontWeight={p.foreignKeyTarget ? 600 : 400}
                        >
                          {p.name}
                          {p.foreignKeyTarget ? " ⧉" : ""}
                        </text>
                        {!p.isPrimaryKey && (
                          <text x={n.x + n.w / 2 - 6} y={ry} textAnchor="end" fontSize={7.5} fill="#94a3b8">
                            {p.clrType}
                          </text>
                        )}
                      </g>
                    );
                  })}
                  {hidden > 0 && (
                    <text x={n.x} y={n.y + n.h / 2 - 5} textAnchor="middle" fontSize={8.5} fill="#94a3b8">
                      +{hidden} colunas
                    </text>
                  )}
                </g>
              );
            })}
          </g>
        </svg>
      </div>
    </div>
  );
}
