import { api } from "./client";

/**
 * F24-T008 — upload genérico de imagens (`POST /uploads/images?module=`).
 * Reutilizável por Knowledge e futuros módulos; path web relativo.
 */
export async function uploadImage(
  file: File,
  module: "knowledge" | "general" = "general",
): Promise<{ imagePath: string } | null> {
  const formData = new FormData();
  formData.append("file", file);

  return api.post<{ imagePath: string }>(
    `/uploads/images?module=${encodeURIComponent(module)}`,
    formData,
  );
}
