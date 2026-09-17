"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/lib/auth";
import { usePart, usePartMovements, useSetPartActive } from "@/lib/queries";
import { formatCurrency, formatDateTime } from "@/lib/format";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Pagination,
  Spinner,
} from "@/components/ui";
import { MovementTable } from "@/components/movement-table";
import { PartFormModal } from "@/components/part-form";
import { StockMovementModal } from "@/components/stock-dialog";

export default function PartDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const { isAdmin } = useAuth();

  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState(false);
  const [movement, setMovement] = useState(false);

  const { data: part, isLoading, error } = usePart(id);
  const movements = usePartMovements(id, page);
  const setActive = useSetPartActive();

  if (isLoading) return <Spinner label="Loading part…" />;
  if (error) return <Alert>{(error as Error).message}</Alert>;
  if (!part) return null;

  const details = [
    { label: "Category", value: part.categoryName },
    { label: "Supplier", value: part.supplierName ?? "—" },
    { label: "Unit price", value: formatCurrency(part.unitPrice) },
    { label: "Stock value", value: formatCurrency(part.stockValue) },
    { label: "Reorder threshold", value: String(part.lowStockThreshold) },
    { label: "Barcode", value: part.barcode ?? "—" },
    { label: "Added", value: formatDateTime(part.createdAtUtc) },
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <Link href="/parts" className="text-sm text-muted hover:underline">
            ← Back to parts
          </Link>
          <h1 className="flex flex-wrap items-center gap-2 text-xl font-semibold tracking-tight">
            {part.partNumber}
            {part.isLowStock && <Badge tone="warning">Needs reorder</Badge>}
            {!part.isActive && <Badge tone="danger">Inactive</Badge>}
          </h1>
          <p className="text-sm text-muted">{part.name}</p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button onClick={() => setMovement(true)} disabled={!part.isActive}>
            Record movement
          </Button>
          {isAdmin && (
            <>
              <Button variant="secondary" onClick={() => setEditing(true)}>
                Edit
              </Button>
              <Button
                variant={part.isActive ? "danger" : "secondary"}
                disabled={setActive.isPending}
                onClick={() =>
                  setActive.mutate({ id: part.id, isActive: !part.isActive })
                }
              >
                {part.isActive ? "Deactivate" : "Reactivate"}
              </Button>
            </>
          )}
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="rounded-xl border border-border bg-surface p-5 shadow-sm">
          <p className="text-xs font-medium uppercase tracking-wide text-muted">
            On hand
          </p>
          <p
            className={`mt-2 text-4xl font-semibold tabular-nums ${
              part.isLowStock ? "text-warning" : ""
            }`}
          >
            {part.quantityOnHand}
          </p>
          <p className="mt-1 text-xs text-muted">
            Derived from the movement ledger
          </p>
        </div>

        <Card title="Details" className="lg:col-span-2">
          <dl className="grid gap-x-6 gap-y-3 p-5 sm:grid-cols-2">
            {details.map((detail) => (
              <div key={detail.label} className="flex justify-between gap-3">
                <dt className="text-sm text-muted">{detail.label}</dt>
                <dd className="text-sm font-medium">{detail.value}</dd>
              </div>
            ))}
          </dl>
          {part.description && (
            <p className="border-t border-border px-5 py-3 text-sm text-muted">
              {part.description}
            </p>
          )}
        </Card>
      </div>

      <Card title="Movement history">
        {movements.isLoading ? (
          <Spinner label="Loading history…" />
        ) : !movements.data || movements.data.items.length === 0 ? (
          <EmptyState message="No movements recorded for this part yet." />
        ) : (
          <>
            <MovementTable movements={movements.data.items} />
            <Pagination
              page={movements.data.page}
              totalPages={movements.data.totalPages}
              totalCount={movements.data.totalCount}
              onChange={setPage}
            />
          </>
        )}
      </Card>

      {editing && (
        <PartFormModal part={part} onClose={() => setEditing(false)} />
      )}
      {movement && (
        <StockMovementModal part={part} onClose={() => setMovement(false)} />
      )}
    </div>
  );
}
