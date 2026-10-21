/**
 * Serviço de áudio para Central de Operações (ADR-063 F37-T002)
 * Implementa speechSynthesis (Web Speech API) + sons de áudio como fallback
 * Valida permissões de áudio no início (gesture/autoplay policy)
 */

export interface Announcement {
  id: string;
  message: string;
  priority: "critical" | "warning" | "info";
  timestamp: Date;
}

export interface AudioConfig {
  enabled: boolean;
  voiceEnabled: boolean;
  soundEnabled: boolean;
  volume: number; // 0.0 a 1.0
  rate: number; // 0.5 a 2.0 (velocidade da voz)
  pitch: number; // 0.0 a 2.0 (tom da voz)
}

class AudioService {
  private synthesis: SpeechSynthesis | null = null;
  private voices: SpeechSynthesisVoice[] = [];
  private config: AudioConfig = {
    enabled: true,
    voiceEnabled: true,
    soundEnabled: true,
    volume: 0.8,
    rate: 1.0,
    pitch: 1.0,
  };
  private currentUtterance: SpeechSynthesisUtterance | null = null;
  private audioContext: AudioContext | null = null;
  private isInitialized = false;
  private announcementQueue: Announcement[] = [];
  private isSpeaking = false;

  constructor() {
    if (typeof window !== "undefined" && "speechSynthesis" in window) {
      this.synthesis = window.speechSynthesis;
      this.loadVoices();
    }
  }

  /**
   * Carrega vozes disponíveis no navegador
   */
  private loadVoices(): void {
    if (!this.synthesis) return;

    const load = () => {
      this.voices = this.synthesis!.getVoices();
      // Preferir voz em português
      const ptVoice = this.voices.find((v) => v.lang.startsWith("pt"));
      if (ptVoice) {
        this.voices = [ptVoice, ...this.voices.filter((v) => v !== ptVoice)];
      }
    };

    load();
    this.synthesis.onvoiceschanged = load;
  }

  /**
   * Inicializa o contexto de áudio (requer gesture do usuário)
   * Deve ser chamado em resposta a uma interação do usuário (click, keydown)
   */
  async initialize(): Promise<{ success: boolean; message: string }> {
    if (this.isInitialized) {
      return { success: true, message: "Áudio já inicializado" };
    }

    try {
      // Inicializar AudioContext para sons
      if (typeof window !== "undefined" && "AudioContext" in window) {
        this.audioContext = new AudioContext();
      }

      // Validar speechSynthesis
      if (!this.synthesis) {
        return {
          success: false,
          message: "Síntese de voz não suportada neste navegador",
        };
      }

      // Testar se consegue falar (gesture policy)
      const testUtterance = new SpeechSynthesisUtterance("teste");
      testUtterance.volume = 0; // mudo
      this.synthesis.speak(testUtterance);

      this.isInitialized = true;
      return { success: true, message: "Áudio inicializado com sucesso" };
    } catch (error) {
      console.error("Erro ao inicializar áudio:", error);
      return {
        success: false,
        message: "Erro ao inicializar áudio: permissão negada ou não suportado",
      };
    }
  }

  /**
   * Para reprodução de áudio atual
   */
  stop(): void {
    if (this.synthesis) {
      this.synthesis.cancel();
    }
    if (this.currentUtterance) {
      this.currentUtterance = null;
    }
    this.isSpeaking = false;
  }

  /**
   * Atualiza configuração de áudio
   */
  updateConfig(newConfig: Partial<AudioConfig>): void {
    this.config = { ...this.config, ...newConfig };
  }

  /**
   * Retorna configuração atual
   */
  getConfig(): AudioConfig {
    return { ...this.config };
  }

  /**
   * Verifica se áudio está habilitado
   */
  isEnabled(): boolean {
    return this.config.enabled && this.isInitialized;
  }

