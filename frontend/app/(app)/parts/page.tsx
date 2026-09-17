"use client";

import { Suspense, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { useCategories, useParts } from "@/lib/queries";
import { formatCurrency } from "@/lib/format";
import type { Part, PartQuery, PartSortField } from "@/lib/types";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Input,
  Pagination,
  Select,
  Spinner,
  Table,
  Td,
  Th,
} from "@/components/ui";
import { PartFormModal } from "@/components/part-form";
import { StockMovementModal } from "@/components/stock-dialog";

const sortOptions: { value: PartSortField; label: string }[] = [
  { value: "PartNumber", label: "Part number" },
  { value: "Name", label: "Name" },
  { value: "QuantityOnHand", label: "Quantity" },
  { value: "UnitPrice", label: "Unit price" },
  { value: "CreatedAtUtc", label: "Recently added" },
];

function PartsPageContent() {
  const searchParams = useSearchParams();
  const { isAdmin } = useAuth();

  const [searchInput, setSearchInput] = useState("");
  const [query, setQuery] = useState<PartQuery>({
    page: 1,
    pageSize: 20,
    sortBy: "PartNumber",
    lowStockOnly: searchParams.get("lowStockOnly") === "true",
  });

  const [editing, setEditing] = useState<Part | null>(null);
  const [creating, setCreating] = useState(false);
  const [movementPart, setMovementPart] = useState<Part | null>(null);

  // Debounce so a fast typist does not fire a request per keystroke.
  useEffect(() => {
    const timer = setTimeout(
      () => setQuery((current) => ({ ...current, search: searchInput, page: 1 })),
      300,
    );

    return () => clearTimeout(timer);
  }, [searchInput]);

  const { data: categories } = useCategories();
  const { data, isLoading, error, isFetching } = useParts(query);

  const categoryOptions = useMemo(
    () => categories ?? [],
    [categories],
  );

  const patch = (changes: Partial<PartQuery>) =>
    setQuery((current) => ({ ...current, ...changes, page: changes.page ?? 1 }));

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Parts</h1>
          <p className="text-sm text-muted">
            Search the catalogue and record stock movements.
          </p>
        </div>
        {isAdmin && <Button onClick={() => setCreating(true)}>New part</Button>}
      </div>

      <Card>
        <div className="grid gap-3 border-b border-border p-4 sm:grid-cols-2 xl:grid-cols-4">
          <Input
            type="search"
            placeholder="Search part number, name or barcode"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            aria-label="Search parts"
          />

          <Select
            value={query.categoryId ?? ""}
            aria-label="Filter by category"
            onChange={(event) =>
              patch({ categoryId: event.target.value || undefined })
            }
          >
            <option value="">All categories</option>
            {categoryOptions.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </Select>

          <Select
            value={query.sortBy}
            aria-label="Sort by"
            onChange={(event) =>
              patch({ sortBy: event.target.value as PartSortField })
            }
          >
            {sortOptions.map((option) => (
              <option key={option.value} value={option.value}>
                Sort: {option.label}
              </option>
            ))}
          </Select>

          <div className="flex flex-wrap items-center gap-4 text-sm">
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={query.lowStockOnly ?? false}
                onChange={(event) =>
                  patch({ lowStockOnly: event.target.checked })
                }
              />
              Needs reorder
            </label>
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={query.includeInactive ?? false}
                onChange={(event) =>
                  patch({ includeInactive: event.target.checked })
                }
              />
              Include inactive
            </label>
          </div>
        </div>

        {error && (
          <div className="p-4">
            <Alert>{(error as Error).message}</Alert>
          </div>
        )}

        {isLoading ? (
          <Spinner label="Loading parts…" />
        ) : !data || data.items.length === 0 ? (
          <EmptyState message="No parts match these filters." />
        ) : (
          <>
            <div className={isFetching ? "opacity-60 transition" : undefined}>
              <Table>
                <thead>
                  <tr>
                    <Th>Part</Th>
                    <Th>Category</Th>
                    <Th>Supplier</Th>
                    <Th className="text-right">On hand</Th>
                    <Th className="text-right">Unit price</Th>
                    <Th className="text-right">Value</Th>
                    <Th />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((part) => (
                    <tr key={part.id}>
                      <Td>
                        <Link
                          href={`/parts/${part.id}`}
                          className="font-medium hover:underline"
                        >
                          {part.partNumber}
                        </Link>
                        <span className="block text-xs text-muted">
                          {part.name}
                        </span>
                      </Td>
                      <Td className="text-muted">{part.categoryName}</Td>
                      <Td className="text-muted">{part.supplierName ?? "—"}</Td>
                      <Td className="text-right tabular-nums">
                        <span className="font-medium">{part.quantityOnHand}</span>
                        <span className="ml-2 inline-flex gap-1">
                          {part.isLowStock && <Badge tone="warning">Reorder</Badge>}
                          {!part.isActive && <Badge>Inactive</Badge>}
                        </span>
                      </Td>
                      <Td className="text-right tabular-nums text-muted">
                        {formatCurrency(part.unitPrice)}
                      </Td>
                      <Td className="text-right tabular-nums">
                        {formatCurrency(part.stockValue)}
                      </Td>
                      <Td className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="secondary"
                            onClick={() => setMovementPart(part)}
                            disabled={!part.isActive}
                          >
                            Stock
                          </Button>
                          {isAdmin && (
                            <Button
                              variant="ghost"
                              onClick={() => setEditing(part)}
                            >
                              Edit
                            </Button>
                          )}
                        </div>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>

            <Pagination
              page={data.page}
              totalPages={data.totalPages}
              totalCount={data.totalCount}
              onChange={(page) => setQuery((current) => ({ ...current, page }))}
            />
          </>
        )}
      </Card>

      {(creating || editing) && (
        <PartFormModal
          part={editing}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
        />
      )}

      {movementPart && (
        <StockMovementModal
          part={movementPart}
          onClose={() => setMovementPart(null)}
        />
      )}
    </div>
  );
}

export default function PartsPage() {
  return (
    <Suspense fallback={<Spinner label="Loading parts…" />}>
      <PartsPageContent />
    </Suspense>
  );
}
