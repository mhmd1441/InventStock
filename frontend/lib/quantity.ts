import type { StockUnit } from "./types";

export function formatStockQuantity(quantity: number, unit: StockUnit) {
  if (unit === "Centimeter") {
    const metres = quantity / 100;
    return `${quantity} cm (${metres.toLocaleString(undefined, { maximumFractionDigits: 2 })} m)`;
  }
  return `${quantity} pc`;
}

export function stockUnitLabel(unit: StockUnit) {
  return unit === "Centimeter" ? "centimetres" : "pieces";
}

/** Counter input is metres for cut hose; the stock ledger always receives whole centimetres. */
export function saleQuantityInBaseUnits(input: string, unit: StockUnit): number | null {
  const value = input.trim();
  if (unit === "Piece") {
    if (!/^\d+$/.test(value)) return null;
    const pieces = Number(value);
    return Number.isSafeInteger(pieces) && pieces > 0 && pieces <= 1_000_000 ? pieces : null;
  }
  if (!/^(?:\d+(?:\.\d{1,2})?|\.\d{1,2})$/.test(value)) return null;
  const [metres, fraction = ""] = value.split(".");
  const centimetres = Number(metres || "0") * 100 + Number(fraction.padEnd(2, "0"));
  return Number.isSafeInteger(centimetres) && centimetres > 0 && centimetres <= 1_000_000 ? centimetres : null;
}

/** The employee types USD/metre; the sale API and ledger use USD/centimetre. */
export function salePricePerBaseUnit(input: string, unit: StockUnit): string | null {
  if (unit === "Piece") return input;
  const value = input.trim();
  if (!/^\d+(?:\.\d{1,2})?$/.test(value)) return null;
  const [dollars, cents = ""] = value.split(".");
  const centsPerMetre = Number(dollars) * 100 + Number(cents.padEnd(2, "0"));
  return Number.isSafeInteger(centsPerMetre) && centsPerMetre > 0 ? String(centsPerMetre / 10_000) : null;
}
