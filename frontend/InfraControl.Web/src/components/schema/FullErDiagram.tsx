import { useMemo, useState } from "react";
import dagre from "@dagrejs/dagre";
import { Button } from "@/components/ui/Button";
import type { SchemaEntity } from "@/services/api/schema";
import { ErEntityTip, useErTooltip } from "./ErTooltip";
import { useErNodeDrag } from "./useErNodeDrag";

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

const PAD = 40;

/** Ponta da aresta no lado da caixa voltado para o destino (suporta
 *  layout manual — o destino pode estar em qualquer direção). */
function edgeEnds(a: PlacedNode, b: PlacedNode) {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  if (Math.abs(dx) >= Math.abs(dy)) {
    const dir = dx >= 0 ? 1 : -1;
    return {
      p1: { x: a.x + dir * (a.w / 2), y: a.y },
      p2: { x: b.x - dir * (b.w / 2), y: b.y },
      horizontal: true,
    };
  }
  const dir = dy >= 0 ? 1 : -1;
  return {
    p1: { x: a.x, y: a.y + dir * (a.h / 2) },
    p2: { x: b.x, y: b.y - dir * (b.h / 2) },
    horizontal: false,
  };
}

export function FullErDiagram({ entities }: { entities: SchemaEntity[] }) {
  const [zoom, setZoom] = useState(1);
  const drag = useErNodeDrag("infracontrol:der:full");
  const { tip, handlers: tipHandlers, boxRef } = useErTooltip(drag.dragging !== null);

  const { baseNodes, edges } = useMemo(() => {
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

    return {
      baseNodes: entities.map((e) => {
        const pos = g.node(e.entityName);
        return { entity: e, x: pos.x, y: pos.y, w: BOX_W, h: boxHeight(e) };
      }),
      edges: grouped,
    };
  }, [entities]);

  // Aplica os offsets do layout manual salvo pelo usuário.
  const nodes = useMemo(
    () =>
      baseNodes.map((n) => {
        const o = drag.offsets[n.entity.entityName];
        return o ? { ...n, x: n.x + o.dx, y: n.y + o.dy } : n;
      }),
    [baseNodes, drag.offsets],
  );

  const nodeByName = useMemo(
    () => new Map(nodes.map((n) => [n.entity.entityName, n])),
    [nodes],
  );

  // Bounds dinâmicos: nós arrastados para fora do layout original
  // continuam visíveis (o viewBox cresce com eles).
  const bounds = nodes.reduce(
    (acc, n) => ({
      minX: Math.min(acc.minX, n.x - n.w / 2 - PAD),
      maxX: Math.max(acc.maxX, n.x + n.w / 2 + PAD),
      minY: Math.min(acc.minY, n.y - n.h / 2 - PAD),
      maxY: Math.max(acc.maxY, n.y + n.h / 2 + PAD),
    }),
    { minX: Infinity, maxX: -Infinity, minY: Infinity, maxY: -Infinity },
  );
  const vbX = Number.isFinite(bounds.minX) ? bounds.minX : 0;
  const vbY = Number.isFinite(bounds.minY) ? bounds.minY : 0;
  const vbW = Math.max(400, (bounds.maxX ?? 800) - vbX);
  const vbH = Math.max(300, (bounds.maxY ?? 600) - vbY);

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
        <Button variant="ghost" onClick={drag.save} disabled={!drag.dirty}>
          <i className="pi pi-save" /> {drag.savedFlash ? "Salvo!" : "Salvar layout"}
        </Button>
        <Button variant="ghost" onClick={drag.reset} disabled={Object.keys(drag.offsets).length === 0 && !drag.dirty}>
          <i className="pi pi-undo" /> Restaurar
        </Button>
        <span style={{ fontSize: 11, color: "#94a3b8" }}>
          {entities.length} tabelas · {edges.length} relacionamentos · arraste para mover · role para navegar
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
          width={vbW * zoom}
          height={vbH * zoom}
          viewBox={`${vbX} ${vbY} ${vbW} ${vbH}`}
          style={{ display: "block" }}
          role="img"
          aria-label="Diagrama entidade-relacionamento completo"
        >
          <defs>
            <marker id="full-er-arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
              <path d="M0,0 L8,4 L0,8 z" fill="#94a3b8" />
            </marker>
          </defs>
          {/* arestas primeiro (ficam atrás das caixas) */}
            {edges.map((edge) => {
              const a = nodeByName.get(edge.from);
              const b = nodeByName.get(edge.to);
              if (!a || !b) return null;
              const { p1, p2, horizontal } = edgeEnds(a, b);
              const mx = (p1.x + p2.x) / 2;
              const my = (p1.y + p2.y) / 2;
              const d = horizontal
                ? `M ${p1.x} ${p1.y} C ${mx} ${p1.y}, ${mx} ${p2.y}, ${p2.x} ${p2.y}`
                : `M ${p1.x} ${p1.y} C ${p1.x} ${my}, ${p2.x} ${my}, ${p2.x} ${p2.y}`;
              return (
                <g key={`${edge.from}-${edge.to}`}>
                  <path
                    d={d}
                    fill="none"
                    stroke="#94a3b8"
                    strokeWidth={1.2}
                    markerEnd="url(#full-er-arrow)"
                  />
                  <text
                    x={mx}
                    y={(p1.y + p2.y) / 2 - 4}
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
              const isDragging = drag.dragging === n.entity.entityName;
              return (
                <g
                  key={n.entity.entityName}
                  {...tipHandlers(n.entity)}
                  onPointerDown={(e) => drag.beginDrag(e, n.entity.entityName)}
                  style={{ cursor: isDragging ? "grabbing" : "grab" }}
                >
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
        </svg>
      </div>
    </div>
  );
}
