/** Exact counter math: USD inputs are cents; six-decimal unit prices are millionths. */
export function decimalUnits(value: string, scale: number): bigint | null {
  if (!new RegExp(`^(?:0|[1-9]\\d*)(?:\\.\\d{1,${scale}})?$`).test(value.trim())) return null;
  const [whole, fraction = ""] = value.trim().split(".");
  return BigInt(whole) * BigInt(10) ** BigInt(scale) + BigInt(fraction.padEnd(scale, "0") || "0");
}

export function usdCents(value: string): bigint | null { return decimalUnits(value, 2); }
export function signedUsdCents(value: string): bigint | null {
  const trimmed = value.trim();
  const negative = trimmed.startsWith("-");
  const amount = decimalUnits(negative ? trimmed.slice(1) : trimmed, 2);
  return amount === null ? null : negative ? -amount : amount;
}

export function lineCents(price: string, quantity: number): bigint | null {
  const millionths = decimalUnits(price, 6);
  if (millionths === null || !Number.isSafeInteger(quantity) || quantity <= 0) return null;
  // price * quantity (millionths) -> cents, midpoint away from zero for positive prices.
  return (millionths * BigInt(quantity) + BigInt(5000)) / BigInt(10000);
}

export function settlementCents(received: bigint[], change: bigint[], credit: bigint, debt: bigint): bigint {
  return received.reduce((sum, x) => sum + x, BigInt(0))
    - change.reduce((sum, x) => sum + x, BigInt(0)) + credit + debt;
}

export function excessCreditCents(payment: bigint, currentBalance: bigint): bigint {
  const debt = currentBalance > BigInt(0) ? currentBalance : BigInt(0);
  return payment > debt ? payment - debt : BigInt(0);
}

export function centsNumber(value: bigint): number {
  const number = Number(value);
  if (!Number.isSafeInteger(number)) throw new Error("Amount exceeds the supported USD-cent range.");
  return number / 100;
}

export function formatCents(value: bigint): string {
  const sign = value < BigInt(0) ? "-" : "";
  const absolute = value < BigInt(0) ? -value : value;
  return `${sign}$${absolute / BigInt(100)}.${String(absolute % BigInt(100)).padStart(2, "0")}`;
}
