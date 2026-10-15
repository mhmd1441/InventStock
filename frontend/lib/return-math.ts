import { usdCents } from "./sale-math";

/** Allocate against the original posted line, never round each returned unit independently. */
export function additionalReturnCents(lineTotalUsd: number, soldQuantity: number,
  returnedQuantity: number, refundedUsd: number, requestedQuantity: number): bigint | null {
  const total = usdCents(lineTotalUsd.toFixed(2));
  const refunded = usdCents(refundedUsd.toFixed(2));
  if (total === null || refunded === null || !Number.isSafeInteger(soldQuantity) || soldQuantity < 1 ||
    !Number.isSafeInteger(returnedQuantity) || returnedQuantity < 0 ||
    !Number.isSafeInteger(requestedQuantity) || requestedQuantity < 1 ||
    returnedQuantity + requestedQuantity > soldQuantity) return null;
  const cumulative = BigInt(returnedQuantity + requestedQuantity);
  const target = (total * cumulative + BigInt(Math.floor(soldQuantity / 2))) / BigInt(soldQuantity);
  return target - refunded;
}
