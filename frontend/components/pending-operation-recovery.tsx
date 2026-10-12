"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { clearPendingStockOperation, readPendingStockOperation, type PendingStockOperation } from "@/lib/pending-stock-operation";
import { Alert, Button, Modal, Spinner } from "./ui";

type Resolution = { state: "checking" | "committed" | "not-committed" | "error"; message?: string };

export function PendingOperationRecovery({ actorId }: { actorId: string }) {
  const [pending, setPending] = useState<PendingStockOperation | null>(null);
  const [resolution, setResolution] = useState<Resolution>({ state: "checking" });

  useEffect(() => {
    let active = true;
    Promise.resolve().then(async () => {
      const operation = readPendingStockOperation(actorId);
      if (!active) return;
      setPending(operation);
      if (!operation) return;
      setResolution({ state: "checking" });
      try {
        const isHold = operation.kind === "Hold" || operation.kind === "Release";
        await api.get<unknown>(`${isHold ? "/api/stock-hold-operations" : "/api/stock-operations"}/${operation.operationId}`);
        if (!active) return;
        setResolution({
          state: "committed",
          message: `${operation.partNumber}: ${Math.abs(operation.quantity)} unit(s) were already recorded.`,
        });
      } catch (error) {
        if (!active) return;
        setResolution(error instanceof ApiError && error.status === 404
          ? { state: "not-committed", message: "The server has no committed movement for this operation. Physically verify the stock, then enter the action again if needed." }
          : { state: "error", message: error instanceof Error ? error.message : "Could not check the previous operation." });
      }
    });
    return () => { active = false; };
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
