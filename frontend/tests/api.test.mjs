import { test, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import api from "../.test-build/api.js";

const originalFetch = global.fetch;
const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
beforeEach(() => { api.resetSession(); api.setSessionLostHandler(null); });
afterEach(() => { global.fetch = originalFetch; api.resetSession(); });

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
