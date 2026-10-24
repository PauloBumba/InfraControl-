import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getAccountSettings } from "@/services/api/settings";

vi.mock("@/services/api/settings", () => ({
  getAccountSettings: vi.fn(),
}));

import { useAutoRefresh } from "./useAutoRefresh";

const mockedGetSettings = vi.mocked(getAccountSettings);

describe("useAutoRefresh (F44-T003)", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("should poll load when autoRefresh is enabled", async () => {
    mockedGetSettings.mockResolvedValue({
      userId: "u1",
      timezone: "America/Sao_Paulo",
      darkMode: true,
      autoRefresh: true,
      telemetry: false,
    });
    const load = vi.fn();

    renderHook(() => useAutoRefresh(load, 30_000));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(load).not.toHaveBeenCalled();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });
    expect(load).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });
    expect(load).toHaveBeenCalledTimes(2);
  });

  it("should not poll when autoRefresh is disabled", async () => {
    mockedGetSettings.mockResolvedValue({
      userId: "u1",
      timezone: "America/Sao_Paulo",
      darkMode: true,
      autoRefresh: false,
      telemetry: false,
    });
    const load = vi.fn();

    renderHook(() => useAutoRefresh(load, 30_000));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(90_000);
    });

    expect(load).not.toHaveBeenCalled();
  });

  it("should not poll when settings cannot be loaded", async () => {
    mockedGetSettings.mockRejectedValue(new Error("401"));
    const load = vi.fn();

    renderHook(() => useAutoRefresh(load, 30_000));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(90_000);
    });

    expect(load).not.toHaveBeenCalled();
  });

  it("should stop polling after unmount", async () => {
    mockedGetSettings.mockResolvedValue({
      userId: "u1",
      timezone: "America/Sao_Paulo",
      darkMode: true,
      autoRefresh: true,
      telemetry: false,
    });
    const load = vi.fn();
    const { unmount } = renderHook(() => useAutoRefresh(load, 30_000));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    unmount();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(90_000);
    });

    expect(load).not.toHaveBeenCalled();
  });
});
