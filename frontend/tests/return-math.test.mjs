import test from "node:test";
import assert from "node:assert/strict";
import { additionalReturnCents } from "../.test-build/return-math.js";

test("three separate returns allocate exactly the original $1.01", () => {
  assert.equal(additionalReturnCents(1.01, 3, 0, 0, 1), 34n);
  assert.equal(additionalReturnCents(1.01, 3, 1, 0.34, 1), 33n);
  assert.equal(additionalReturnCents(1.01, 3, 2, 0.67, 1), 34n);
});

test("returned quantity cannot exceed the original line", () => {
  assert.equal(additionalReturnCents(20, 2, 1, 10, 2), null);
});
