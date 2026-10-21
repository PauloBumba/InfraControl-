import { useState, useEffect, useCallback } from "react";
import { useAudio } from "../hooks";

/**
 * Painel central kiosk — Central de Operações (ADR-063 F37-T001)
 * Layout legível à distância com cartões de disponibilidade e tela cheia.
 * Dados fakes para validar UX/visual antes de conectar backend real.
 *
 * F37-T002: Alertas sonoros e por voz (validação com dados fakes)
 *
 * F37-T007: estilos passam a ser o CSS próprio do projeto
 * (`styles/pages/operations-center.css`, classes `uni-ops-*` + tokens
 * `--uni-*`).
 *
 * 2026-10-21 — Painel fixo (sem rotação): o painel de TV/NOC passa a exibir
 * TODOS os módulos simultaneamente em colunas (uma seção por módulo), sem
 * troca automática de tela nem abas. O usuário especificou tela fixa de
 * exibição contínua mostrando o estado de cada coisa — a rotação automática
 * de 30s por módulo (F37-T001) foi removida por decisão explícita. Os
 * controles de áudio (F37-T002) e tela cheia permanecem.
 */

type Module = "servers" | "switches" | "printers" | "network" | "backup" | "cameras";

interface ServerStatus {
  name: string;
  status: "online" | "offline" | "warning";
  cpu: number;
  memory: number;
  disk: number;
}

interface SwitchStatus {
  name: string;
  status: "online" | "offline" | "warning";
  uptime: string;
  ports: { total: number; up: number; down: number };
}

interface PrinterStatus {
  name: string;
  status: "online" | "offline" | "warning";
  toner: number | null; // null = modelo sem % aproximado
  drum: number | null;
  error: string | null;
}

interface NetworkStatus {
  name: string;
  status: "online" | "offline" | "warning";
  bandwidth: { down: number; up: number };
  latency: number;
}

interface BackupStatus {
  name: string;
  status: "success" | "warning" | "error";
  lastBackup: string;
  size: string;
}

interface CameraStatus {
  name: string;
  status: "online" | "offline" | "warning";
  recording: boolean;
}

const MODULES: { id: Module; label: string; icon: string }[] = [
  { id: "servers", label: "Servidores", icon: "pi-server" },
  { id: "switches", label: "Switches", icon: "pi-sitemap" },
  { id: "printers", label: "Impressoras", icon: "pi-print" },
  { id: "network", label: "Rede/Wi-Fi", icon: "pi-wifi" },
  { id: "backup", label: "Backup", icon: "pi-database" },
  { id: "cameras", label: "Câmeras/Telefonia", icon: "pi-video" },
];

// Dados fakes para validação visual
const FAKE_SERVERS: ServerStatus[] = [
  { name: "SRV-PROD-01", status: "online", cpu: 45, memory: 62, disk: 78 },
  { name: "SRV-PROD-02", status: "online", cpu: 32, memory: 58, disk: 65 },
  { name: "SRV-DB-01", status: "warning", cpu: 78, memory: 85, disk: 82 },
  { name: "SRV-DB-02", status: "online", cpu: 55, memory: 71, disk: 58 },
  { name: "SRV-APP-01", status: "online", cpu: 28, memory: 45, disk: 42 },
  { name: "SRV-APP-02", status: "offline", cpu: 0, memory: 0, disk: 0 },
];

const FAKE_SWITCHES: SwitchStatus[] = [
  { name: "SW-CORE-01", status: "online", uptime: "45d 12h", ports: { total: 48, up: 46, down: 2 } },
  { name: "SW-CORE-02", status: "online", uptime: "45d 12h", ports: { total: 48, up: 48, down: 0 } },
  { name: "SW-ACC-01", status: "online", uptime: "30d 8h", ports: { total: 24, up: 22, down: 2 } },
  { name: "SW-ACC-02", status: "warning", uptime: "12d 4h", ports: { total: 24, up: 20, down: 4 } },
];

const FAKE_PRINTERS: PrinterStatus[] = [
  { name: "HP-LaserJet-01", status: "online", toner: 65, drum: 80, error: null },
  { name: "HP-LaserJet-02", status: "warning", toner: 15, drum: 45, error: "Toner baixo" },
  { name: "Brother-01", status: "online", toner: 78, drum: 92, error: null },
  { name: "Canon-01", status: "offline", toner: null, drum: null, error: "Sem comunicação" },
];

const FAKE_NETWORK: NetworkStatus[] = [
  { name: "Internet Gateway", status: "online", bandwidth: { down: 950, up: 850 }, latency: 12 },
  { name: "Wi-Fi Controller", status: "online", bandwidth: { down: 450, up: 320 }, latency: 8 },
  { name: "VPN Gateway", status: "warning", bandwidth: { down: 120, up: 95 }, latency: 45 },
];

