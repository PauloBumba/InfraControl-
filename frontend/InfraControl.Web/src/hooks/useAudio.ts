import { useState, useEffect, useCallback } from "react";
import { audioService, AudioConfig, Announcement } from "../services/audio";

export interface UseAudioReturn {
  isInitialized: boolean;
  isInitializing: boolean;
  initError: string | null;
  config: AudioConfig;
  announcements: Announcement[];
  isSpeaking: boolean;
  initialize: () => Promise<void>;
  announce: (message: string, priority?: "critical" | "warning" | "info") => Promise<void>;
  stop: () => void;
  updateConfig: (newConfig: Partial<AudioConfig>) => void;
  clearQueue: () => void;
}

/**
 * Hook para gerenciar áudio na Central de Operações
 * Lida com inicialização (gesture policy), configuração e anúncios
 */
export function useAudio(): UseAudioReturn {
  const [isInitialized, setIsInitialized] = useState(false);
  const [isInitializing, setIsInitializing] = useState(false);
  const [initError, setInitError] = useState<string | null>(null);
  const [config, setConfig] = useState<AudioConfig>(audioService.getConfig());
  const [announcements, setAnnouncements] = useState<Announcement[]>([]);
  const [isSpeaking, setIsSpeaking] = useState(false);

  // Sincronizar estado com serviço
  useEffect(() => {
    const interval = setInterval(() => {
      setConfig(audioService.getConfig());
      setAnnouncements(audioService.getAnnouncements());
      setIsSpeaking(audioService.isCurrentlySpeaking());
    }, 500);

    return () => clearInterval(interval);
  }, []);

  /**
   * Inicializa o áudio (requer gesture do usuário)
   */
  const initialize = useCallback(async () => {
    if (isInitialized || isInitializing) {
      return;
    }

    setIsInitializing(true);
    setInitError(null);

    try {
      const result = await audioService.initialize();
      if (result.success) {
        setIsInitialized(true);
      } else {
        setInitError(result.message);
      }
    } catch (error) {
      setInitError(error instanceof Error ? error.message : "Erro desconhecido");
    } finally {
      setIsInitializing(false);
    }
  }, [isInitialized, isInitializing]);

  /**
   * Anuncia mensagem
   */
  const announce = useCallback(async (message: string, priority?: "critical" | "warning" | "info") => {
    await audioService.announce(message, priority);
  }, []);

  /**
   * Para reprodução atual
   */
  const stop = useCallback(() => {
    audioService.stop();
  }, []);

  /**
   * Atualiza configuração
   */
  const updateConfig = useCallback((newConfig: Partial<AudioConfig>) => {
    audioService.updateConfig(newConfig);
  }, []);

  /**
   * Limpa fila de anúncios
   */
  const clearQueue = useCallback(() => {
    audioService.clearQueue();
  }, []);

  return {
    isInitialized,
    isInitializing,
    initError,
    config,
    announcements,
    isSpeaking,
    initialize,
    announce,
    stop,
    updateConfig,
    clearQueue,
  };
}
