import { test, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import api from "../.test-build/api.js";

const originalFetch = global.fetch;
const originalWindow = global.window;
const storage = new Map();
const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
beforeEach(() => {
  global.window = { localStorage: {
    getItem: key => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value),
    removeItem: key => storage.delete(key),
  }};
  storage.clear(); api.resetSession(); api.setSessionLostHandler(null);
});
afterEach(() => { global.fetch = originalFetch; global.window = originalWindow; storage.clear(); api.resetSession(); });

test("delayed old-session responses are rejected after switching users", async () => {
  let complete;
  global.fetch = () => new Promise(resolve => { complete = resolve; });
  api.setAccessToken("old-admin");
  const request = api.api.get("/api/admin/parts");
  const rejected = assert.rejects(request, { name: "AbortError" });
  api.resetSession();
  api.setAccessToken("new-staff");
  complete(json({ unitPrice: 500 }));
  await rejected;
  assert.equal(api.getAccessToken(), "new-staff");
});

test("a late refresh cannot restore an old administrator token", async () => {
  let complete;
  global.fetch = () => new Promise(resolve => { complete = resolve; });
  const restore = api.restoreSession();
  api.resetSession();
  api.setAccessToken("staff");
  complete(json({ accessToken: "admin" }));
  assert.equal(await restore, null);
  assert.equal(api.getAccessToken(), "staff");
});

test("concurrent unauthorized queries use one refresh", async () => {
  let refreshCount = 0;
  let completeRefresh;
  const refreshed = new Promise(resolve => { completeRefresh = resolve; });
  global.fetch = async (url, init) => {
    if (String(url).endsWith("/api/auth/refresh")) {
      refreshCount++;
      await refreshed;
      return json({ accessToken: "fresh" });
    }
    return init.headers.Authorization === "Bearer fresh" ? json({ ok: true }) : json({ title: "Expired" }, 401);
  };
  const first = api.api.get("/api/parts");
  const second = api.api.get("/api/dashboard");
  await new Promise(resolve => setImmediate(resolve));
  completeRefresh();
  assert.deepEqual(await Promise.all([first, second]), [{ ok: true }, { ok: true }]);
  assert.equal(refreshCount, 1);
});

test("logout clears the token and aborts old requests even when the server fails", async () => {
  let oldSignal;
  global.fetch = async (_url, init) => { oldSignal = init.signal; return json({ ok: true }); };
  api.setAccessToken("admin");
  await api.api.get("/api/parts");
  global.fetch = async () => { throw new TypeError("offline"); };
  await assert.rejects(api.logout(), /offline/);
  assert.equal(api.getAccessToken(), null);
  assert.equal(oldSignal.aborted, true);
  assert.equal(api.isExplicitlySignedOut(), true);
  let fetches = 0;
  global.fetch = async () => { fetches++; return json({ accessToken: "old-admin" }); };
  assert.equal(await api.restoreSession(), null);
  assert.equal(fetches, 0);
});

test("only a successful explicit login clears the signed-out restoration block", async () => {
  api.markExplicitlySignedOut();
  global.fetch = async () => json({ title: "Wrong password" }, 401);
  await assert.rejects(api.login("admin@example.test", "wrong"));
  assert.equal(api.isExplicitlySignedOut(), true);
  global.fetch = async () => json({ accessToken: "staff", sessionId: "staff-session", user: { id: "staff", roles: ["Staff"] } });
  await api.login("staff@example.test", "correct");
  assert.equal(api.isExplicitlySignedOut(), false);
  assert.equal(api.getAccessToken(), "staff");
});

test("a failed logout retry revokes only the old session before a new login", async () => {
  global.fetch = async () => json({ accessToken: "admin", sessionId: "old-session", user: { id: "admin", roles: ["Admin"] } });
  await api.login("admin@example.test", "correct");
  global.fetch = async () => { throw new TypeError("offline"); };
  await assert.rejects(api.logout(), /offline/);

  const calls = [];
  global.fetch = async (url, init) => {
    calls.push({ url: String(url), body: init.body });
    return String(url).endsWith("/api/auth/logout")
      ? new Response(null, { status: 204 })
      : json({ accessToken: "staff", sessionId: "new-session", user: { id: "staff", roles: ["Staff"] } });
  };
  await api.login("staff@example.test", "correct");

  assert.equal(JSON.parse(calls[0].body).sessionId, "old-session");
  assert.equal(api.getAccessToken(), "staff");
});

test("GET passes cancellation and disables browser caching", async () => {
  const controller = new AbortController();
  let options;
  global.fetch = async (_url, init) => { options = init; return json([]); };
  await api.api.get("/api/parts", { search: "~/12582" }, controller.signal);
  assert.equal(options.cache, "no-store");
  controller.abort();
  assert.equal(options.signal.aborted, true);
});

test("PATCH sends a JSON request body rather than leaking values into the URL", async () => {
  let request;
  global.fetch = async (url, init) => { request = { url: String(url), init }; return new Response(null, { status: 204 }); };
  await api.api.patch("/api/parts/id/active", { isActive: false });
  assert.equal(request.init.method, "PATCH");
  assert.equal(request.init.body, JSON.stringify({ isActive: false }));
  assert.equal(new URL(request.url).search, "");
  assert.equal(request.init.headers["Content-Type"], "application/json");
});
