import { useState } from "react";

type TimeRange = "1H" | "6H" | "24H" | "7D";

interface TrafficDataPoint {
  time: string;
  upload: number;
  download: number;
}

const mockData: Record<TimeRange, TrafficDataPoint[]> = {
  "1H": [
    { time: "10:00", upload: 120, download: 340 },
    { time: "10:10", upload: 135, download: 380 },
    { time: "10:20", upload: 98, download: 290 },
    { time: "10:30", upload: 145, download: 420 },
    { time: "10:40", upload: 160, download: 450 },
    { time: "10:50", upload: 142, download: 410 },
    { time: "11:00", upload: 155, download: 430 },
  ],
  "6H": [
    { time: "06:00", upload: 80, download: 200 },
    { time: "07:00", upload: 120, download: 350 },
    { time: "08:00", upload: 180, download: 520 },
    { time: "09:00", upload: 200, download: 580 },
    { time: "10:00", upload: 160, download: 450 },
    { time: "11:00", upload: 155, download: 430 },
  ],
  "24H": [
    { time: "00:00", upload: 30, download: 80 },
    { time: "04:00", upload: 15, download: 40 },
    { time: "08:00", upload: 180, download: 520 },
    { time: "12:00", upload: 200, download: 600 },
    { time: "16:00", upload: 190, download: 550 },
    { time: "20:00", upload: 100, download: 300 },
    { time: "23:59", upload: 40, download: 100 },
  ],
  "7D": [
    { time: "Seg", upload: 150, download: 420 },
    { time: "Ter", upload: 165, download: 460 },
    { time: "Qua", upload: 140, download: 390 },
    { time: "Qui", upload: 180, download: 510 },
    { time: "Sex", upload: 195, download: 560 },
    { time: "Sáb", upload: 90, download: 250 },
    { time: "Dom", upload: 70, download: 200 },
  ],
};

function buildPath(data: TrafficDataPoint[], key: "upload" | "download", maxVal: number, height: number, width: number): string {
  const step = width / (data.length - 1);
  return data
    .map((d, i) => {
      const x = i * step;
      const y = height - (d[key] / maxVal) * height;
      return `${i === 0 ? "M" : "L"}${x},${y}`;
    })
    .join(" ");
}

export function TrafficChart() {
  const [range, setRange] = useState<TimeRange>("24H");
  const data = mockData[range];

  const maxVal = Math.max(...data.map((d) => Math.max(d.upload, d.download))) * 1.1;
  const svgWidth = 700;
  const svgHeight = 200;
  const chartWidth = svgWidth;
  const chartHeight = svgHeight - 40;

  const downloadPath = buildPath(data, "download", maxVal, chartHeight, chartWidth);
  const uploadPath = buildPath(data, "upload", maxVal, chartHeight, chartWidth);

  const lastPoint = data[data.length - 1];

  return (
    <article className="panel traffic-chart">
      <div className="panel-heading">
        <div>
          <h2>Traffic</h2>
          <p>Network throughput over time</p>
        </div>
        <div className="time-range-tabs">
          {(["1H", "6H", "24H", "7D"] as TimeRange[]).map((r) => (
            <button
              key={r}
              className={`time-range-tab ${range === r ? "active" : ""}`}
              onClick={() => setRange(r)}
            >
              {r}
            </button>
          ))}
        </div>
      </div>

      <div className="chart-area">
        <svg viewBox={`0 0 ${svgWidth} ${svgHeight}`} preserveAspectRatio="none" className="traffic-svg">
          <defs>
            <linearGradient id="downloadFill" x1="0" x2="0" y1="0" y2="1">
              <stop offset="0%" stopColor="#3b82f6" stopOpacity="0.3" />
              <stop offset="100%" stopColor="#3b82f6" stopOpacity="0" />
            </linearGradient>
            <linearGradient id="uploadFill" x1="0" x2="0" y1="0" y2="1">
              <stop offset="0%" stopColor="#22c55e" stopOpacity="0.3" />
              <stop offset="100%" stopColor="#22c55e" stopOpacity="0" />
            </linearGradient>
          </defs>

          {[0, 0.25, 0.5, 0.75, 1].map((pct) => (
            <line key={pct} x1={0} y1={chartHeight * pct} x2={chartWidth} y2={chartHeight * pct} stroke="var(--border)" strokeDasharray="4,4" />
          ))}

          <path
            d={`M0,${chartHeight} L${data.map((d, i) => `${i * (chartWidth / (data.length - 1))},${chartHeight - (d.download / maxVal) * chartHeight}`).join(" L")} L${chartWidth},${chartHeight} Z`}
            fill="url(#downloadFill)"
          />
          <path d={downloadPath} fill="none" stroke="#3b82f6" strokeWidth="3" strokeLinecap="round" className="traffic-line" />

          <path
            d={`M0,${chartHeight} L${data.map((d, i) => `${i * (chartWidth / (data.length - 1))},${chartHeight - (d.upload / maxVal) * chartHeight}`).join(" L")} L${chartWidth},${chartHeight} Z`}
            fill="url(#uploadFill)"
          />
          <path d={uploadPath} fill="none" stroke="#22c55e" strokeWidth="3" strokeLinecap="round" className="traffic-line" />

          {data.map((d, i) => {
            const x = i * (chartWidth / (data.length - 1));
            return (
              <g key={d.time}>
                <circle cx={x} cy={chartHeight - (d.download / maxVal) * chartHeight} r="4" fill="#3b82f6" className="traffic-dot" />
                <circle cx={x} cy={chartHeight - (d.upload / maxVal) * chartHeight} r="4" fill="#22c55e" className="traffic-dot" />
              </g>
            );
          })}

          {data.map((d, i) => {
            const x = i * (chartWidth / (data.length - 1));
            return (
              <text key={d.time} x={x} y={svgHeight - 5} textAnchor="middle" fill="var(--text-muted)" fontSize="11">
                {d.time}
              </text>
            );
          })}
        </svg>
      </div>

      <div className="chart-legend">
        <span><span className="legend-dot" style={{ background: "#3b82f6" }} /> Download</span>
        <span><span className="legend-dot" style={{ background: "#22c55e" }} /> Upload</span>
        {lastPoint && (
          <strong>{lastPoint.download} Mbps ↓ / {lastPoint.upload} Mbps ↑</strong>
        )}
      </div>
    </article>
  );
}
