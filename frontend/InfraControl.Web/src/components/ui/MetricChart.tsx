/**
 * Gráfico reutilizável (Fase E docs/28 §11): encapsula `Chart` do PrimeReact
 * (Chart.js). Tipos iniciais: doughnut | bar | line. Defaults de cor alinhados
 * à paleta UniFi; páginas passam só `labels`/`values` (ou `datasets` completo).
 *
 * Requer `chart.js` instalado (peer do primereact/chart).
 */
import { Chart } from "primereact/chart";
import type { CSSProperties } from "react";

const UNI_PALETTE = [
  "#3b82f6",
  "#f59e0b",
  "#ef4444",
  "#8b5cf6",
  "#06b6d4",
  "#16a34a",
  "#64748b",
];

export type MetricChartType = "doughnut" | "bar" | "line";

export function MetricChart({
  type,
  labels,
  values,
  datasets,
  label = "Série",
  ariaLabel,
  height = 220,
  className,
  style,
}: {
  type: MetricChartType;
  labels: string[];
  /** Atalho 1 série — ignorado se `datasets` for passado. */
  values?: number[];
  datasets?: Array<{
    label: string;
    data: number[];
    backgroundColor?: string | string[];
    borderColor?: string | string[];
    fill?: boolean;
    tension?: number;
  }>;
  label?: string;
  ariaLabel?: string;
  height?: number;
  className?: string;
  style?: CSSProperties;
}) {
  const resolvedDatasets =
    datasets ??
    [
      {
        label,
        data: values ?? [],
        backgroundColor:
          type === "doughnut" || type === "bar"
            ? UNI_PALETTE.slice(0, labels.length)
            : "rgba(59, 130, 246, 0.15)",
        borderColor:
          type === "line" ? "#3b82f6" : UNI_PALETTE.slice(0, labels.length),
        borderWidth: type === "line" ? 2 : 1,
        fill: type === "line" ? true : undefined,
        tension: type === "line" ? 0.35 : undefined,
      },
    ];

  const data = { labels, datasets: resolvedDatasets };

  const options = {
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: type === "doughnut" ? ("bottom" as const) : ("top" as const),
        labels: { boxWidth: 10, font: { size: 11 }, color: "#64748b" },
      },
    },
    scales:
      type === "doughnut"
        ? undefined
        : {
            x: {
              ticks: { color: "#94a3b8", font: { size: 10 } },
              grid: { color: "#f1f5f9" },
            },
            y: {
              beginAtZero: true,
              ticks: { color: "#94a3b8", font: { size: 10 }, precision: 0 },
              grid: { color: "#f1f5f9" },
            },
          },
  };

  return (
    <div
      className={["uni-metric-chart", className].filter(Boolean).join(" ")}
      style={{ height, ...style }}
    >
      <Chart
        type={type}
        data={data}
        options={options}
        pt={{
          canvas: {
            role: "img",
            "aria-label": ariaLabel ?? label,
          },
        }}
        style={{ width: "100%", height: "100%" }}
      />
    </div>
  );
}
