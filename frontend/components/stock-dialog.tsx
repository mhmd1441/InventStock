"use client";

import { useState } from "react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useStockAdjust, useStockIn, useStockOut } from "@/lib/queries";
import type { Part } from "@/lib/types";
import { Alert, Badge, Button, Field, Input, Modal } from "./ui";

type Mode = "In" | "Out" | "Adjustment";

export function StockMovementModal({
  part,
  onClose,
}: {
  part: Part;
  onClose: () => void;
}) {
  const { isAdmin } = useAuth();
  const stockIn = useStockIn();
  const stockOut = useStockOut();
  const adjust = useStockAdjust();

  const [mode, setMode] = useState<Mode>("In");
  const [quantity, setQuantity] = useState("1");
  const [reason, setReason] = useState("");
  const [reference, setReference] = useState("");
  const [formError, setFormError] = useState<string | null>(null);

  const modes: Mode[] = isAdmin ? ["In", "Out", "Adjustment"] : ["In", "Out"];
  const pending = stockIn.isPending || stockOut.isPending || adjust.isPending;

  const parsedQuantity = Number(quantity);
  const projected =
    Number.isFinite(parsedQuantity) && quantity !== ""
      ? part.quantityOnHand +
        (mode === "In"
          ? Math.abs(parsedQuantity)
          : mode === "Out"
            ? -Math.abs(parsedQuantity)
            : parsedQuantity)
      : part.quantityOnHand;

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setFormError(null);

    if (!Number.isFinite(parsedQuantity) || parsedQuantity === 0) {
      setFormError("Enter a quantity other than zero.");
      return;
    }

    if (mode !== "In" && reason.trim() === "") {
      setFormError("Stock-out and adjustments require a reason for the audit trail.");
      return;
    }

    try {
      if (mode === "In") {
        await stockIn.mutateAsync({
          partId: part.id,
          quantity: Math.abs(parsedQuantity),
          reason: reason || undefined,
          reference: reference || undefined,
        });
      } else if (mode === "Out") {
        await stockOut.mutateAsync({
          partId: part.id,
          quantity: Math.abs(parsedQuantity),
          reason: reason || undefined,
          reference: reference || undefined,
        });
      } else {
        await adjust.mutateAsync({
          partId: part.id,
          quantityChange: parsedQuantity,
          reason,
          reference: reference || undefined,
        });
      }

      onClose();
    } catch (error) {
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
            {part.quantityOnHand}
            {part.isLowStock && <Badge tone="warning">Reorder</Badge>}
          </span>
        </div>

        <div
          role="radiogroup"
          aria-label="Movement type"
          className="grid grid-cols-3 gap-2"
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
          label={mode === "Adjustment" ? "Quantity change" : "Quantity"}
          hint={
            mode === "Adjustment"
              ? "Use a negative number to write stock off"
              : undefined
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
          hint={mode !== "In" ? "Required — public to shop users; do not enter supplier costs" : "Optional"}
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
            {projected}
          </span>
        </div>

        {projected < 0 && (
          <Alert tone="warning">
            This would take stock below zero and will be rejected by the server.
          </Alert>
        )}
        {mode === "Out" && <Alert tone="warning">Raw stock-out records inventory only, not a sale or payment.</Alert>}

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
