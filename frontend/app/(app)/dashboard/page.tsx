"use client";

import Link from "next/link";
import { useDashboard } from "@/lib/queries";
import { formatCurrency, formatNumber } from "@/lib/format";
import { Alert, Badge, Card, EmptyState, Spinner } from "@/components/ui";
import { MovementTable } from "@/components/movement-table";

export default function DashboardPage() {
  const { data, isLoading, error } = useDashboard();

  if (isLoading) return <Spinner label="Loading dashboard…" />;
  if (error) return <Alert>{(error as Error).message}</Alert>;
  if (!data) return null;

  const stats = [
    { label: "Active parts", value: formatNumber(data.activeParts) },
    { label: "Units in stock", value: formatNumber(data.totalStockUnits) },
    ...("totalStockValue" in data ? [{ label: "Stock value", value: formatCurrency(data.totalStockValue) }] : []),
    {
      label: "Needs reorder",
      value: formatNumber(data.lowStockCount),
      tone: data.lowStockCount > 0 ? ("warning" as const) : undefined,
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Dashboard</h1>
        <p className="text-sm text-muted">
          {formatNumber(data.totalCategories)} categories ·{" "}
          {"totalSuppliers" in data ? `${formatNumber(data.totalSuppliers)} suppliers · ` : ""}
          {formatNumber(data.totalParts)} parts on record
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {stats.map((stat) => (
          <div
            key={stat.label}
            className="rounded-xl border border-border bg-surface p-5 shadow-sm"
          >
            <p className="text-xs font-medium uppercase tracking-wide text-muted">
              {stat.label}
            </p>
            <p
              className={`mt-2 text-2xl font-semibold ${
                stat.tone === "warning" ? "text-warning" : ""
              }`}
            >
              {stat.value}
            </p>
          </div>
        ))}
      </div>

      <div className="grid gap-6 xl:grid-cols-2">
        <Card
          title="Needs reorder"
          action={
            <Link
              href="/parts?lowStockOnly=true"
              className="text-sm text-accent hover:underline"
            >
              View all
            </Link>
          }
        >
          {data.lowStockParts.length === 0 ? (
            <EmptyState message="Every part is above its reorder threshold." />
          ) : (
            <ul className="divide-y divide-border">
              {data.lowStockParts.map((part) => (
                <li
                  key={part.id}
                  className="flex items-center justify-between gap-3 px-5 py-3"
                >
                  <div className="min-w-0">
                    <Link
                      href={`/parts/${part.id}`}
                      className="block truncate text-sm font-medium hover:underline"
                    >
                      {part.partNumber}
                    </Link>
                    <p className="truncate text-xs text-muted">{part.name}</p>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    <span className="text-sm font-medium">
                      {part.quantityOnHand}
                    </span>
                    <span className="text-xs text-muted">
                      / {part.lowStockThreshold}
                    </span>
                    <Badge tone="warning">Reorder</Badge>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card
          title="Recent movements"
          action={
            <Link
              href="/movements"
              className="text-sm text-accent hover:underline"
            >
              View all
            </Link>
          }
        >
          {data.recentMovements.length === 0 ? (
            <EmptyState message="No stock has moved yet." />
          ) : (
            <MovementTable movements={data.recentMovements} showPart />
          )}
        </Card>
      </div>
    </div>
  );
}
