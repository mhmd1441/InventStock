import { test } from "node:test";
import assert from "node:assert/strict";
import { parseSupplierImportText } from "../.test-build/supplier-import.js";

test("supplier CSV maps known columns and ignores supplier stock", () => {
  const rows = parseSupplierImportText([
    "Part Number,Description,Brand,Supplier Code,Price,Currency,TVA,Supplier Stock",
    '204-501-27-82,"Coolant hose, upper",FEBI,433,15,USD,Excluded,999',
  ].join("\n"));
  assert.equal(rows.length, 1);
  assert.deepEqual(rows[0], {
    sourceRowNumber: 2,
    partNumber: "204-501-27-82",
    name: "Coolant hose, upper",
    brand: "FEBI",
    variant: null,
    supplierCode: "433",
    catalogueCost: 15,
    currency: "USD",
    taxTreatment: "Excluded",
  });
  assert.equal("stock" in rows[0], false);
});

test("supplier TSV accepts aliases and preserves unknown cost", () => {
  const rows = parseSupplierImportText("SKU\tName\tManufacturer\tCost\tCurrency\nLX433\tAir filter\tKnecht\t\t");
  assert.equal(rows[0].partNumber, "LX433");
  assert.equal(rows[0].catalogueCost, null);
  assert.equal(rows[0].currency, null);
});

test("cost without currency is rejected before staging", () => {
  assert.throws(() => parseSupplierImportText("Number,Name,Cost\n123,Filter,10"), /cost and currency together/i);
});
