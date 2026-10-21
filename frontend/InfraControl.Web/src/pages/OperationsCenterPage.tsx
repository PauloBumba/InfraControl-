import { useState, useEffect, useCallback } from "react";
import { useAudio } from "../hooks";

/**
 * Painel central kiosk — Central de Operações (ADR-063 F37-T001)
 * Layout legível à distância com cartões de disponibilidade, rotação automática
 * e tela cheia. Dados fakes para validar UX/visual antes de conectar backend real.
 *
 * F37-T002: Alertas sonoros e por voz (validação com dados fakes)
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
  const colors: Record<string, string> = {
    online: "bg-green-100 text-green-800 border-green-300",
    offline: "bg-red-100 text-red-800 border-red-300",
    warning: "bg-yellow-100 text-yellow-800 border-yellow-300",
    success: "bg-green-100 text-green-800 border-green-300",
    error: "bg-red-100 text-red-800 border-red-300",
  };

  const labels: Record<string, string> = {
    online: "Online",
    offline: "Offline",
    warning: "Alerta",
    success: "OK",
    error: "Erro",
  };

  return (
    <span className={`px-3 py-1 rounded-full text-sm font-medium border ${colors[status] || colors.warning}`}>
      {labels[status] || status}
    </span>
  );
}

export function OperationsCenterPage() {
  const [currentModule, setCurrentModule] = useState<Module>("servers");
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [rotationEnabled, setRotationEnabled] = useState(true);
  const rotationInterval = 30000; // 30 segundos por módulo

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

  // Rotação automática de módulos
  useEffect(() => {
    if (!rotationEnabled) return;

    const interval = setInterval(() => {
      setCurrentModule((prev) => {
        const currentIndex = MODULES.findIndex((m) => m.id === prev);
        const nextIndex = currentIndex >= 0 ? (currentIndex + 1) % MODULES.length : 0;
        const nextModule = MODULES[nextIndex];
        return nextModule?.id || "servers";
      });
    }, rotationInterval);

    return () => clearInterval(interval);
  }, [rotationEnabled, rotationInterval]);

  // Anunciar mudança de módulo (F37-T002 - validação com dados fakes)
  useEffect(() => {
    if (!audioInitialized) return;

    const moduleInfo = MODULES.find((m) => m.id === currentModule);
    if (moduleInfo?.label) {
      announce(`Exibindo ${moduleInfo.label}`, "info");
    }
  }, [currentModule, audioInitialized, announce]);

  // Simular anúncios de alertas críticos (F37-T002 - validação com dados fakes)
  useEffect(() => {
    if (!audioInitialized || !rotationEnabled) return;

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
  }, [audioInitialized, rotationEnabled, announce]);

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

  const renderModule = () => {
    switch (currentModule) {
      case "servers":
        return (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {FAKE_SERVERS.map((server) => (
              <div key={server.name} className="bg-white rounded-lg shadow p-6 border border-gray-200">
                <div className="flex justify-between items-start mb-4">
                  <h3 className="text-2xl font-bold text-gray-900">{server.name}</h3>
                  <StatusChip status={server.status} />
                </div>
                <div className="space-y-3">
                  <div>
                    <div className="flex justify-between text-sm mb-1">
                      <span className="text-gray-600">CPU</span>
                      <span className="font-medium">{server.cpu}%</span>
                    </div>
                    <div className="w-full bg-gray-200 rounded-full h-3">
                      <div
                        className={`h-3 rounded-full ${server.cpu > 80 ? "bg-red-500" : server.cpu > 60 ? "bg-yellow-500" : "bg-green-500"}`}
                        style={{ width: `${server.cpu}%` }}
                      />
                    </div>
                  </div>
                  <div>
                    <div className="flex justify-between text-sm mb-1">
                      <span className="text-gray-600">Memória</span>
                      <span className="font-medium">{server.memory}%</span>
                    </div>
                    <div className="w-full bg-gray-200 rounded-full h-3">
                      <div
                        className={`h-3 rounded-full ${server.memory > 80 ? "bg-red-500" : server.memory > 60 ? "bg-yellow-500" : "bg-green-500"}`}
                        style={{ width: `${server.memory}%` }}
                      />
                    </div>
                  </div>
                  <div>
                    <div className="flex justify-between text-sm mb-1">
                      <span className="text-gray-600">Disco</span>
                      <span className="font-medium">{server.disk}%</span>
                    </div>
                    <div className="w-full bg-gray-200 rounded-full h-3">
                      <div
                        className={`h-3 rounded-full ${server.disk > 80 ? "bg-red-500" : server.disk > 60 ? "bg-yellow-500" : "bg-green-500"}`}
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
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {FAKE_SWITCHES.map((sw) => (
              <div key={sw.name} className="bg-white rounded-lg shadow p-6 border border-gray-200">
                <div className="flex justify-between items-start mb-4">
                  <h3 className="text-2xl font-bold text-gray-900">{sw.name}</h3>
                  <StatusChip status={sw.status} />
                </div>
                <div className="text-lg text-gray-600 mb-4">Uptime: {sw.uptime}</div>
                <div className="flex gap-4">
                  <div className="flex-1 bg-blue-50 rounded-lg p-4 text-center">
                    <div className="text-3xl font-bold text-blue-600">{sw.ports.total}</div>
                    <div className="text-sm text-gray-600">Total</div>
                  </div>
                  <div className="flex-1 bg-green-50 rounded-lg p-4 text-center">
                    <div className="text-3xl font-bold text-green-600">{sw.ports.up}</div>
                    <div className="text-sm text-gray-600">Up</div>
                  </div>
                  <div className="flex-1 bg-red-50 rounded-lg p-4 text-center">
                    <div className="text-3xl font-bold text-red-600">{sw.ports.down}</div>
                    <div className="text-sm text-gray-600">Down</div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        );

      case "printers":
        return (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {FAKE_PRINTERS.map((printer) => (
              <div key={printer.name} className="bg-white rounded-lg shadow p-6 border border-gray-200">
                <div className="flex justify-between items-start mb-4">
                  <h3 className="text-xl font-bold text-gray-900">{printer.name}</h3>
                  <StatusChip status={printer.status} />
                </div>
                {printer.error && (
                  <div className="bg-red-50 text-red-800 p-2 rounded mb-4 text-sm">{printer.error}</div>
                )}
                <div className="space-y-2">
                  {printer.toner !== null && (
                    <div>
                      <div className="flex justify-between text-sm mb-1">
                        <span className="text-gray-600">Toner</span>
                        <span className="font-medium">{printer.toner}%</span>
                      </div>
                      <div className="w-full bg-gray-200 rounded-full h-2">
                        <div
                          className={`h-2 rounded-full ${printer.toner < 20 ? "bg-red-500" : printer.toner < 40 ? "bg-yellow-500" : "bg-green-500"}`}
                          style={{ width: `${printer.toner}%` }}
                        />
                      </div>
                    </div>
                  )}
                  {printer.drum !== null && (
                    <div>
                      <div className="flex justify-between text-sm mb-1">
                        <span className="text-gray-600">Cilindro</span>
                        <span className="font-medium">{printer.drum}%</span>
                      </div>
                      <div className="w-full bg-gray-200 rounded-full h-2">
                        <div
                          className={`h-2 rounded-full ${printer.drum < 20 ? "bg-red-500" : printer.drum < 40 ? "bg-yellow-500" : "bg-green-500"}`}
                          style={{ width: `${printer.drum}%` }}
                        />
                      </div>
                    </div>
                  )}
                  {printer.toner === null && printer.drum === null && (
                    <div className="text-sm text-gray-500 italic">Estado aproximado não disponível neste modelo</div>
                  )}
                </div>
              </div>
            ))}
          </div>
        );

      case "network":
        return (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {FAKE_NETWORK.map((net) => (
              <div key={net.name} className="bg-white rounded-lg shadow p-6 border border-gray-200">
                <div className="flex justify-between items-start mb-4">
                  <h3 className="text-xl font-bold text-gray-900">{net.name}</h3>
                  <StatusChip status={net.status} />
                </div>
                <div className="space-y-4">
                  <div>
                    <div className="text-sm text-gray-600 mb-1">Download</div>
                    <div className="text-2xl font-bold text-blue-600">{net.bandwidth.down} Mbps</div>
                  </div>
                  <div>
                    <div className="text-sm text-gray-600 mb-1">Upload</div>
                    <div className="text-2xl font-bold text-green-600">{net.bandwidth.up} Mbps</div>
                  </div>
                  <div>
                    <div className="text-sm text-gray-600 mb-1">Latência</div>
                    <div className={`text-2xl font-bold ${net.latency > 50 ? "text-red-600" : net.latency > 30 ? "text-yellow-600" : "text-green-600"}`}>
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
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {FAKE_BACKUP.map((backup) => (
              <div key={backup.name} className="bg-white rounded-lg shadow p-6 border border-gray-200">
                <div className="flex justify-between items-start mb-4">
                  <h3 className="text-xl font-bold text-gray-900">{backup.name}</h3>
                  <StatusChip status={backup.status} />
                </div>
                <div className="space-y-2">
                  <div className="text-sm text-gray-600">Último backup</div>
                  <div className="text-lg font-medium">{backup.lastBackup}</div>
                  <div className="text-sm text-gray-600">Tamanho</div>
                  <div className="text-lg font-medium">{backup.size}</div>
                </div>
              </div>
            ))}
          </div>
        );

      case "cameras":
        return (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {FAKE_CAMERAS.map((cam) => (
              <div key={cam.name} className="bg-white rounded-lg shadow p-6 border border-gray-200">
                <div className="flex justify-between items-start mb-4">
                  <h3 className="text-xl font-bold text-gray-900">{cam.name}</h3>
                  <StatusChip status={cam.status} />
                </div>
                <div className="flex items-center gap-2">
                  <div className={`w-3 h-3 rounded-full ${cam.recording ? "bg-red-500 animate-pulse" : "bg-gray-400"}`} />
                  <span className="text-sm">{cam.recording ? "Gravando" : "Parada"}</span>
                </div>
              </div>
            ))}
          </div>
        );

      default:
        return null;
    }
  };

  const currentModuleInfo = MODULES.find((m) => m.id === currentModule) || MODULES[0];

  return (
    <div className="min-h-screen bg-gray-100">
      {/* Header simplificado do painel */}
      <header className="bg-white shadow-sm border-b border-gray-200 px-6 py-4">
        <div className="flex justify-between items-center">
          <div className="flex items-center gap-4">
            <h1 className="text-3xl font-bold text-gray-900">InfraControle</h1>
            <span className="text-xl text-gray-500">Central de Operações</span>
          </div>
          <div className="flex items-center gap-4">
            {/* Controles de áudio (F37-T002) */}
            <div className="flex items-center gap-2 border-r border-gray-300 pr-4">
              {!audioInitialized ? (
                <button
                  onClick={handleInitAudio}
                  disabled={audioInitializing}
                  className="px-3 py-1 rounded text-sm font-medium bg-purple-100 text-purple-800 hover:bg-purple-200 disabled:opacity-50 flex items-center gap-2"
                >
                  <i className={`pi ${audioInitializing ? "pi-spinner pi-spin" : "pi-volume-up"}`} />
                  <span>{audioInitializing ? "Inicializando..." : "Ativar áudio"}</span>
                </button>
              ) : (
                <>
                  <button
                    onClick={handleTestAudio}
                    disabled={isSpeaking}
                    className="px-3 py-1 rounded text-sm font-medium bg-blue-100 text-blue-800 hover:bg-blue-200 disabled:opacity-50 flex items-center gap-2"
                  >
                    <i className="pi pi-volume-up" />
                    <span>Testar</span>
                  </button>
                  <button
                    onClick={toggleAudio}
                    className={`px-3 py-1 rounded text-sm font-medium ${audioConfig.enabled ? "bg-green-100 text-green-800" : "bg-gray-100 text-gray-800"} flex items-center gap-2`}
                  >
                    <i className={`pi ${audioConfig.enabled ? "pi-volume-up" : "pi-volume-off"}`} />
                    <span>{audioConfig.enabled ? "Áudio on" : "Áudio off"}</span>
                  </button>
                  {isSpeaking && (
                    <div className="flex items-center gap-1 text-sm text-blue-600">
                      <div className="w-2 h-2 bg-blue-600 rounded-full animate-pulse" />
                      <span>Falando</span>
                    </div>
                  )}
                </>
              )}
              {audioInitError && (
                <span className="text-xs text-red-600 max-w-xs truncate" title={audioInitError}>
                  Erro
                </span>
              )}
            </div>

            <div className="flex items-center gap-2">
              <span className="text-sm text-gray-600">Rotação:</span>
              <button
                onClick={() => setRotationEnabled(!rotationEnabled)}
                className={`px-3 py-1 rounded text-sm font-medium ${rotationEnabled ? "bg-green-100 text-green-800" : "bg-gray-100 text-gray-800"}`}
              >
                {rotationEnabled ? "Ativada" : "Pausada"}
              </button>
            </div>
            <button
              onClick={toggleFullscreen}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors flex items-center gap-2"
            >
              <i className={`pi ${isFullscreen ? "pi-window-minimize" : "pi-window-maximize"}`} />
              <span>{isFullscreen ? "Sair de tela cheia" : "Tela cheia"}</span>
            </button>
          </div>
        </div>
      </header>

      {/* Barra de módulos */}
      <div className="bg-white border-b border-gray-200 px-6 py-3">
        <div className="flex gap-2 overflow-x-auto">
          {MODULES.map((module) => (
            <button
              key={module.id}
              onClick={() => setCurrentModule(module.id)}
              className={`px-4 py-2 rounded-lg flex items-center gap-2 transition-colors ${
                currentModule === module.id
                  ? "bg-blue-600 text-white"
                  : "bg-gray-100 text-gray-700 hover:bg-gray-200"
              }`}
            >
              <i className={`pi ${module.icon}`} />
              <span className="font-medium">{module.label}</span>
            </button>
          ))}
        </div>
      </div>

      {/* Conteúdo do módulo atual */}
      <main className="p-6">
        <div className="mb-4 flex justify-between items-center">
          <h2 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
            <i className={`pi ${currentModuleInfo?.icon}`} />
            {currentModuleInfo?.label}
          </h2>
          <div className="text-sm text-gray-500">
            Última atualização: {new Date().toLocaleTimeString("pt-BR")}
          </div>
        </div>
        {renderModule()}
      </main>

      {/* Indicador de rotação */}
      {rotationEnabled && (
        <div className="fixed bottom-4 right-4 bg-white rounded-lg shadow-lg px-4 py-2 border border-gray-200">
          <div className="flex items-center gap-2">
            <div className="w-2 h-2 bg-green-500 rounded-full animate-pulse" />
            <span className="text-sm text-gray-600">Rotação automática: {rotationInterval / 1000}s</span>
          </div>
        </div>
      )}
    </div>
  );
}
