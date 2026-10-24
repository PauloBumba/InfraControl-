import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const listeners: Array<(event: { eventType: string }) => void> = [];

vi.mock("@/services/signalr/notifications", () => ({
  onNotificationPush: (
    listener: (event: { eventType: string }) => void,
  ) => {
    listeners.push(listener);
    return () => {
      const index = listeners.indexOf(listener);
      if (index >= 0) listeners.splice(index, 1);
    };
  },
}));

import { useOperationalRefresh } from "./useOperationalRefresh";

describe("useOperationalRefresh (F44-T002)", () => {
  beforeEach(() => {
    listeners.length = 0;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should call onChange for operational incident and alert events", () => {
    const onChange = vi.fn();
    renderHook(() => useOperationalRefresh(onChange));

    for (const eventType of [
      "IncidentCreatedDomainEvent",
      "IncidentUpdatedDomainEvent",
      "IncidentResolvedDomainEvent",
      "AlertCreatedDomainEvent",
      "AlertResolvedDomainEvent",
    ]) {
      act(() => {
        listeners.forEach((listener) => listener({ eventType }));
      });
    }

    expect(onChange).toHaveBeenCalledTimes(5);
  });

  it("should ignore non-operational events", () => {
    const onChange = vi.fn();
    renderHook(() => useOperationalRefresh(onChange));

    act(() => {
      listeners.forEach((listener) =>
        listener({ eventType: "PdiCreatedDomainEvent" }),
      );
    });

    expect(onChange).not.toHaveBeenCalled();
  });

  it("should react to the global infracontrol:refresh event", () => {
    const onChange = vi.fn();
    renderHook(() => useOperationalRefresh(onChange));

    act(() => {
      window.dispatchEvent(new Event("infracontrol:refresh"));
    });

    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it("should unsubscribe bus and window listener on unmount", () => {
    const onChange = vi.fn();
    const { unmount } = renderHook(() => useOperationalRefresh(onChange));
    unmount();

    expect(listeners).toHaveLength(0);
    act(() => {
      window.dispatchEvent(new Event("infracontrol:refresh"));
    });
    expect(onChange).not.toHaveBeenCalled();
  });
});