const FAKE_BACKUP: BackupStatus[] = [
  { name: "PostgreSQL Daily", status: "success", lastBackup: "2026-10-20 02:00", size: "12.5 GB" },
  { name: "PostgreSQL Weekly", status: "success", lastBackup: "2026-10-19 03:00", size: "85.2 GB" },
  { name: "Config Backup", status: "warning", lastBackup: "2026-10-18 05:00", size: "245 MB" },
];

const FAKE_CAMERAS: CameraStatus[] = [
  { name: "CAM-ENTRADA-01", status: "online", recording: true },
  { name: "CAM-ENTRADA-02", status: "online", recording: true },
  { name: "CAM-ESTACIONAMENTO", status: "online", recording: true },
  { name: "CAM-CORREDOR-01", status: "warning", recording: false },
  { name: "CAM-CORREDOR-02", status: "offline", recording: false },
];

function StatusChip({ status }: { status: string }) {
  // Cor por estado vem do CSS (`.uni-ops-chip--{status}`) com tokens --uni-*
  // e variáveis locais --ops-*-fg: contraste garantido nos dois temas.
  const labels: Record<string, string> = {
    online: "Online",
    offline: "Offline",
    warning: "Alerta",
    success: "OK",
    error: "Erro",
  };

  return <span className={`uni-ops-chip uni-ops-chip--${status}`}>{labels[status] || status}</span>;
}

