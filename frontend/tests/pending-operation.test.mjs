import { test, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import {
  clearPendingStockOperation,
  readPendingStockOperation,
  rememberPendingStockOperation,
} from "../.test-build/pending-stock-operation.js";

const originalWindow = global.window;
const storage = new Map();

beforeEach(() => {
  global.window = { localStorage: {
    getItem: key => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value),
    removeItem: key => storage.delete(key),
  }};
  storage.clear();
});

afterEach(() => {
  global.window = originalWindow;
  storage.clear();
});

test("pending stock recovery is isolated to the original actor", () => {
  const pending = {
    operationId: "11111111-1111-1111-1111-111111111111",
    actorId: "admin",
    partId: "part-1",
    partNumber: "2045012782",
    kind: "PhysicalCount",
    quantity: 3,
    createdAtUtc: new Date().toISOString(),
  };

  rememberPendingStockOperation(pending);

  assert.equal(readPendingStockOperation("employee"), null);
  assert.deepEqual(readPendingStockOperation("admin"), pending);
});

test("only the matching operation can clear the pending journal", () => {
  const pending = {
    operationId: "11111111-1111-1111-1111-111111111111",
    actorId: "admin",
    partId: "part-1",
    partNumber: "2045012782",
    kind: "In",
    quantity: 1,
    createdAtUtc: new Date().toISOString(),
  };

  rememberPendingStockOperation(pending);
  clearPendingStockOperation("22222222-2222-2222-2222-222222222222");
  assert.deepEqual(readPendingStockOperation("admin"), pending);
  clearPendingStockOperation(pending.operationId);
  assert.equal(readPendingStockOperation("admin"), null);
});
