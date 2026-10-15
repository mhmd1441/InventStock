import assert from "node:assert/strict";
import test from "node:test";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { saleQuantityInBaseUnits, salePricePerBaseUnit } = require("../.test-build/quantity.js");
const { lineCents } = require("../.test-build/sale-math.js");

test("half a metre at six USD per metre posts 50 cm and three USD", () => {
  const quantity = saleQuantityInBaseUnits("0.5", "Centimeter");
  const basePrice = salePricePerBaseUnit("6", "Centimeter");
  assert.equal(quantity, 50);
  assert.equal(basePrice, "0.06");
  assert.equal(lineCents(basePrice, quantity), 300n);
});

test("cut hose length and per-metre price reject fractions smaller than the supported units", () => {
  assert.equal(saleQuantityInBaseUnits(".29", "Centimeter"), 29);
  assert.equal(saleQuantityInBaseUnits("0.005", "Centimeter"), null);
  assert.equal(saleQuantityInBaseUnits("0", "Centimeter"), null);
  assert.equal(salePricePerBaseUnit("6.33", "Centimeter"), "0.0633");
  assert.equal(salePricePerBaseUnit("6.333", "Centimeter"), null);
});