export function OperationsCenterPage() {
  const [isFullscreen, setIsFullscreen] = useState(false);
  // Relógio do painel: numa tela fixa de exibição contínua o "Última
  // atualização" não pode ficar congelado — atualiza a cada 30s.
  const [now, setNow] = useState(() => new Date());

  // Integração de áudio (F37-T002)
  const {
    isInitialized: audioInitialized,
    isInitializing: audioInitializing,
    initError: audioInitError,
    config: audioConfig,
    isSpeaking,
    initialize: initAudio,
    announce,
    updateConfig: updateAudioConfig,
  } = useAudio();

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 30000);
    return () => clearInterval(id);
  }, []);

  // Simular anúncios de alertas críticos (F37-T002 - validação com dados fakes)
  useEffect(() => {
    if (!audioInitialized) return;

    const interval = setInterval(() => {
      // Simular alerta crítico aleatório
      const criticalAlerts = [
        "Servidor SRV-DB-01 com CPU acima de 80%",
        "Switch SW-ACC-02 com 4 portas down",
        "Impressora HP-LaserJet-02 com toner baixo",
        "Latência VPN Gateway acima de 40ms",
      ];
      const randomAlert = criticalAlerts[Math.floor(Math.random() * criticalAlerts.length)];
      if (randomAlert) {
        announce(randomAlert, "critical");
      }
    }, 60000); // A cada 60 segundos

    return () => clearInterval(interval);
  }, [audioInitialized, announce]);

  // Tela cheia
  const toggleFullscreen = useCallback(() => {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen().then(() => setIsFullscreen(true));
    } else {
      document.exitFullscreen().then(() => setIsFullscreen(false));
    }
  }, []);

  // Inicializar áudio ao clicar no botão
  const handleInitAudio = useCallback(async () => {
    await initAudio();
  }, [initAudio]);

  // Testar áudio
  const handleTestAudio = useCallback(async () => {
    if (audioInitialized) {
      await announce("Teste de áudio - InfraControle Central de Operações", "info");
    }
  }, [audioInitialized, announce]);

  // Alternar áudio
  const toggleAudio = useCallback(() => {
    updateAudioConfig({ enabled: !audioConfig.enabled });
  }, [audioConfig.enabled, updateAudioConfig]);

  // Detectar mudança de fullscreen via ESC
  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };
    document.addEventListener("fullscreenchange", handleFullscreenChange);
    return () => document.removeEventListener("fullscreenchange", handleFullscreenChange);
  }, []);

  const renderModule = (module: Module) => {
    switch (module) {
      case "servers":
        return (
          <div className="uni-ops-grid">
            {FAKE_SERVERS.map((server) => (
              <div key={server.name} className="uni-ops-card">
                <div className="uni-ops-card-head">
                  <h3 className="uni-ops-card-title">{server.name}</h3>
                  <StatusChip status={server.status} />
                </div>
                <div>
                  <div className="uni-ops-metric">
                    <div className="uni-ops-metric-head">
                      <span>CPU</span>
                      <span>{server.cpu}%</span>
                    </div>
                    <div className="uni-ops-bar">
                      <div
                        className={`uni-ops-bar-fill ${server.cpu > 80 ? "is-crit" : server.cpu > 60 ? "is-warn" : ""}`}
                        style={{ width: `${server.cpu}%` }}
                      />
                    </div>
                  </div>
                  <div className="uni-ops-metric">
                    <div className="uni-ops-metric-head">
                      <span>Memória</span>
                      <span>{server.memory}%</span>
                    </div>
                    <div className="uni-ops-bar">
                      <div
                        className={`uni-ops-bar-fill ${server.memory > 80 ? "is-crit" : server.memory > 60 ? "is-warn" : ""}`}
                        style={{ width: `${server.memory}%` }}
                      />
                    </div>
                  </div>
                  <div className="uni-ops-metric">
                    <div className="uni-ops-metric-head">
                      <span>Disco</span>
                      <span>{server.disk}%</span>
                    </div>
                    <div className="uni-ops-bar">
                      <div
                        className={`uni-ops-bar-fill ${server.disk > 80 ? "is-crit" : server.disk > 60 ? "is-warn" : ""}`}
                        style={{ width: `${server.disk}%` }}
                      />
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        );

      case "switches":
        return (
          <div className="uni-ops-grid">
            {FAKE_SWITCHES.map((sw) => (
              <div key={sw.name} className="uni-ops-card">
                <div className="uni-ops-card-head">
                  <h3 className="uni-ops-card-title">{sw.name}</h3>
                  <StatusChip status={sw.status} />
                </div>
                <div className="uni-ops-uptime">Uptime: {sw.uptime}</div>
                <div className="uni-ops-stat-row">
                  <div className="uni-ops-stat">
                    <div className="uni-ops-stat-value">{sw.ports.total}</div>
                    <div className="uni-ops-stat-label">Total</div>
                  </div>
                  <div className="uni-ops-stat is-green">
                    <div className="uni-ops-stat-value">{sw.ports.up}</div>
                    <div className="uni-ops-stat-label">Up</div>
                  </div>
                  <div className="uni-ops-stat is-red">
                    <div className="uni-ops-stat-value">{sw.ports.down}</div>
                    <div className="uni-ops-stat-label">Down</div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        );

      case "printers":
        return (
          <div className="uni-ops-grid">
            {FAKE_PRINTERS.map((printer) => (
              <div key={printer.name} className="uni-ops-card">
                <div className="uni-ops-card-head">
                  <h3 className="uni-ops-card-title">{printer.name}</h3>
                  <StatusChip status={printer.status} />
                </div>
                {printer.error && <div className="uni-ops-error">{printer.error}</div>}
                <div>
                  {printer.toner !== null && (
                    <div className="uni-ops-metric">
                      <div className="uni-ops-metric-head">
                        <span>Toner</span>
                        <span>{printer.toner}%</span>
                      </div>
                      <div className="uni-ops-bar">
                        <div
                          className={`uni-ops-bar-fill ${printer.toner < 20 ? "is-crit" : printer.toner < 40 ? "is-warn" : ""}`}
                          style={{ width: `${printer.toner}%` }}
                        />
                      </div>
                    </div>
                  )}
                  {printer.drum !== null && (
                    <div className="uni-ops-metric">
                      <div className="uni-ops-metric-head">
                        <span>Cilindro</span>
                        <span>{printer.drum}%</span>
                      </div>
                      <div className="uni-ops-bar">
                        <div
                          className={`uni-ops-bar-fill ${printer.drum < 20 ? "is-crit" : printer.drum < 40 ? "is-warn" : ""}`}
                          style={{ width: `${printer.drum}%` }}
                        />
                      </div>
                    </div>
                  )}
                  {printer.toner === null && printer.drum === null && (
                    <div className="uni-ops-note">Estado aproximado não disponível neste modelo</div>
                  )}
                </div>
              </div>
            ))}
          </div>
        );

      case "network":
        return (
          <div className="uni-ops-grid">
            {FAKE_NETWORK.map((net) => (
              <div key={net.name} className="uni-ops-card">
                <div className="uni-ops-card-head">
                  <h3 className="uni-ops-card-title">{net.name}</h3>
                  <StatusChip status={net.status} />
                </div>
                <div>
                  <div className="uni-ops-field">
                    <div className="uni-ops-field-label">Download</div>
                    <div className="uni-ops-field-value">{net.bandwidth.down} Mbps</div>
                  </div>
                  <div className="uni-ops-field">
                    <div className="uni-ops-field-label">Upload</div>
                    <div className="uni-ops-field-value is-green">{net.bandwidth.up} Mbps</div>
                  </div>
                  <div className="uni-ops-field">
                    <div className="uni-ops-field-label">Latência</div>
                    <div
                      className={`uni-ops-field-value ${net.latency > 50 ? "is-crit" : net.latency > 30 ? "is-warn" : "is-green"}`}
                    >
                      {net.latency} ms
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        );

      case "backup":
        return (
          <div className="uni-ops-grid">
            {FAKE_BACKUP.map((backup) => (
              <div key={backup.name} className="uni-ops-card">
                <div className="uni-ops-card-head">
                  <h3 className="uni-ops-card-title">{backup.name}</h3>
                  <StatusChip status={backup.status} />
                </div>
                <div>
                  <div className="uni-ops-field">
                    <div className="uni-ops-field-label">Último backup</div>
                    <div className="uni-ops-field-value is-text">{backup.lastBackup}</div>
                  </div>
                  <div className="uni-ops-field">
                    <div className="uni-ops-field-label">Tamanho</div>
                    <div className="uni-ops-field-value is-text">{backup.size}</div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        );

      case "cameras":
        return (
          <div className="uni-ops-grid">
            {FAKE_CAMERAS.map((cam) => (
              <div key={cam.name} className="uni-ops-card">
                <div className="uni-ops-card-head">
                  <h3 className="uni-ops-card-title">{cam.name}</h3>
                  <StatusChip status={cam.status} />
                </div>
                <div className="uni-ops-rec">
                  <div className={`uni-ops-rec-dot ${cam.recording ? "is-recording" : ""}`} />
                  <span>{cam.recording ? "Gravando" : "Parada"}</span>
                </div>
              </div>
            ))}
          </div>
        );

      default:
        return null;
    }
  };

  return (
    <div className="uni-ops-page">
      {/* Header simplificado do painel */}
      <header className="uni-ops-header">
        <div className="uni-ops-header-row">
          <div className="uni-ops-brand">
            <h1 className="uni-ops-title">InfraControle</h1>
            <span className="uni-ops-subtitle">Central de Operações</span>
          </div>
          <div className="uni-ops-tools">
            {/* Controles de áudio (F37-T002) */}
            <div className="uni-ops-audio">
              {!audioInitialized ? (
                <button
                  onClick={handleInitAudio}
                  disabled={audioInitializing}
                  className="uni-ops-btn is-purple"
                >
                  <i className={`pi ${audioInitializing ? "pi-spinner pi-spin" : "pi-volume-up"}`} />
                  <span>{audioInitializing ? "Inicializando..." : "Ativar áudio"}</span>
                </button>
              ) : (
                <>
                  <button
                    onClick={handleTestAudio}
                    disabled={isSpeaking}
                    className="uni-ops-btn is-blue"
                  >
                    <i className="pi pi-volume-up" />
                    <span>Testar</span>
                  </button>
                  <button
                    onClick={toggleAudio}
                    className={`uni-ops-btn ${audioConfig.enabled ? "is-green" : ""}`}
                  >
                    <i className={`pi ${audioConfig.enabled ? "pi-volume-up" : "pi-volume-off"}`} />
                    <span>{audioConfig.enabled ? "Áudio on" : "Áudio off"}</span>
                  </button>
                  {isSpeaking && (
                    <div className="uni-ops-speaking">
                      <div className="uni-ops-speaking-dot" />
                      <span>Falando</span>
                    </div>
                  )}
                </>
              )}
              {audioInitError && (
                <span className="uni-ops-audio-error" title={audioInitError}>
                  Erro
                </span>
              )}
            </div>

            <div className="uni-ops-updated">
              Última atualização: {now.toLocaleTimeString("pt-BR")}
            </div>
            <button onClick={toggleFullscreen} className="uni-ops-btn is-primary">
              <i className={`pi ${isFullscreen ? "pi-window-minimize" : "pi-window-maximize"}`} />
              <span>{isFullscreen ? "Sair de tela cheia" : "Tela cheia"}</span>
            </button>
          </div>
        </div>
      </header>

      {/* Painel fixo: todos os módulos visíveis ao mesmo tempo (sem rotação) */}
      <main className="uni-ops-main">
        <div className="uni-ops-board">
          {MODULES.map((moduleInfo) => (
            <section key={moduleInfo.id} className="uni-ops-section">
              <div className="uni-ops-section-head">
                <h2 className="uni-ops-section-title">
                  <i className={`pi ${moduleInfo.icon}`} />
                  {moduleInfo.label}
                </h2>
              </div>
              {renderModule(moduleInfo.id)}
            </section>
          ))}
        </div>
      </main>
    </div>
  );
}
