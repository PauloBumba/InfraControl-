import { useParams, useNavigate } from "react-router-dom";
import { allDevices } from "@/data/deviceData";
import { printerDetails, cameraDetails, phoneDetails, serverDetails, vmDetails, switchPorts, nasDetails, upsDetails } from "@/data/deviceData";

function ProgressBar({ value, color = "#3b82f6" }: { value: number; color?: string }) {
  return (
    <div style={{ width: "100%", height: 6, background: "#f3f4f6", borderRadius: 3, overflow: "hidden" }}>
      <div style={{ height: "100%", width: `${value}%`, background: color, borderRadius: 3, transition: "width 0.3s" }} />
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between", padding: "6px 0", borderBottom: "1px solid #f3f4f6", fontSize: 12 }}>
      <span style={{ color: "#6b7280" }}>{label}</span>
      <span style={{ fontWeight: 500, fontFamily: "monospace" }}>{value}</span>
    </div>
  );
}

// ============ PRINTER VIEW ============
function PrinterView({ deviceId }: { deviceId: string }) {
  const data = printerDetails[deviceId];
  if (!data) return <div style={{ padding: 20, color: "#9ca3af" }}>Dados não disponíveis</div>;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
        {/* Toner */}
        <div className="uni-panel">
          <div className="uni-panel-header"><h2>Toner</h2></div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {[
              { name: "Preto", value: data.tonerBlack, color: "#1a1a1a" },
              { name: "Ciano", value: data.tonerCyan, color: "#00bcd4" },
              { name: "Magenta", value: data.tonerMagenta, color: "#e91e63" },
              { name: "Amarelo", value: data.tonerYellow, color: "#ffc107" },
            ].filter(t => t.value > 0 || data.tonerCyan > 0).map((t) => (
              <div key={t.name}>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, marginBottom: 3 }}>
                  <span style={{ fontWeight: 500 }}>{t.name}</span>
                  <span style={{ color: t.value < 20 ? "#dc2626" : "#6b7280" }}>{t.value}%</span>
                </div>
                <ProgressBar value={t.value} color={t.value < 20 ? "#dc2626" : t.color} />
              </div>
            ))}
          </div>
        </div>
        {/* Drum */}
        <div className="uni-panel">
          <div className="uni-panel-header"><h2>Cilindro (Drum)</h2></div>
          <div className="uni-panel-body" style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {[
              { name: "Preto", value: data.drumBlack, color: "#1a1a1a" },
              { name: "Ciano", value: data.drumCyan, color: "#00bcd4" },
              { name: "Magenta", value: data.drumMagenta, color: "#e91e63" },
              { name: "Amarelo", value: data.drumYellow, color: "#ffc107" },
            ].filter(t => t.value > 0 || data.drumCyan > 0).map((t) => (
              <div key={t.name}>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, marginBottom: 3 }}>
                  <span style={{ fontWeight: 500 }}>{t.name}</span>
                  <span style={{ color: t.value < 20 ? "#dc2626" : "#6b7280" }}>{t.value}%</span>
                </div>
                <ProgressBar value={t.value} color={t.value < 20 ? "#dc2626" : t.color} />
              </div>
            ))}
          </div>
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Estatísticas</h2></div>
        <div className="uni-panel-body">
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr", gap: 12, textAlign: "center" }}>
            <div><div style={{ fontSize: 10, color: "#9ca3af" }}>Total Impressões</div><div style={{ fontSize: 18, fontWeight: 700 }}>{data.totalPages.toLocaleString()}</div></div>
            <div><div style={{ fontSize: 10, color: "#9ca3af" }}>Hoje</div><div style={{ fontSize: 18, fontWeight: 700 }}>{data.pagesToday}</div></div>
            <div><div style={{ fontSize: 10, color: "#9ca3af" }}>Jams</div><div style={{ fontSize: 18, fontWeight: 700, color: data.paperJams > 0 ? "#dc2626" : "#22c55e" }}>{data.paperJams}</div></div>
            <div><div style={{ fontSize: 10, color: "#9ca3af" }}>Velocidade</div><div style={{ fontSize: 14, fontWeight: 600 }}>{data.printSpeed}</div></div>
          </div>
          {data.warnings.length > 0 && (
            <div style={{ marginTop: 12, padding: 10, background: "#fefce8", borderRadius: 6, fontSize: 12 }}>
              {data.warnings.map((w, i) => <div key={i} style={{ color: "#ca8a04" }}>⚠ {w}</div>)}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ============ CAMERA VIEW ============
function CameraView({ deviceId }: { deviceId: string }) {
  const data = cameraDetails[deviceId];
  if (!data) return <div style={{ padding: 20, color: "#9ca3af" }}>Dados não disponíveis</div>;

  const storagePercent = (data.storageUsed / data.storageTotal) * 100;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
        <div className="uni-panel">
          <div className="uni-panel-header"><h2>Configuração</h2></div>
          <div className="uni-panel-body">
            <InfoRow label="Resolução" value={data.resolution} />
            <InfoRow label="FPS" value={`${data.fps} fps`} />
            <InfoRow label="Visão Noturno" value={data.nightVision ? "Sim" : "Não"} />
            <InfoRow label="Detecção Movimento" value={data.motionDetection ? "Sim" : "Não"} />
            <InfoRow label="PTZ" value={data.ptz ? "Sim" : "Não"} />
            <InfoRow label="Microfone" value={data.micEnabled ? "Sim" : "Não"} />
            <InfoRow label="Alto-falante" value={data.speakerEnabled ? "Sim" : "Não"} />
            <InfoRow label="Distância IR" value={data.irDistance} />
            <InfoRow label="Detecção IA" value={data.aiDetection ? "Sim" : "Não"} />
          </div>
        </div>
        <div className="uni-panel">
          <div className="uni-panel-header"><h2>Armazenamento</h2></div>
          <div className="uni-panel-body">
            <div style={{ textAlign: "center", marginBottom: 12 }}>
              <div style={{ fontSize: 28, fontWeight: 700 }}>{data.storageUsed} GB</div>
              <div style={{ fontSize: 11, color: "#9ca3af" }}>de {data.storageTotal} GB usados</div>
              <div style={{ marginTop: 8 }}>
                <ProgressBar value={storagePercent} color={storagePercent > 80 ? "#dc2626" : "#3b82f6"} />
              </div>
              <div style={{ fontSize: 10, color: "#9ca3af", marginTop: 4 }}>{storagePercent.toFixed(1)}%</div>
            </div>
            <InfoRow label="Modo Gravação" value={data.recordingMode} />
            <InfoRow label="Destino" value={data.storagePath} />
            <InfoRow label="Eventos 24h" value={`${data.events24h}`} />
            <InfoRow label="Movimento 24h" value={`${data.motionEvents}`} />
          </div>
        </div>
      </div>
    </div>
  );
}

// ============ PHONE VIEW ============
function PhoneView({ deviceId }: { deviceId: string }) {
  const data = phoneDetails[deviceId];
  if (!data) return <div style={{ padding: 20, color: "#9ca3af" }}>Dados não disponíveis</div>;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Linhas</h2></div>
        <div className="uni-panel-body">
          {data.lines.map((line, i) => (
            <div key={i} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "8px 0", borderBottom: i < data.lines.length - 1 ? "1px solid #f3f4f6" : "none" }}>
              <div>
                <div style={{ fontSize: 13, fontWeight: 600 }}>{line.number}</div>
                <div style={{ fontSize: 11, color: "#9ca3af" }}>{line.label}</div>
              </div>
              <span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f0fdf4", color: "#16a34a" }}>Ativa</span>
            </div>
          ))}
        </div>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>Chamadas Hoje</div>
          <div style={{ fontSize: 24, fontWeight: 700 }}>{data.callsToday}</div>
        </div>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>Perdidas</div>
          <div style={{ fontSize: 24, fontWeight: 700, color: data.callsMissed > 0 ? "#dc2626" : "#22c55e" }}>{data.callsMissed}</div>
        </div>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>Duração Média</div>
          <div style={{ fontSize: 18, fontWeight: 700 }}>{data.avgCallDuration}</div>
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Chamadas Recentes</h2></div>
        <div className="uni-panel-body">
          {data.recentCalls.map((call, i) => (
            <div key={i} style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 0", borderBottom: i < data.recentCalls.length - 1 ? "1px solid #f3f4f6" : "none" }}>
              <i className={`pi ${call.type === "Recebida" ? "pi-phone" : call.type === "Realizada" ? "pi-arrow-up" : "pi-missing"}`} style={{ fontSize: 12, color: call.type === "Perdida" ? "#dc2626" : call.type === "Recebida" ? "#22c55e" : "#3b82f6" }} />
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: 12, fontWeight: 500 }}>{call.name}</div>
                <div style={{ fontSize: 10, color: "#9ca3af" }}>{call.number}</div>
              </div>
              <span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: call.type === "Perdida" ? "#fef2f2" : "#f3f4f6", color: call.type === "Perdida" ? "#dc2626" : "#6b7280" }}>{call.type}</span>
              <span style={{ fontSize: 11 }}>{call.duration}</span>
              <span style={{ fontSize: 10, color: "#9ca3af" }}>{call.time}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ============ SERVER VIEW ============
