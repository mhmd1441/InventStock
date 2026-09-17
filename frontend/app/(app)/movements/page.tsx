"use client";

import { useState } from "react";
import { useMovements } from "@/lib/queries";
import {
  Alert,
  Card,
  EmptyState,
  Pagination,
  Spinner,
} from "@/components/ui";
import { MovementTable } from "@/components/movement-table";

export default function MovementsPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading, error } = useMovements(page);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Stock movements</h1>
        <p className="text-sm text-muted">
          Every change to stock, with the user who made it.
        </p>
      </div>

      <Card>
        {error && (
          <div className="p-4">
            <Alert>{(error as Error).message}</Alert>
          </div>
        )}

        {isLoading ? (
          <Spinner label="Loading movements…" />
        ) : !data || data.items.length === 0 ? (
          <EmptyState message="No stock has moved yet." />
        ) : (
          <>
            <MovementTable movements={data.items} showPart />
            <Pagination
              page={data.page}
              totalPages={data.totalPages}
              totalCount={data.totalCount}
              onChange={setPage}
            />
          </>
        )}
      </Card>
    </div>
  );
}
