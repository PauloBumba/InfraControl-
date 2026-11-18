import type { CSSProperties } from "react";

/** Tipos que a plataforma renderiza inline (img/video). PDF e demais ficam no modal/download. */
const PREVIEWABLE_MEDIA = /^(image\/|video\/)/i;

export function isPreviewableMedia(contentType: string | null | undefined): boolean {
  return !!contentType && PREVIEWABLE_MEDIA.test(contentType);
}

const mediaStyle: CSSProperties = {
  display: "block",
  maxWidth: "100%",
  maxHeight: 320,
  borderRadius: 8,
  border: "1px solid #e2e8f0",
  background: "#0f172a",
  marginTop: 8,
};

interface Props {
  /** URL autenticada com `?inline=true` (mesma origem — cookies vão junto). */
  src: string;
  contentType: string | null | undefined;
  fileName: string;
}

/**
 * Preview inline de evidência: imagem e vídeo aparecem direto na lista,
 * sem modal. PDF e binários não renderizam aqui — o caller decide
 * (modal de preview ou download).
 */
export function EvidenceMediaPreview({ src, contentType, fileName }: Props) {
  if (!isPreviewableMedia(contentType)) return null;

  if (contentType!.startsWith("video/")) {
    return (
      <video
        src={src}
        controls
        preload="metadata"
        style={mediaStyle}
        aria-label={fileName}
      />
    );
  }

  return (
    <a href={src} target="_blank" rel="noreferrer" style={{ display: "block", marginTop: 8 }}>
      <img
        src={src}
        alt={fileName}
        loading="lazy"
        style={{ ...mediaStyle, cursor: "zoom-in", background: "#f8fafc" }}
      />
    </a>
  );
}
