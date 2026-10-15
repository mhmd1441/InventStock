import { test, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import { usdCents, signedUsdCents, lineCents, settlementCents, excessCreditCents, formatCents } from "../.test-build/sale-math.js";
import { parseTenderDrafts, preferredTenderAccount } from "../.test-build/tender-math.js";
import { rememberPendingSale, readPendingSale, clearPendingSale } from "../.test-build/pending-sale-operation.js";
import { rememberPendingCustomerOperation, readPendingCustomerOperation, clearPendingCustomerOperation } from "../.test-build/pending-customer-operation.js";

const savedWindow = global.window;
const storage = new Map();
beforeEach(() => {
  storage.clear();
  global.window = { localStorage: {
    getItem: key => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value),
    removeItem: key => storage.delete(key),
  }};
});
afterEach(() => { global.window = savedWindow; storage.clear(); });

test("line rounding is exact and each line rounds once", () => {
  assert.equal(lineCents("0.335", 3), BigInt(101));
  assert.equal(lineCents("0.335", 1), BigInt(34));
  assert.equal(lineCents("0.000001", 1), BigInt(0));
  assert.equal(lineCents("0.3350001", 3), null);
  assert.equal(formatCents(BigInt(-101)), "-$1.01");
});

test("sale settlement and signed customer credit use integer cents", () => {
  assert.equal(settlementCents([usdCents("10.00"), usdCents("20.00")], [usdCents("2.00")], usdCents("1.00"), usdCents("5.00")), BigInt(3400));
  assert.equal(excessCreditCents(usdCents("10.00"), signedUsdCents("5.00")), BigInt(500));
  assert.equal(excessCreditCents(usdCents("10.00"), signedUsdCents("-3.00")), BigInt(1000));
  assert.equal(signedUsdCents("-25.50"), BigInt(-2550));
  assert.equal(usdCents("1.005"), null);
});

test("native LBP is preserved and USD tender cannot disagree with its settlement", () => {
  const lbp = { id: "lbp", currency: "LBP", kind: "PhysicalSafe" };
  const usd = { id: "usd", currency: "USD", kind: "PhysicalSafe" };
  assert.deepEqual(parseTenderDrafts([{ key: "a", accountId: "lbp", native: "900000", usd: "10.00" }], [lbp]),
    [{ cashAccountId: "lbp", currency: "LBP", nativeAmount: 900000, confirmedUsdAmount: 10 }]);
  assert.throws(() => parseTenderDrafts([{ key: "a", accountId: "usd", native: "9.00", usd: "10.00" }], [usd]));
  assert.throws(() => parseTenderDrafts([{ key: "a", accountId: "lbp", native: "9e5", usd: "10.00" }], [lbp]));
  assert.deepEqual(parseTenderDrafts([{ key: "blank", accountId: "", native: "", usd: "" }], [usd]), []);
  assert.deepEqual(parseTenderDrafts([{ key: "blank", accountId: "usd", native: "", usd: "" }], [usd]), []);
  assert.equal(preferredTenderAccount([lbp, usd]), usd);
  assert.throws(() => parseTenderDrafts([{ key: "partial", accountId: "", native: "10", usd: "" }], [usd]));
});

test("sale and customer recovery records are minimal and isolated by actor", () => {
  const sale = { operationId: "11111111-1111-1111-1111-111111111111", actorId: "a", createdAtUtc: "2026-10-13T00:00:00Z" };
  const payment = { ...sale, kind: "payment", customerId: "customer" };
  rememberPendingSale(sale);
  rememberPendingCustomerOperation(payment);
  assert.equal(readPendingSale("b"), null);
  assert.equal(readPendingCustomerOperation("b"), null);
  assert.deepEqual(readPendingSale("a"), sale);
  assert.deepEqual(readPendingCustomerOperation("a"), payment);
  assert.equal(JSON.stringify([...storage.values()]).includes("customer"), true);
  assert.equal(JSON.stringify([...storage.values()]).includes("900000"), false);
  clearPendingSale("a", "22222222-2222-2222-2222-222222222222");
  assert.deepEqual(readPendingSale("a"), sale);
  clearPendingSale("a", sale.operationId);
  clearPendingCustomerOperation("a", sale.operationId);
  assert.equal(readPendingSale("a"), null);
  assert.equal(readPendingCustomerOperation("a"), null);
});