function ServerView({ deviceId }: { deviceId: string }) {
  const data = serverDetails[deviceId];
  if (!data) return <div style={{ padding: 20, color: "#9ca3af" }}>Dados não disponíveis</div>;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
        <div className="uni-panel">
          <div className="uni-panel-header"><h2>CPU</h2></div>
          <div className="uni-panel-body">
            <div style={{ fontSize: 11, color: "#9ca3af", marginBottom: 4 }}>{data.cpu.model}</div>
            <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, marginBottom: 6 }}><span>Uso</span><span style={{ fontWeight: 600 }}>{data.cpu.usage}%</span></div>
            <ProgressBar value={data.cpu.usage} color={data.cpu.usage > 80 ? "#dc2626" : "#3b82f6"} />
            <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#9ca3af", marginTop: 8 }}><span>{data.cpu.cores} cores</span><span>{data.cpu.temp}°C</span></div>
          </div>
        </div>
        <div className="uni-panel">
          <div className="uni-panel-header"><h2>Memória</h2></div>
          <div className="uni-panel-body">
            <div style={{ fontSize: 11, color: "#9ca3af", marginBottom: 4 }}>{data.ram.total} GB total</div>
            <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, marginBottom: 6 }}><span>Uso</span><span style={{ fontWeight: 600 }}>{data.ram.used} GB / {data.ram.total} GB</span></div>
            <ProgressBar value={(data.ram.used / data.ram.total) * 100} color={data.ram.used / data.ram.total > 0.8 ? "#dc2626" : "#22c55e"} />
            <div style={{ fontSize: 11, color: "#9ca3af", marginTop: 8 }}>{data.ram.free} GB livres</div>
          </div>
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Discos</h2></div>
        <div className="uni-panel-body">
          {data.disks.map((disk, i) => (
            <div key={i} style={{ marginBottom: i < data.disks.length - 1 ? 12 : 0 }}>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, marginBottom: 3 }}>
                <span style={{ fontWeight: 500 }}>{disk.name} <span style={{ color: "#9ca3af", fontSize: 10 }}>({disk.type})</span></span>
                <span>{disk.used} / {disk.total} GB</span>
              </div>
              <ProgressBar value={(disk.used / disk.total) * 100} color={(disk.used / disk.total) > 0.8 ? "#dc2626" : "#3b82f6"} />
            </div>
          ))}
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Serviços</h2></div>
        <div className="uni-panel-body">
          {data.services.map((svc, i) => (
            <div key={i} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "6px 0", borderBottom: i < data.services.length - 1 ? "1px solid #f3f4f6" : "none" }}>
              <span style={{ fontSize: 12, fontWeight: 500 }}>{svc.name}</span>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span style={{ fontSize: 11, color: "#9ca3af", fontFamily: "monospace" }}>:{svc.port}</span>
                <span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f0fdf4", color: "#16a34a" }}>{svc.status}</span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ============ VM VIEW ============
function VmView({ deviceId }: { deviceId: string }) {
  const data = vmDetails[deviceId];
  if (!data) return <div style={{ padding: 20, color: "#9ca3af" }}>Dados não disponíveis</div>;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>CPU</div>
          <div style={{ fontSize: 20, fontWeight: 700 }}>{data.cpuUsage}%</div>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>{data.cpuCores} cores</div>
          <ProgressBar value={data.cpuUsage} color={data.cpuUsage > 80 ? "#dc2626" : "#3b82f6"} />
        </div>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>RAM</div>
          <div style={{ fontSize: 20, fontWeight: 700 }}>{data.ramUsed} GB</div>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>de {data.ramTotal} GB</div>
          <ProgressBar value={(data.ramUsed / data.ramTotal) * 100} color={(data.ramUsed / data.ramTotal) > 0.8 ? "#dc2626" : "#22c55e"} />
        </div>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>Disco</div>
          <div style={{ fontSize: 20, fontWeight: 700 }}>{data.diskUsed} GB</div>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>de {data.diskTotal} GB</div>
          <ProgressBar value={(data.diskUsed / data.diskTotal) * 100} color={(data.diskUsed / data.diskTotal) > 0.8 ? "#dc2626" : "#3b82f6"} />
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Detalhes</h2></div>
        <div className="uni-panel-body">
          <InfoRow label="Host" value={data.host} />
          <InfoRow label="OS" value={data.os} />
          <InfoRow label="Uptime" value={data.uptime} />
          <InfoRow label="Snapshots" value={`${data.snapshots}`} />
          <InfoRow label="Rede RX" value={data.networkRx} />
          <InfoRow label="Rede TX" value={data.networkTx} />
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Serviços</h2></div>
        <div className="uni-panel-body">
          {data.services.map((svc, i) => (
            <div key={i} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "6px 0", borderBottom: i < data.services.length - 1 ? "1px solid #f3f4f6" : "none" }}>
              <span style={{ fontSize: 12, fontWeight: 500 }}>{svc.name}</span>
              <span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f0fdf4", color: "#16a34a" }}>{svc.status}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ============ SWITCH PORTS VIEW ============
function SwitchView({ deviceId }: { deviceId: string }) {
  const switchData = switchPorts[deviceId];
  if (!switchData) return <div style={{ padding: 20, color: "#9ca3af" }}>Dados não disponíveis</div>;
  const ports = switchData.ports;

  const active = ports.filter(p => p.status === "active").length;
  const poe = ports.filter(p => p.poe).length;
  const poeWatts = poe * 15.4;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>Portas Ativas</div>
          <div style={{ fontSize: 20, fontWeight: 700 }}>{active}/{ports.length}</div>
        </div>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>PoE Ativos</div>
          <div style={{ fontSize: 20, fontWeight: 700, color: "#f59e0b" }}>{poe}</div>
        </div>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>PoE Watts</div>
          <div style={{ fontSize: 20, fontWeight: 700 }}>{poeWatts.toFixed(1)}W</div>
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Portas</h2></div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(80px, 1fr))", gap: 6, padding: 16 }}>
          {ports.map((port) => (
            <div key={port.port} style={{
              padding: "8px 4px", borderRadius: 6, border: `2px solid ${port.status === "active" ? "#22c55e" : port.status === "disabled" ? "#f59e0b" : "#e5e7eb"}`,
              background: port.status === "active" ? "#f0fdf4" : port.status === "disabled" ? "#fefce8" : "#f9fafb",
              textAlign: "center", fontSize: 10, cursor: "pointer", transition: "all 0.12s"
            }}>
              <div style={{ fontSize: 14, fontWeight: 700 }}>{port.port}</div>
              {port.poe && <div style={{ fontSize: 8, color: "#f59e0b", fontWeight: 700 }}>PoE</div>}
              {port.device !== "—" && <div style={{ fontSize: 8, color: "#6b7280", marginTop: 2, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{port.device}</div>}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ============ NAS VIEW ============
function NasView({ deviceId }: { deviceId: string }) {
  const data = nasDetails[deviceId];
  if (!data) return <div style={{ padding: 20, color: "#9ca3af" }}>Dados não disponíveis</div>;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Discos</h2></div>
        <div className="uni-panel-body">
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))", gap: 10 }}>
            {data.disks.map((disk) => (
              <div key={disk.slot} style={{ padding: 12, borderRadius: 6, border: "1px solid #e5e7eb", background: "#f9fafb" }}>
                <div style={{ fontSize: 10, color: "#9ca3af" }}>Slot {disk.slot}</div>
                <div style={{ fontSize: 12, fontWeight: 600 }}>{disk.model}</div>
                <div style={{ fontSize: 11, color: "#6b7280" }}>{disk.size} GB · {disk.temp}°C</div>
                <div style={{ fontSize: 10, color: "#22c55e", fontWeight: 500, marginTop: 4 }}>{disk.status}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Volumes</h2></div>
        <div className="uni-panel-body">
          {data.volumes.map((vol, i) => (
            <div key={i} style={{ marginBottom: i < data.volumes.length - 1 ? 12 : 0 }}>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, marginBottom: 3 }}>
                <span style={{ fontWeight: 500 }}>{vol.name} <span style={{ color: "#9ca3af", fontSize: 10 }}>({vol.raid} · {vol.filesystem})</span></span>
                <span>{vol.used} / {vol.size} GB</span>
              </div>
              <ProgressBar value={(vol.used / vol.size) * 100} color="#3b82f6" />
            </div>
          ))}
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Serviços</h2></div>
        <div className="uni-panel-body">
          {data.services.map((svc, i) => (
            <div key={i} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "6px 0", borderBottom: i < data.services.length - 1 ? "1px solid #f3f4f6" : "none" }}>
              <span style={{ fontSize: 12, fontWeight: 500 }}>{svc.name}</span>
              <span style={{ fontSize: 10, padding: "2px 6px", borderRadius: 4, background: "#f0fdf4", color: "#16a34a" }}>{svc.status}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ============ UPS VIEW ============
function UpsView({ deviceId }: { deviceId: string }) {
  const data = upsDetails[deviceId];
  if (!data) return <div style={{ padding: 20, color: "#9ca3af" }}>Dados não disponíveis</div>;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>Bateria</div>
          <div style={{ fontSize: 28, fontWeight: 700, color: data.batteryLevel > 50 ? "#22c55e" : "#dc2626" }}>{data.batteryLevel}%</div>
          <ProgressBar value={data.batteryLevel} color={data.batteryLevel > 50 ? "#22c55e" : "#dc2626"} />
          <div style={{ fontSize: 10, color: "#9ca3af", marginTop: 4 }}>{data.batteryTimeLeft} restantes</div>
        </div>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>Carga</div>
          <div style={{ fontSize: 28, fontWeight: 700 }}>{data.loadPercent}%</div>
          <ProgressBar value={data.loadPercent} color={data.loadPercent > 80 ? "#dc2626" : "#3b82f6"} />
          <div style={{ fontSize: 10, color: "#9ca3af", marginTop: 4 }}>{data.capacity} VA</div>
        </div>
        <div className="uni-panel" style={{ padding: 16, textAlign: "center" }}>
          <div style={{ fontSize: 10, color: "#9ca3af" }}>Temperatura</div>
          <div style={{ fontSize: 28, fontWeight: 700 }}>{data.temperature}°C</div>
          <div style={{ fontSize: 10, color: "#9ca3af", marginTop: 4 }}>{data.inputVoltage}V → {data.outputVoltage}V</div>
        </div>
      </div>
      <div className="uni-panel">
        <div className="uni-panel-header"><h2>Informações</h2></div>
        <div className="uni-panel-body">
          <InfoRow label="Modelo" value={data.model} />
          <InfoRow label="Último Self-Test" value={data.lastSelfTest} />
          <InfoRow label="Substituir Bateria" value={data.replaceBattery} />
        </div>
      </div>
    </div>
  );
}

// ============ MAIN DEVICE DETAIL ============
export function DeviceDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const device = allDevices.find(d => d.id === id);

  if (!device) {
    return (
      <div className="uni-page-empty">
        <i className="pi pi-exclamation-circle" />
        <h2>Dispositivo não encontrado</h2>
        <button onClick={() => navigate("/devices")} style={{ marginTop: 8, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          Voltar para Dispositivos
        </button>
      </div>
    );
  }

  const typeIcons: Record<string, string> = {
    gateway: "pi pi-th-large", switch: "pi pi-server", ap: "pi pi-wifi",
    printer: "pi pi-print", camera: "pi pi-video", phone: "pi pi-phone",
    server: "pi pi-desktop", vm: "pi pi-box", nas: "pi pi-database",
    ups: "pi pi-bolt", storage: "pi pi-database"
  };
  const typeColors: Record<string, string> = {
    gateway: "#3b82f6", switch: "#8b5cf6", ap: "#22c55e",
    printer: "#f59e0b", camera: "#ec4899", phone: "#06b6d4",
    server: "#3b82f6", vm: "#8b5cf6", nas: "#22c55e",
    ups: "#f59e0b", storage: "#3b82f6"
  };

  const renderTypeView = () => {
    switch (device.type) {
      case "printer": return <PrinterView deviceId={device.id} />;
      case "camera": return <CameraView deviceId={device.id} />;
      case "phone": return <PhoneView deviceId={device.id} />;
      case "server": return <ServerView deviceId={device.id} />;
      case "vm": return <VmView deviceId={device.id} />;
      case "switch": return <SwitchView deviceId={device.id} />;
      case "nas": return <NasView deviceId={device.id} />;
      case "ups": return <UpsView deviceId={device.id} />;
      default: return <div style={{ padding: 20, color: "#9ca3af" }}>Visualização não disponível para este tipo</div>;
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <button onClick={() => navigate("/devices")} style={{ width: 32, height: 32, borderRadius: 6, border: "1px solid #e5e7eb", display: "flex", alignItems: "center", justifyContent: "center", background: "white" }}>
          <i className="pi pi-arrow-left" style={{ fontSize: 12 }} />
        </button>
        <div style={{ width: 40, height: 40, borderRadius: 8, background: `${typeColors[device.type]}15`, color: typeColors[device.type], display: "flex", alignItems: "center", justifyContent: "center" }}>
          <i className={typeIcons[device.type] || "pi pi-circle"} />
        </div>
        <div>
          <h1 style={{ fontSize: 18, fontWeight: 700 }}>{device.name}</h1>
          <p style={{ fontSize: 12, color: "#6b7280" }}>{device.model} · {device.ip}</p>
        </div>
        <span style={{ marginLeft: "auto", fontSize: 10, fontWeight: 600, padding: "3px 8px", borderRadius: 4, background: device.status === "online" ? "#f0fdf4" : device.status === "warning" ? "#fefce8" : "#fef2f2", color: device.status === "online" ? "#16a34a" : device.status === "warning" ? "#ca8a04" : "#dc2626" }}>
          {device.status === "online" ? "Online" : device.status === "warning" ? "Atenção" : "Offline"}
        </span>
      </div>

      {/* Info Bar */}
      <div className="uni-panel" style={{ padding: 12 }}>
        <div style={{ display: "flex", gap: 20, fontSize: 11, color: "#6b7280", flexWrap: "wrap" }}>
          <span><i className="pi pi-map-marker" style={{ marginRight: 4 }} />{device.location}</span>
          <span><i className="pi pi-building" style={{ marginRight: 4 }} />{device.department}</span>
          <span><i className="pi pi-tag" style={{ marginRight: 4 }} />{device.serial}</span>
          <span><i className="pi pi-code" style={{ marginRight: 4 }} />v{device.firmware}</span>
          <span><i className="pi pi-clock" style={{ marginRight: 4 }} />{device.uptime}</span>
          <span style={{ fontFamily: "monospace" }}>{device.mac}</span>
        </div>
      </div>

      {/* Type-Specific View */}
      {renderTypeView()}
    </div>
  );
}
