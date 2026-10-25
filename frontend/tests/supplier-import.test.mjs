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

test("messy 100-row matrix preserves safe values and rejects dangerous ambiguity", () => {
  const header = "Number;Name;Manufacturer;Cost;Currency;TVA;SupplierCode";
  const rows = Array.from({ length: 100 }, (_, index) => {
    const number = index === 1 || index === 2 ? "SAME-001" : index === 3 ? "0000125" :
      index === 4 ? "a 204-501-27-82" : `MX-${String(index).padStart(4, "0")}`;
    const brand = index === 1 ? "FEBI" : index === 2 ? "Matrix" : index === 5 ? "" : "Bosch";
    const name = index === 6 ? "خرطوم زيت" : `Filter ${index}`;
    const cost = index === 7 ? "" : index % 2 ? "12,50" : "12.50";
    const currency = index === 7 ? "" : "USD";
    const tax = index === 8 ? "Unknown" : index % 3 ? "Included" : "Excluded";
    return [number, name, brand, cost, currency, tax, `SUP-${index}`].join(";");
  });
  const parsed = parseSupplierImportText([header, ...rows].join("\n"));
  assert.equal(parsed.length, 100);
  assert.equal(parsed[2].brand, "Matrix");
  assert.equal(parsed[3].partNumber, "0000125");
  assert.equal(parsed[4].partNumber, "a 204-501-27-82");
  assert.equal(parsed[5].brand, null);
  assert.equal(parsed[6].name, "خرطوم زيت");
  assert.equal(parsed[7].catalogueCost, null);
  assert.equal(parsed[8].taxTreatment, "Unknown");
  assert.equal(parsed[9].catalogueCost, 12.5);

  assert.throws(() => parseSupplierImportText(`${header}\n${rows[0]}\n${rows[0]}`), /duplicate supplier row/i);
  assert.throws(() => parseSupplierImportText(`${header}\nX;Bad currency;Brand;10;GBP;Unknown;S`), /unsupported currency/i);
  assert.throws(() => parseSupplierImportText(`${header}\nX;${"N".repeat(201)};Brand;10;USD;Unknown;S`), /longer than/i);
  assert.throws(() => parseSupplierImportText("Number,Name,Cost,Currency\nX,Filter,12,50,USD"), /more values/i);
});
