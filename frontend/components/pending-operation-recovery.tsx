"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { clearPendingStockOperation, readPendingStockOperation, type PendingStockOperation } from "@/lib/pending-stock-operation";
import type { StockOperationStatus } from "@/lib/types";
import { Alert, Button, Modal, Spinner } from "./ui";

type Resolution = { state: "checking" | "committed" | "not-committed" | "error"; message?: string };

export function PendingOperationRecovery({ actorId }: { actorId: string }) {
  const [pending, setPending] = useState<PendingStockOperation | null>(null);
  const [resolution, setResolution] = useState<Resolution>({ state: "checking" });

  useEffect(() => {
    const operation = readPendingStockOperation(actorId);
    setPending(operation);
    if (!operation) return;
    setResolution({ state: "checking" });
    api.get<StockOperationStatus>(`/api/stock-operations/${operation.operationId}`)
      .then((result) => setResolution({
        state: "committed",
        message: `${result.movement.partNumber}: ${Math.abs(result.movement.quantityChange)} unit(s) were already recorded.`,
      }))
      .catch((error) => setResolution(error instanceof ApiError && error.status === 404
        ? { state: "not-committed", message: "The server has no committed movement for this operation. Physically verify the stock, then enter the action again if needed." }
        : { state: "error", message: error instanceof Error ? error.message : "Could not check the previous operation." }));
  }, [actorId]);

  if (!pending) return null;
  const acknowledge = () => { clearPendingStockOperation(pending.operationId); setPending(null); };
  return <Modal title="Resolve previous stock action" onClose={() => undefined}>
    <div className="space-y-4">
      <p className="text-sm text-muted">{pending.kind} {Math.abs(pending.quantity)} unit(s) — {pending.partNumber}</p>
      {resolution.state === "checking" ? <Spinner label="Checking whether it committed…" /> :
        <Alert tone={resolution.state === "committed" ? "success" : resolution.state === "error" ? "warning" : "warning"}>{resolution.message}</Alert>}
      {resolution.state === "error" && <div className="flex justify-end"><Button onClick={() => window.location.reload()}>Retry check</Button></div>}
      {resolution.state !== "checking" && resolution.state !== "error" &&
        <div className="flex justify-end"><Button onClick={acknowledge}>Acknowledge</Button></div>}
    </div>
  </Modal>;
}
