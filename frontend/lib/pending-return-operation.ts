/** Only an operation ID and actor are persisted; no customer, sale, price, or refund details. */
export interface PendingReturnOperation { operationId: string; actorId: string; createdAtUtc: string }
const key = (actorId: string) => `inventstock.pending-return.${actorId}`;
export function rememberPendingReturn(value: PendingReturnOperation) {
  window.localStorage.setItem(key(value.actorId), JSON.stringify(value));
}
export function readPendingReturn(actorId: string): PendingReturnOperation | null {
  const raw = window.localStorage.getItem(key(actorId));
  if (!raw) return null;
  try {
    const value = JSON.parse(raw) as PendingReturnOperation;
    return value.actorId === actorId && /^[0-9a-f-]{36}$/i.test(value.operationId) ? value : null;
  } catch { return null; }
}
export function clearPendingReturn(actorId: string, operationId: string) {
  if (readPendingReturn(actorId)?.operationId === operationId) window.localStorage.removeItem(key(actorId));
}
