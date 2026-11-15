import { useCallback, useRef, useState } from "react";

/**
 * Drag de nós do DER com persistência do layout em localStorage.
 * As posições são guardadas como OFFSET (dx/dy) sobre o layout
 * automático — se o grafo mudar (entidade nova, colunas), o offset
 * continua válido. "Salvar" grava; "Restaurar" limpa.
 */

export interface NodeOffset {
  dx: number;
  dy: number;
}

export type NodeOffsets = Record<string, NodeOffset>;

function loadLayout(key: string): NodeOffsets {
  try {
    const raw = localStorage.getItem(key);
    const parsed = raw ? JSON.parse(raw) : null;
    return parsed && typeof parsed === "object" ? parsed : {};
  } catch {
    return {};
  }
}

export function useErNodeDrag(storageKey: string) {
  const [offsets, setOffsets] = useState<NodeOffsets>(() => loadLayout(storageKey));
  const [dragging, setDragging] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const [savedFlash, setSavedFlash] = useState(false);
  const movedRef = useRef(false);
  const offsetsRef = useRef(offsets);
  offsetsRef.current = offsets;
  const flashTimer = useRef<number | null>(null);

  /** true se o último gesto arrastou (usado p/ suprimir click-to-select). */
  const didMove = useCallback(() => movedRef.current, []);

  const beginDrag = useCallback((e: React.PointerEvent<SVGGElement>, name: string) => {
    if (e.button !== 0) return;
    const svg = e.currentTarget.ownerSVGElement;
    if (!svg) return;
    const rect = svg.getBoundingClientRect();
    const vb = svg.viewBox.baseVal;
    if (!vb.width || !vb.height || !rect.width || !rect.height) return;
    e.preventDefault();
    const pxPerUnitX = rect.width / vb.width;
    const pxPerUnitY = rect.height / vb.height;
    const startX = e.clientX;
    const startY = e.clientY;
    const base = offsetsRef.current[name] ?? { dx: 0, dy: 0 };
    movedRef.current = false;
    setDragging(name);

    const onMove = (ev: PointerEvent) => {
      const ddx = (ev.clientX - startX) / pxPerUnitX;
      const ddy = (ev.clientY - startY) / pxPerUnitY;
      if (Math.abs(ddx) + Math.abs(ddy) > 0.5) movedRef.current = true;
      setOffsets((prev) => ({
        ...prev,
        [name]: { dx: base.dx + ddx, dy: base.dy + ddy },
      }));
      setDirty(true);
    };
    const onUp = () => {
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
      setDragging(null);
      // movedRef NÃO é zerado aqui: o evento `click` dispara depois do
      // pointerup e precisa ler `true` para suprimir click-to-select.
      // O próximo pointerdown já reinicia a flag.
    };
    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
  }, []);

  const save = useCallback(() => {
    try {
      localStorage.setItem(storageKey, JSON.stringify(offsets));
    } catch {
      /* storage indisponível — ignora */
    }
    setDirty(false);
    setSavedFlash(true);
    if (flashTimer.current) window.clearTimeout(flashTimer.current);
    flashTimer.current = window.setTimeout(() => setSavedFlash(false), 1500);
  }, [offsets, storageKey]);

  const reset = useCallback(() => {
    setOffsets({});
    setDirty(false);
    try {
      localStorage.removeItem(storageKey);
    } catch {
      /* ignore */
    }
  }, [storageKey]);

  return { offsets, dragging, dirty, savedFlash, beginDrag, didMove, save, reset };
}
