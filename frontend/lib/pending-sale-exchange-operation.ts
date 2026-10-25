/** Recovery journal intentionally stores no cart, customer, money, or private data. */
export interface PendingSaleExchangeOperation { operationId: string; actorId: string; createdAtUtc: string }
const key = (actorId: string) => `inventstock.pending-sale-exchange.${actorId}`;
export function rememberPendingSaleExchange(value: PendingSaleExchangeOperation) {
  window.localStorage.setItem(key(value.actorId), JSON.stringify(value));
}
export function readPendingSaleExchange(actorId: string): PendingSaleExchangeOperation | null {
  const raw = window.localStorage.getItem(key(actorId));
  if (!raw) return null;
  try { const item = JSON.parse(raw) as PendingSaleExchangeOperation;
    return item.actorId === actorId && /^[0-9a-f-]{36}$/i.test(item.operationId) ? item : null; } catch { return null; }
}
export function clearPendingSaleExchange(actorId: string, operationId: string) {
  if (readPendingSaleExchange(actorId)?.operationId === operationId) window.localStorage.removeItem(key(actorId));
}
