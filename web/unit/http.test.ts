import { describe, expect, test } from "bun:test";
import { ApiError, createApi, type FetchLike } from "../src/api/http";

function fake(status: number, body: unknown, headers: Record<string, string> = {}) {
  const calls: { url: string; init: RequestInit }[] = [];
  const f: FetchLike = async (url, init) => {
    calls.push({ url, init });
    const text = body === undefined ? "" : typeof body === "string" ? body : JSON.stringify(body);
    return new Response(status === 204 ? null : text, { status, headers });
  };
  return { f, calls };
}

describe("api client", () => {
  test("GET returns the JSON, same-origin, no store", async () => {
    const { f, calls } = fake(200, { ownerExists: true });
    const api = createApi(f);
    expect(await api.get<{ ownerExists: boolean }>("/api/auth/status")).toEqual({ ownerExists: true });
    expect(calls[0].init.credentials).toBe("same-origin");
    expect(calls[0].init.method).toBe("GET");
    expect(calls[0].init.body).toBeUndefined();
  });

  test("a body is JSON with its content type", async () => {
    const { f, calls } = fake(200, { ok: true });
    await createApi(f).post("/api/auth/login", { name: "a", password: "b" });
    expect((calls[0].init.headers as Record<string, string>)["Content-Type"]).toBe("application/json");
    expect(JSON.parse(String(calls[0].init.body))).toEqual({ name: "a", password: "b" });
  });

  test("204 is undefined; a body-less POST has no content type", async () => {
    const { f, calls } = fake(204, undefined);
    expect(await createApi(f).post("/api/auth/logout")).toBeUndefined();
    expect((calls[0].init.headers as Record<string, string>)["Content-Type"]).toBeUndefined();
  });

  test("errors carry status, code, message and Retry-After", async () => {
    const { f } = fake(429, { error: { code: "rate_limited", message: "slow down" } }, { "Retry-After": "12" });
    const e = (await createApi(f).post("/api/auth/login", {}).catch((x: unknown) => x)) as ApiError;
    expect(e).toBeInstanceOf(ApiError);
    expect([e.status, e.code, e.message, e.retryAfter]).toEqual([429, "rate_limited", "slow down", 12]);
  });

  test("an error without the shape still is an ApiError", async () => {
    const { f } = fake(502, "<html>bad gateway</html>");
    const e = (await createApi(f).get("/api/core/info").catch((x: unknown) => x)) as ApiError;
    expect([e.status, e.code]).toEqual([502, "http_502"]);
  });

  test("a network failure is code network", async () => {
    const api = createApi(async () => {
      throw new TypeError("Failed to fetch");
    });
    const e = (await api.get("/api/core/info").catch((x: unknown) => x)) as ApiError;
    expect([e.status, e.code]).toEqual([0, "network"]);
  });

  test("401 on a signed-in route signals unauthenticated; on the sign-in routes it does not", async () => {
    const { f } = fake(401, { error: { code: "unauthenticated", message: "sign in" } });
    const api = createApi(f);
    const seen: string[] = [];
    api.onAuthSignal((s) => seen.push(s));
    await api.get("/api/users").catch(() => undefined);
    await api.post("/api/auth/login", {}).catch(() => undefined);
    await api.post("/api/auth/login/2fa", {}).catch(() => undefined);
    await api.post("/api/auth/setup", {}).catch(() => undefined);
    await api.post("/api/auth/launcher", {}).catch(() => undefined);
    expect(seen).toEqual(["unauthenticated"]);
  });

  test("a restricted 403 signals restricted; a plain 403 does not", async () => {
    const seen: string[] = [];
    for (const code of ["password_change_required", "2fa_setup_required", "forbidden", "csrf"]) {
      const { f } = fake(403, { error: { code, message: code } });
      const api = createApi(f);
      api.onAuthSignal((s) => seen.push(`${code}:${s}`));
      await api.get("/api/users").catch(() => undefined);
    }
    expect(seen).toEqual(["password_change_required:restricted", "2fa_setup_required:restricted"]);
  });

  test("only API paths", async () => {
    const { f } = fake(200, {});
    expect(createApi(f).get("https://evil.example/api/x")).rejects.toThrow("not an API path");
  });
});

test('reauth_required signals a prompt, but never retries the sensitive request', async () => {
  const { f, calls } = fake(403, { error: { code: 'reauth_required', message: 'confirm your identity' } });
  const api = createApi(f);
  const seen: string[] = [];
  api.onAuthSignal(s => seen.push(s));
  await expect(api.post('/api/users', { name: 'new' })).rejects.toThrow('confirm your identity');
  expect(seen).toEqual(['reauth']);
  expect(calls).toHaveLength(1);
});
