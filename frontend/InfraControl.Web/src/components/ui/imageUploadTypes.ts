/**
 * F24-T008 — allowlist de imagens espelhando `ImageUploadRules` no backend.
 * Extensão original é preservada no disco; o componente e o service
 * compartilham esta lista para não divergir.
 */
export const IMAGE_UPLOAD_EXTENSIONS = [
  ".jpg",
  ".jpeg",
  ".png",
  ".gif",
  ".webp",
  ".bmp",
  ".svg",
  ".avif",
] as const;

export const IMAGE_UPLOAD_ACCEPT =
  "image/jpeg,image/png,image/gif,image/webp,image/bmp,image/svg+xml,image/avif,.jpg,.jpeg,.png,.gif,.webp,.bmp,.svg,.avif";

export const IMAGE_UPLOAD_MAX_SIZE_MB = 10;

const EXTENSION_SET = new Set<string>(IMAGE_UPLOAD_EXTENSIONS);

/** Extensão do arquivo em minúsculas (com ponto) ou null se ausente. */
export function imageExtensionOf(fileName: string): string | null {
  const dot = fileName.lastIndexOf(".");
  if (dot < 0) return null;
  return fileName.slice(dot).toLowerCase();
}

export function isAllowedImageFile(file: File): boolean {
  const ext = imageExtensionOf(file.name);
  if (!ext || !EXTENSION_SET.has(ext)) return false;
  if (!file.type) return true;
  return file.type.startsWith("image/");
}

/** Path relativo `/uploads/...` ou data-URL / URL absoluta — pronto pro `<img src>`. */
export function resolveUploadSrc(path: string | null | undefined): string | null {
  if (!path) return null;
  return path;
}
