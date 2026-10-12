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
