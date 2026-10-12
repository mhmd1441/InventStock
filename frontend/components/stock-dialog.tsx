"use client";

import { useState } from "react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { clearPendingStockOperation, rememberPendingStockOperation } from "@/lib/pending-stock-operation";
import { useStockAdjust, useStockCount, useStockHold, useStockIn, useStockOut, useStockRelease } from "@/lib/queries";
import { formatStockQuantity, stockUnitLabel } from "@/lib/quantity";
import type { Part } from "@/lib/types";
import { Alert, Badge, Button, Field, Input, Modal } from "./ui";

type Mode = "In" | "Out" | "Adjustment" | "PhysicalCount" | "Hold" | "Release";

export function StockMovementModal({
  part,
  onClose,
}: {
  part: Part;
  onClose: () => void;
}) {
  const { isAdmin, user } = useAuth();
  const stockIn = useStockIn();
  const stockOut = useStockOut();
  const adjust = useStockAdjust();
  const count = useStockCount();
  const hold = useStockHold();
  const release = useStockRelease();

  const [mode, setMode] = useState<Mode>("In");
  const [quantity, setQuantity] = useState("1");
  const [reason, setReason] = useState("");
  const [reference, setReference] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  // Kept stable after an uncertain response so pressing Record again replays rather than duplicates.
  const [operationId] = useState(() => crypto.randomUUID());

  const modes: Mode[] = isAdmin ? ["In", "Out", "Hold", "Release", "Adjustment", "PhysicalCount"] : ["In", "Out", "Hold"];
  const pending = stockIn.isPending || stockOut.isPending || adjust.isPending || count.isPending || hold.isPending || release.isPending;

  const parsedQuantity = Number(quantity);
  const projected =
    Number.isFinite(parsedQuantity) && quantity !== ""
      ? mode === "PhysicalCount" ? parsedQuantity - part.quarantinedQuantity : part.quantityOnHand +
        (mode === "In"
          ? Math.abs(parsedQuantity)
          : mode === "Out" || mode === "Hold"
            ? -Math.abs(parsedQuantity)
            : mode === "Release" ? Math.abs(parsedQuantity)
            : parsedQuantity)
      : part.quantityOnHand;

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setFormError(null);

    if (!Number.isFinite(parsedQuantity) || (mode === "PhysicalCount" ? parsedQuantity < 0 : parsedQuantity === 0)) {
      setFormError(mode === "PhysicalCount" ? "A physical count cannot be negative." : "Enter a quantity other than zero.");
      return;
    }

    if (mode !== "In" && mode !== "PhysicalCount" && reason.trim() === "") {
      setFormError("Stock-out and adjustments require a reason for the audit trail.");
      return;
    }

    try {
      if (!user) throw new Error("Your session ended. Sign in again.");
      rememberPendingStockOperation({
        operationId, actorId: user.id, partId: part.id, partNumber: part.partNumber,
        kind: mode, quantity: parsedQuantity, createdAtUtc: new Date().toISOString(),
      });
      if (mode === "In") {
        await stockIn.mutateAsync({
          partId: part.id,
          operationId,
          quantity: Math.abs(parsedQuantity),
          reason: reason || undefined,
          reference: reference || undefined,
        });
      } else if (mode === "Out") {
        await stockOut.mutateAsync({
          partId: part.id,
          operationId,
          quantity: Math.abs(parsedQuantity),
          reason: reason || undefined,
          reference: reference || undefined,
        });
      } else if (mode === "Adjustment") {
        await adjust.mutateAsync({
          partId: part.id,
          operationId,
          quantityChange: parsedQuantity,
          reason,
          reference: reference || undefined,
        });
      } else if (mode === "PhysicalCount") {
        await count.mutateAsync({
          partId: part.id,
          operationId,
          countedQuantity: parsedQuantity,
          expectedBookQuantity: part.quantityOnHand,
          reason: reason || undefined,
        });
      } else if (mode === "Hold") {
        await hold.mutateAsync({ partId: part.id, operationId, quantity: Math.abs(parsedQuantity), reason, physicalSeparationConfirmed: true });
      } else {
        await release.mutateAsync({ partId: part.id, operationId, quantity: Math.abs(parsedQuantity), reason });
      }

      clearPendingStockOperation(operationId);
      onClose();
    } catch (error) {
      // An HTTP problem is a definite server answer. Network/abort failures remain unresolved.
      if (error instanceof ApiError) clearPendingStockOperation(operationId);
      setFormError(
        error instanceof ApiError
          ? [error.message, ...error.fieldMessages].join(" ")
          : "Could not record the movement.",
      );
    }
  };

  return (
    <Modal title={`Stock movement — ${part.partNumber}`} onClose={onClose}>
      <form onSubmit={submit} className="space-y-4">
        {formError && <Alert>{formError}</Alert>}

        <div className="flex items-center justify-between rounded-lg bg-surface-muted px-3 py-2 text-sm">
          <span className="text-muted">Currently on hand</span>
          <span className="flex items-center gap-2 font-medium tabular-nums">
            {part.stockConfidence === "NotCounted" ? "Not counted" : formatStockQuantity(part.quantityOnHand, part.stockUnit)}
            {part.isLowStock && <Badge tone="warning">Reorder</Badge>}
          </span>
        </div>

        <div
          role="radiogroup"
          aria-label="Movement type"
          className="grid grid-cols-2 gap-2 sm:grid-cols-3"
        >
          {modes.map((option) => (
            <Button
              key={option}
              type="button"
              role="radio"
              aria-checked={mode === option}
              variant={mode === option ? "primary" : "secondary"}
              onClick={() => setMode(option)}
            >
              {option}
            </Button>
          ))}
        </div>

        <Field
          label={mode === "Adjustment" ? "Quantity change" : mode === "PhysicalCount" ? "Physically counted quantity" : "Quantity"}
          hint={
            mode === "Adjustment"
              ? "Use a negative number to write stock off"
              : `Enter whole ${stockUnitLabel(part.stockUnit)}${part.stockUnit === "Centimeter" ? " (150 = 1.5 metres)" : ""}`
          }
        >
          <Input
            type="number"
            step="1"
            value={quantity}
            onChange={(event) => setQuantity(event.target.value)}
            autoFocus
          />
        </Field>

        <Field
          label="Reason"
          hint={mode !== "In" && mode !== "PhysicalCount" ? "Required — public to shop users; do not enter supplier costs" : "Optional"}
        >
          <Input
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder={
              mode === "Adjustment" ? "Damaged in storage" : "Workshop issue"
            }
          />
        </Field>

        <Field label="Reference" hint="Optional — order or job number">
          <Input
            value={reference}
            onChange={(event) => setReference(event.target.value)}
            placeholder="WO-1042"
          />
        </Field>

        <div className="flex items-center justify-between rounded-lg bg-surface-muted px-3 py-2 text-sm">
          <span className="text-muted">After this movement</span>
          <span
            className={`font-medium tabular-nums ${
              projected < 0
                ? "text-danger"
                : projected <= part.lowStockThreshold
                  ? "text-warning"
                  : ""
            }`}
          >
            {mode === "PhysicalCount" || part.stockConfidence === "Counted" ? formatStockQuantity(projected, part.stockUnit) : "Still not counted"}
          </span>
        </div>

        {projected < 0 && (
          <Alert tone="warning">
            This would take stock below zero and will be rejected by the server.
          </Alert>
        )}
        {mode === "Out" && <Alert tone="warning">Raw stock-out records inventory only, not a sale or payment.</Alert>}
        {mode === "PhysicalCount" && <Alert tone="warning">Count the physical stock now. If another stock action commits first, the server will reject this and require a recount.</Alert>}
        {mode === "Hold" && <Alert tone="warning">Before recording, physically separate or clearly tag these goods. They become unavailable for sale immediately.</Alert>}
        {mode === "Release" && <Alert tone="warning">Release only goods physically verified as sellable. This action is admin-only.</Alert>}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={pending}>
            {pending ? "Recording…" : "Record movement"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
