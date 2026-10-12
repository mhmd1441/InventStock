export const PENDING_STOCK_OPERATION_KEY = "inventstock.pending-stock-operation";

export interface PendingStockOperation {
  operationId: string;
  actorId: string;
  partId: string;
  partNumber: string;
  kind: "In" | "Out" | "Adjustment" | "PhysicalCount" | "Hold" | "Release";
  quantity: number;
  createdAtUtc: string;
}

export function rememberPendingStockOperation(value: PendingStockOperation) {
  window.localStorage.setItem(PENDING_STOCK_OPERATION_KEY, JSON.stringify(value));
}

export function readPendingStockOperation(actorId: string): PendingStockOperation | null {
  const raw = window.localStorage.getItem(PENDING_STOCK_OPERATION_KEY);
  if (!raw) return null;
  try {
    const value = JSON.parse(raw) as PendingStockOperation;
    return value.actorId === actorId ? value : null;
  } catch {
    window.localStorage.removeItem(PENDING_STOCK_OPERATION_KEY);
    return null;
  }
}

export function clearPendingStockOperation(operationId: string) {
  const raw = window.localStorage.getItem(PENDING_STOCK_OPERATION_KEY);
  if (!raw) return;
  try {
    const value = JSON.parse(raw) as PendingStockOperation;
    if (value.operationId === operationId)
      window.localStorage.removeItem(PENDING_STOCK_OPERATION_KEY);
  } catch {
    window.localStorage.removeItem(PENDING_STOCK_OPERATION_KEY);
  }
}
