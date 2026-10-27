import { useEffect, useRef, useState, type ChangeEvent } from "react";
import {
  IMAGE_UPLOAD_ACCEPT,
  IMAGE_UPLOAD_MAX_SIZE_MB,
  isAllowedImageFile,
  resolveUploadSrc,
} from "./imageUploadTypes";
import "./ImageUpload.css";

export interface ImageUploadProps {
  /** Path já salvo (`/uploads/...`) ou data-URL de preview. */
  value?: string | null;
  /** Path persistido no servidor, ou `null` ao remover. */
  onChange: (path: string | null) => void;
  /**
   * Upload reutilizável — o caller injeta o destino (ex.:
   * `(file) => uploadImage(file, "knowledge").then(r => r!.imagePath)`).
   * Sem `upload`, só preview local (data-URL) — útil em formulários
   * que ainda não persistem.
   */
  upload?: (file: File) => Promise<string>;
  disabled?: boolean;
  accept?: string;
  maxSizeMB?: number;
  label?: string;
}

export function ImageUpload({
  value,
  onChange,
  upload,
  disabled = false,
  accept = IMAGE_UPLOAD_ACCEPT,
  maxSizeMB = IMAGE_UPLOAD_MAX_SIZE_MB,
  label = "Adicionar imagem",
}: ImageUploadProps) {
  const [preview, setPreview] = useState<string | null>(resolveUploadSrc(value) ?? null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setPreview(resolveUploadSrc(value) ?? null);
  }, [value]);

  const handleFileSelect = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    if (file.size > maxSizeMB * 1024 * 1024) {
      setError(`Arquivo muito grande. Máximo: ${maxSizeMB}MB`);
      return;
    }

    if (!isAllowedImageFile(file)) {
      setError("Tipo não permitido. Use JPG, PNG, GIF, WebP, BMP, SVG ou AVIF.");
      return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
      setPreview(e.target?.result as string);
      setError(null);
    };
    reader.readAsDataURL(file);

    if (!upload) {
      // Sem destino: só preview local (data-URL). Persistência exige `upload`.
      return;
    }

    setUploading(true);
    setError(null);

    try {
      const path = await upload(file);
      onChange(path);
      setPreview(path);
    } catch {
      setError("Erro ao fazer upload da imagem.");
      setPreview(resolveUploadSrc(value) ?? null);
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  };

  const handleRemove = () => {
    setPreview(null);
    onChange(null);
    setError(null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const handleClick = () => {
    if (!disabled && fileInputRef.current) fileInputRef.current.click();
  };

  return (
    <div className="image-upload">
      <input
        ref={fileInputRef}
        type="file"
        accept={accept}
        onChange={(e) => void handleFileSelect(e)}
        disabled={disabled || uploading}
        style={{ display: "none" }}
        aria-label={label}
      />

      {preview ? (
        <div className="image-upload-preview">
          <img
            src={preview}
            alt="Preview"
            style={{ maxWidth: "100%", maxHeight: "200px", borderRadius: "8px" }}
          />
          {!disabled && (
            <button
              type="button"
              onClick={handleRemove}
              className="image-upload-remove"
              title="Remover imagem"
            >
              <i className="pi pi-times" />
            </button>
          )}
        </div>
      ) : (
        <button
          type="button"
          onClick={handleClick}
          disabled={disabled || uploading}
          className="image-upload-trigger"
        >
          {uploading ? (
            <>
              <i className="pi pi-spin pi-spinner" />
              <span>Enviando...</span>
            </>
          ) : (
            <>
              <i className="pi pi-plus" />
              <span>{label}</span>
            </>
          )}
        </button>
      )}

      {error && <div className="image-upload-error">{error}</div>}
    </div>
  );
}
