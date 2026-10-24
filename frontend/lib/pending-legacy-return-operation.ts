/** Recovery journal intentionally stores no part, customer, price, refund, or private data. */
export interface PendingLegacyReturnOperation { operationId: string; actorId: string; createdAtUtc: string }
const key = (actorId: string) => `inventstock.pending-legacy-return.${actorId}`;
export function rememberPendingLegacyReturn(value: PendingLegacyReturnOperation) {
  window.localStorage.setItem(key(value.actorId), JSON.stringify(value));
}
export function readPendingLegacyReturn(actorId: string): PendingLegacyReturnOperation | null {
  const raw = window.localStorage.getItem(key(actorId));
  if (!raw) return null;
  try {
    const value = JSON.parse(raw) as PendingLegacyReturnOperation;
    return value.actorId === actorId && /^[0-9a-f-]{36}$/i.test(value.operationId) ? value : null;
  } catch { return null; }
}
export function clearPendingLegacyReturn(actorId: string, operationId: string) {
  if (readPendingLegacyReturn(actorId)?.operationId === operationId)
    window.localStorage.removeItem(key(actorId));
}
