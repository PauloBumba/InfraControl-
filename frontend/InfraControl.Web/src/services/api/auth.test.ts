import { describe, expect, it, vi, afterEach } from "vitest";
import { AuthApiError, changePassword } from "./auth";

/**
 * F26-T001 — `changePassword` consome o `POST /auth/change-password` que
 * já existia no backend sem consumidor (IdentityAuthenticationService).
 * O contrato: 204 no sucesso; 400 com motivo pt-BR no `errors[0]`
 * (senha atual errada, política de senha) — mesma semântica do
 * resetPassword.
 */
describe("changePassword", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function stubFetch(response: { status: number; body?: unknown }) {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: response.status >= 200 && response.status < 300,
      status: response.status,
      json: async () => response.body ?? null,
    });
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
  }

  it("should post current and new password to /auth/change-password", async () => {
    const fetchMock = stubFetch({ status: 204 });

    await changePassword("Atual!123456", "Nova!Senha123");

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/auth/change-password");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({
      currentPassword: "Atual!123456",
      newPassword: "Nova!Senha123",
    });
  });

  it("should surface the pt-BR reason from the envelope as AuthApiError", async () => {
    stubFetch({
      status: 400,
      body: {
        isSuccess: false,
        data: null,
        errors: ["Senha atual incorreta."],
      },
    });

    await expect(changePassword("Errada!12345", "Nova!Senha123")).rejects.toMatchObject({
      name: "ApiError",
      status: 400,
      message: "Senha atual incorreta.",
    });
    await expect(changePassword("Errada!12345", "Nova!Senha123")).rejects.toBeInstanceOf(AuthApiError);
  });
});
