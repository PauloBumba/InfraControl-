import { useCallback, useRef, useState, type MouseEvent } from "react";
import type { SchemaEntity } from "@/services/api/schema";

/**
 * Tooltip estilizado para entidades do DER: segue o mouse dentro do SVG
 * e mostra tabela, colunas, tipos, PK/FK — como o inspector do
 * MySQL Workbench. Uso: espalhar `handlers(entity)` no <g> da caixa e
 * renderizar `<ErEntityTip tip={tip} />` dentro de um container
 * `position: relative`.
 */

interface TipState {
  entity: SchemaEntity;
  x: number;
  y: number;
}

export function useErTooltip(disabled = false) {
  const [tip, setTip] = useState<TipState | null>(null);
  const boxRef = useRef<HTMLDivElement | null>(null);
  const disabledRef = useRef(disabled);
  disabledRef.current = disabled;

  const handlers = useCallback(
    (entity: SchemaEntity) => ({
      onMouseEnter: (e: MouseEvent<SVGGElement>) => {
        if (disabledRef.current) return;
        const rect = boxRef.current?.getBoundingClientRect();
        if (!rect) return;
        setTip({ entity, x: e.clientX - rect.left, y: e.clientY - rect.top });
      },
      onMouseMove: (e: MouseEvent<SVGGElement>) => {
        if (disabledRef.current) return;
        const rect = boxRef.current?.getBoundingClientRect();
        if (!rect) return;
        setTip((prev) =>
          prev && prev.entity.entityName === entity.entityName
            ? { ...prev, x: e.clientX - rect.left, y: e.clientY - rect.top }
            : prev,
        );
      },
      onMouseLeave: () => {
        if (!disabledRef.current) setTip(null);
      },
    }),
    [],
  );

  return { tip, handlers, boxRef };
}

export function ErEntityTip({ tip }: { tip: TipState | null }) {
  if (!tip) return null;
  const { entity } = tip;
  return (
    <div
      role="tooltip"
      style={{
        position: "absolute",
        left: Math.min(tip.x + 14, 900),
        top: tip.y + 14,
        zIndex: 30,
        maxWidth: 340,
        maxHeight: 320,
        overflowY: "auto",
        background: "#0f172a",
        color: "#e2e8f0",
        borderRadius: 8,
        padding: "10px 12px",
        fontSize: 11,
        lineHeight: 1.5,
        boxShadow: "0 8px 24px rgba(0,0,0,0.35)",
        pointerEvents: "none",
        whiteSpace: "pre-wrap",
      }}
    >
      <div style={{ fontWeight: 700, fontSize: 12, marginBottom: 2 }}>
        {entity.entityName}
      </div>
      <div style={{ color: "#94a3b8", marginBottom: 6 }}>{entity.tableName}</div>
      {entity.properties.map((p) => (
        <div key={p.name} style={{ display: "flex", gap: 6, alignItems: "baseline" }}>
          <span style={{ fontWeight: p.isPrimaryKey ? 700 : 500, color: p.isPrimaryKey ? "#7dd3fc" : "#e2e8f0" }}>
            {p.name}
          </span>
          {p.isPrimaryKey && <span style={{ color: "#38bdf8", fontSize: 9 }}>PK</span>}
          {p.foreignKeyTarget && <span style={{ color: "#fbbf24", fontSize: 9 }}>FK</span>}
          {p.isNullable && <span style={{ color: "#64748b", fontSize: 9 }}>null</span>}
          <span style={{ color: "#64748b", marginLeft: "auto" }}>{p.clrType}</span>
        </div>
      ))}
      {entity.properties.some((p) => p.foreignKeyTarget) && (
        <div style={{ marginTop: 6, borderTop: "1px solid #334155", paddingTop: 6 }}>
          {entity.properties
            .filter((p) => p.foreignKeyTarget)
            .map((p) => (
              <div key={p.name} style={{ color: "#fbbf24", fontSize: 10 }}>
                {p.name} → {p.foreignKeyTarget}
              </div>
            ))}
        </div>
      )}
    </div>
  );
}
