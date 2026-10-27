import { describe, expect, it } from "vitest";
import {
  ARTICLE_STATUS_DRAFT,
  ARTICLE_STATUS_IN_REVIEW,
  ARTICLE_STATUS_APPROVED,
  ARTICLE_STATUS_PUBLISHED,
  ARTICLE_STATUS_UPDATED,
  ARTICLE_STATUS_ARCHIVED,
  ARTICLE_STATUS_OBSOLETE,
  ARTICLE_STATUS_REJECTED,
} from "@/services/api/knowledge";
import { knowledgeCategoryAccent, knowledgeNextStatuses } from "./knowledgeCatalogs";

/**
 * F24-K2 (ADR-034, docs/32 §7) — matriz de 16 pares do
 * `KnowledgeArticle.IsValidTransition`. O seletor de status da UI só pode
 * oferecer estas transições; qualquer outra o backend recusa com 409.
 */
describe("knowledgeNextStatuses", () => {
  it("should allow Draft only to InReview", () => {
    expect(knowledgeNextStatuses(ARTICLE_STATUS_DRAFT)).toEqual([ARTICLE_STATUS_IN_REVIEW]);
  });

  it("should allow InReview to Approved, Updated, Rejected and back to Draft", () => {
    expect(knowledgeNextStatuses(ARTICLE_STATUS_IN_REVIEW).sort()).toEqual(
      [ARTICLE_STATUS_APPROVED, ARTICLE_STATUS_UPDATED, ARTICLE_STATUS_REJECTED, ARTICLE_STATUS_DRAFT].sort(),
    );
  });

  it("should allow Approved to Published and back to Draft", () => {
    expect(knowledgeNextStatuses(ARTICLE_STATUS_APPROVED).sort()).toEqual(
      [ARTICLE_STATUS_PUBLISHED, ARTICLE_STATUS_DRAFT].sort(),
    );
  });

  it("should allow Published to InReview, Archived and Obsolete", () => {
    expect(knowledgeNextStatuses(ARTICLE_STATUS_PUBLISHED).sort()).toEqual(
      [ARTICLE_STATUS_IN_REVIEW, ARTICLE_STATUS_ARCHIVED, ARTICLE_STATUS_OBSOLETE].sort(),
    );
  });

  it("should allow Updated to InReview, Archived and Obsolete", () => {
    expect(knowledgeNextStatuses(ARTICLE_STATUS_UPDATED).sort()).toEqual(
      [ARTICLE_STATUS_IN_REVIEW, ARTICLE_STATUS_ARCHIVED, ARTICLE_STATUS_OBSOLETE].sort(),
    );
  });

  it("should allow Archived, Obsolete and Rejected only back to Draft", () => {
    expect(knowledgeNextStatuses(ARTICLE_STATUS_ARCHIVED)).toEqual([ARTICLE_STATUS_DRAFT]);
    expect(knowledgeNextStatuses(ARTICLE_STATUS_OBSOLETE)).toEqual([ARTICLE_STATUS_DRAFT]);
    expect(knowledgeNextStatuses(ARTICLE_STATUS_REJECTED)).toEqual([ARTICLE_STATUS_DRAFT]);
  });

  it("should return empty for unknown status (defensive — never offer invalid)", () => {
    expect(knowledgeNextStatuses(99)).toEqual([]);
  });

  it("should total exactly 16 pairs across the whole matrix", () => {
    const all = [
      ARTICLE_STATUS_DRAFT,
      ARTICLE_STATUS_IN_REVIEW,
      ARTICLE_STATUS_APPROVED,
      ARTICLE_STATUS_PUBLISHED,
      ARTICLE_STATUS_UPDATED,
      ARTICLE_STATUS_ARCHIVED,
      ARTICLE_STATUS_OBSOLETE,
      ARTICLE_STATUS_REJECTED,
    ];
    const total = all.reduce((sum, status) => sum + knowledgeNextStatuses(status).length, 0);
    expect(total).toBe(16);
  });
});

/**
 * F24-T008 — categorias visuais: o DTO não tem cor (só id/nome/descrição),
 * então a UI deriva uma cor estável do nome. Estável = mesmo nome, mesma
 * cor entre renders/sessões; e nunca uma cor "quebrada" (sempre do palette).
 */
describe("knowledgeCategoryAccent", () => {
  it("should return the same accent for the same name", () => {
    expect(knowledgeCategoryAccent("Redes")).toEqual(knowledgeCategoryAccent("Redes"));
  });

  it("should return a well-formed palette entry", () => {
    const accent = knowledgeCategoryAccent("Servidores");
    expect(accent.bg).toMatch(/^#[0-9a-f]{6}$/i);
    expect(accent.color).toMatch(/^#[0-9a-f]{6}$/i);
    expect(accent.border).toMatch(/^#[0-9a-f]{6}$/i);
  });

  it("should not collapse every category to the same color", () => {
    const colors = new Set(
      ["Redes", "Servidores", "Backup", "Impressoras", "Wi-Fi", "Segurança"].map(
        (name) => knowledgeCategoryAccent(name).color,
      ),
    );
    expect(colors.size).toBeGreaterThan(1);
  });
});
