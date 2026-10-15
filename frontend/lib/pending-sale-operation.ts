/** Minimal non-secret recovery journal. Never store a cart, customer, price, cost, or tender here. */
export interface PendingSaleOperation {
  operationId: string;
  actorId: string;
  createdAtUtc: string;
}

function key(actorId: string) { return `inventstock.pending-sale.${actorId}`; }

export function rememberPendingSale(value: PendingSaleOperation) {
  window.localStorage.setItem(key(value.actorId), JSON.stringify(value));
}

export function readPendingSale(actorId: string): PendingSaleOperation | null {
  const raw = window.localStorage.getItem(key(actorId));
  if (!raw) return null;
  try {
    const value = JSON.parse(raw) as PendingSaleOperation;
    return value.actorId === actorId && /^[0-9a-f-]{36}$/i.test(value.operationId) ? value : null;
  } catch { return null; }
}

export function clearPendingSale(actorId: string, operationId: string) {
  if (readPendingSale(actorId)?.operationId === operationId) window.localStorage.removeItem(key(actorId));
}
