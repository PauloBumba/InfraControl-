import { describe, expect, it } from "vitest";
import {
  IMAGE_UPLOAD_EXTENSIONS,
  isAllowedImageFile,
  imageExtensionOf,
  resolveUploadSrc,
} from "./imageUploadTypes";

describe("imageUploadTypes (F24-T008)", () => {
  it("lists the same extensions the backend accepts", () => {
    expect([...IMAGE_UPLOAD_EXTENSIONS]).toEqual([
      ".jpg",
      ".jpeg",
      ".png",
      ".gif",
      ".webp",
      ".bmp",
      ".svg",
      ".avif",
    ]);
  });

  it.each([
    ["foto.PNG", "image/png", true],
    ["a.webp", "image/webp", true],
    ["a.avif", "image/avif", true],
    ["a.svg", "image/svg+xml", true],
    ["doc.pdf", "application/pdf", false],
    ["x.exe", "application/octet-stream", false],
  ])("isAllowedImageFile(%s, %s) → %s", (name, type, expected) => {
    const file = new File([new Uint8Array([1])], name, { type });
    expect(isAllowedImageFile(file)).toBe(expected);
  });

  it("imageExtensionOf normalizes case", () => {
    expect(imageExtensionOf("A.JPEG")).toBe(".jpeg");
  });

  it("resolveUploadSrc keeps relative upload paths", () => {
    expect(resolveUploadSrc("/uploads/knowledge/abc.png")).toBe(
      "/uploads/knowledge/abc.png",
    );
    expect(resolveUploadSrc(null)).toBeNull();
  });
});
