import http from "k6/http";
import { check, fail } from "k6";

const baseUrl = (__ENV.K6_BASE_URL || "").replace(/\/+$/, "");
const authCookie = __ENV.K6_AUTH_COOKIE || "";
const readPath = __ENV.K6_READ_PATH || "/api/v1/inventory/devices?page=1&pageSize=20";

export const options = {
  stages: [
    { duration: "1m", target: 10 },
    { duration: "1m", target: 10 },
    { duration: "30s", target: 25 },
    { duration: "1m", target: 25 },
    { duration: "30s", target: 50 },
    { duration: "1m", target: 50 },
    { duration: "30s", target: 0 },
  ],
  summaryTrendStats: ["avg", "min", "med", "p(50)", "p(95)", "p(99)", "max"],
};

export function setup() {
  if (!baseUrl) fail("Set K6_BASE_URL to the Nginx HTTPS origin.");
  if (!authCookie) fail("Set K6_AUTH_COOKIE to a valid test-user cookie; do not use a production session.");
}

export default function () {
  const readiness = http.get(`${baseUrl}/health/ready`, {
    tags: { name: "readiness" },
  });
  check(readiness, {
    "readiness returns 200": (response) => response.status === 200,
  });

  const inventory = http.get(`${baseUrl}${readPath}`, {
    headers: { Cookie: authCookie },
    tags: { name: "inventory list" },
  });
  check(inventory, {
    "authenticated inventory request returns 200": (response) => response.status === 200,
  });
}