  /**
   * Anuncia mensagem por voz
   */
  async speak(message: string, priority: "critical" | "warning" | "info" = "info"): Promise<void> {
    if (!this.config.enabled || !this.config.voiceEnabled || !this.synthesis) {
      return Promise.resolve();
    }

    // Cancelar fala anterior se for crítico
    if (priority === "critical") {
      this.stop();
    } else if (this.isSpeaking) {
      // Se já está falando e não é crítico, não interrompe
      return;
    }

    return new Promise((resolve, reject) => {
      const utterance = new SpeechSynthesisUtterance(message);
      utterance.lang = "pt-BR";
      utterance.volume = this.config.volume;
      utterance.rate = this.config.rate;
      utterance.pitch = this.config.pitch;

      // Usar voz em português se disponível
      const ptVoice = this.voices.find((v) => v.lang.startsWith("pt"));
      if (ptVoice) {
        utterance.voice = ptVoice;
      }

      utterance.onstart = () => {
        this.isSpeaking = true;
        this.currentUtterance = utterance;
      };

      utterance.onend = () => {
        this.isSpeaking = false;
        this.currentUtterance = null;
        resolve();
      };

      utterance.onerror = (event) => {
        this.isSpeaking = false;
        this.currentUtterance = null;
        console.error("Erro na síntese de voz:", event.error);
        // Fallback para som
        this.playSound(priority);
        reject(new Error(`Erro na síntese de voz: ${event.error}`));
      };

      if (this.synthesis) {
        this.synthesis.speak(utterance);
      }
    });
  }

  /**
   * Toca som de alerta (fallback)
   * Usa Web Audio API para gerar sons simples
   */
  async playSound(priority: "critical" | "warning" | "info" = "info"): Promise<void> {
    if (!this.config.enabled || !this.config.soundEnabled || !this.audioContext) {
      return;
    }

    if (this.audioContext.state === "suspended") {
      await this.audioContext.resume();
    }

    const oscillator = this.audioContext.createOscillator();
    const gainNode = this.audioContext.createGain();

    oscillator.connect(gainNode);
    gainNode.connect(this.audioContext.destination);

    // Configurar frequência e duração baseado na prioridade
    switch (priority) {
      case "critical":
        oscillator.frequency.value = 800; // Hz
        oscillator.type = "square";
        gainNode.gain.value = this.config.volume * 0.5;
        break;
      case "warning":
        oscillator.frequency.value = 600; // Hz
        oscillator.type = "sine";
        gainNode.gain.value = this.config.volume * 0.3;
        break;
      case "info":
        oscillator.frequency.value = 400; // Hz
        oscillator.type = "sine";
        gainNode.gain.value = this.config.volume * 0.2;
        break;
    }

    oscillator.start();

    // Fade out gradual
    if (this.audioContext) {
      gainNode.gain.exponentialRampToValueAtTime(0.01, this.audioContext.currentTime + 0.5);
      oscillator.stop(this.audioContext.currentTime + 0.5);
    }
  }

  /**
   * Anuncia mensagem (voz + som fallback)
   */
  async announce(message: string, priority: "critical" | "warning" | "info" = "info"): Promise<void> {
    if (!this.isEnabled()) {
      return;
    }

    // Adicionar à fila de anúncios
    const announcement: Announcement = {
      id: `${Date.now()}-${Math.random()}`,
      message,
      priority,
      timestamp: new Date(),
    };
    this.announcementQueue.push(announcement);

    // Manter apenas últimos 50 anúncios
    if (this.announcementQueue.length > 50) {
      this.announcementQueue.shift();
    }

    try {
      // Tentar voz primeiro
      await this.speak(message, priority);
    } catch (error) {
      // Fallback para som
      console.warn("Voz falhou, usando som:", error);
      await this.playSound(priority);
    }
  }

  /**
   * Limpa fila de anúncios
   */
  clearQueue(): void {
    this.announcementQueue = [];
  }

  /**
   * Retorna histórico de anúncios
   */
  getAnnouncements(): Announcement[] {
    return [...this.announcementQueue];
  }

  /**
   * Verifica se está falando
   */
  isCurrentlySpeaking(): boolean {
    return this.isSpeaking;
  }
}

// Singleton
export const audioService = new AudioService();
