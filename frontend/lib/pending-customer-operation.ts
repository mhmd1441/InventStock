/** Non-secret financial-operation recovery marker, scoped to its original actor. */
export interface PendingCustomerOperation {
  kind: "profile" | "payment" | "paperPayment" | "correction" | "opening" | "openingCorrection";
  operationId: string;
  actorId: string;
  customerId: string;
  createdAtUtc: string;
}
function key(actorId: string) { return `inventstock.pending-customer.${actorId}`; }
export function rememberPendingCustomerOperation(value: PendingCustomerOperation) {
  window.localStorage.setItem(key(value.actorId), JSON.stringify(value));
}
export function readPendingCustomerOperation(actorId: string): PendingCustomerOperation | null {
  const raw = window.localStorage.getItem(key(actorId));
  if (!raw) return null;
  try {
    const value = JSON.parse(raw) as PendingCustomerOperation;
    return value.actorId === actorId && ["profile", "payment", "paperPayment", "correction", "opening", "openingCorrection"].includes(value.kind) ? value : null;
  } catch { return null; }
}
export function clearPendingCustomerOperation(actorId: string, operationId: string) {
  if (readPendingCustomerOperation(actorId)?.operationId === operationId) window.localStorage.removeItem(key(actorId));
}
