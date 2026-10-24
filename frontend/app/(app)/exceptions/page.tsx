"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { useAuth } from "@/lib/auth";
import { useTransactionExceptions } from "@/lib/queries";
import { formatStockQuantity } from "@/lib/quantity";
import { Alert, Badge, Button, Card, Field, Input, Select, Spinner } from "@/components/ui";
import type { TransactionExceptionKind, TransactionExceptionQuery } from "@/lib/types";

const kinds: { value: "" | TransactionExceptionKind; label: string }[] = [
  { value: "", label: "All exception types" },
  { value: "LinkedReturn", label: "Linked returns" },
  { value: "LegacyReturn", label: "Sold before InventStock" },
  { value: "StockShortage", label: "Stock shortage sales" },
  { value: "UncountedSale", label: "Uncounted-stock sales" },
  { value: "CustomerPaymentCorrection", label: "Customer payment corrections" },
  { value: "CustomerOpeningCorrection", label: "Opening-balance corrections" },
  { value: "OutgoingPaymentReversal", label: "Cash-outgoing reversals" },
  { value: "SaleTenderAccountCorrection", label: "Sale tender account corrections" },
  { value: "PaperRecovery", label: "Paper recovery" },
];
const label = (kind: TransactionExceptionKind) => kinds.find((item) => item.value === kind)?.label ?? kind;
const startUtc = (day: string) => day ? new Date(`${day}T00:00:00`).toISOString() : undefined;
const endUtc = (day: string) => {
  if (!day) return undefined;
  const date = new Date(`${day}T00:00:00`); date.setDate(date.getDate() + 1); return date.toISOString();
};
const initialFrom = () => { const date = new Date(); date.setDate(date.getDate() - 30); return date.toISOString().slice(0, 10); };

export default function ExceptionsPage() {
  const { canViewPrivate } = useAuth();
  const [from, setFrom] = useState(initialFrom);
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [kind, setKind] = useState<"" | TransactionExceptionKind>("");
  const [query, setQuery] = useState<TransactionExceptionQuery>(() => ({
    fromUtc: startUtc(initialFrom()), toUtc: endUtc(new Date().toISOString().slice(0, 10)), page: 1, pageSize: 30,
  }));
  const result = useTransactionExceptions(query);
  const totals = useMemo(() => {
    const rows = result.data?.items ?? [];
    return rows.reduce<Record<string, number>>((current, row) => {
      current[row.kind] = (current[row.kind] ?? 0) + 1; return current;
    }, {});
  }, [result.data]);

  if (!canViewPrivate)
    return <Alert tone="warning">Reveal admin costs to review private transaction exceptions.</Alert>;

  return <div className="space-y-6">
    <div><h1 className="text-xl font-semibold">Transaction exceptions</h1><p className="text-sm text-muted">Daily owner review of unusual or corrective actions. These records are immutable evidence, not approval to delete history.</p></div>
    <Card title="Filters"><div className="grid gap-3 p-5 md:grid-cols-4"><Field label="From"><Input type="date" value={from} onChange={(event) => setFrom(event.target.value)} /></Field><Field label="Through"><Input type="date" value={to} onChange={(event) => setTo(event.target.value)} /></Field><Field label="Exception type"><Select value={kind} onChange={(event) => setKind(event.target.value as "" | TransactionExceptionKind)}>{kinds.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</Select></Field><div className="flex items-end"><Button onClick={() => setQuery({ fromUtc: startUtc(from), toUtc: endUtc(to), kind: kind || undefined, page: 1, pageSize: 30 })}>Review</Button></div></div></Card>
    <Alert tone="warning">Review does not reverse or approve anything automatically. Follow the linked sale, customer, part, or cash record before taking another action.</Alert>
    {result.isLoading && <Spinner label="Loading exceptions…" />}
    {result.isError && <Alert>Exceptions could not be loaded. Check the date range, transaction release flag, and pending database migrations.</Alert>}
    {result.data && <>
      <div className="flex flex-wrap gap-2">{Object.entries(totals).map(([key, count]) => <Badge key={key} tone="warning">{label(key as TransactionExceptionKind)}: {count}</Badge>)}</div>
      <div className="space-y-3">{result.data.items.length === 0 && <Card><p className="p-5 text-sm text-muted">No exceptions in this range.</p></Card>}{result.data.items.map((item) => <Card key={`${item.kind}-${item.id}`}><div className="space-y-2 p-4"><div className="flex flex-wrap items-start justify-between gap-2"><div><Badge tone={item.kind === "StockShortage" ? "warning" : "accent"}>{label(item.kind)}</Badge><p className="mt-2 font-medium">{item.partNumber ?? item.customerName ?? `${item.itemCount ?? 0} returned line(s)`}</p></div><div className="text-right text-sm"><p>{new Date(item.occurredAtUtc).toLocaleString()}</p>{item.amountUsd !== null && <p className="font-semibold">{item.amountUsd < 0 ? "−" : ""}${Math.abs(item.amountUsd).toFixed(2)} USD</p>}</div></div><p className="text-sm">{item.reason}</p><div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted"><span>By {item.actorName}</span><span>Operation {item.operationId.slice(0, 8)}</span>{item.quantity !== null && item.stockUnit && <span>{formatStockQuantity(item.quantity, item.stockUnit)}</span>}{item.customerName && item.partNumber && <span>Customer {item.customerName}</span>}</div><div className="flex flex-wrap gap-3 text-sm">{item.saleId && <Link className="text-accent hover:underline" href={`/returns?sale=${item.saleId}`}>Open sale / linked return →</Link>}{item.partId && <Link className="text-accent hover:underline" href={`/parts/${item.partId}`}>Open part →</Link>}{item.customerId && <Link className="text-accent hover:underline" href="/customers">Open customers →</Link>}{item.kind === "OutgoingPaymentReversal" && <Link className="text-accent hover:underline" href="/cash">Open cash records →</Link>}</div></div></Card>)}</div>
      <div className="flex items-center justify-between text-sm text-muted"><span>{result.data.totalCount} exception(s) · page {result.data.page} of {Math.max(1, result.data.totalPages)}</span><div className="flex gap-2"><Button variant="ghost" disabled={!result.data.hasPreviousPage} onClick={() => setQuery((current) => ({ ...current, page: current.page - 1 }))}>Previous</Button><Button variant="ghost" disabled={!result.data.hasNextPage} onClick={() => setQuery((current) => ({ ...current, page: current.page + 1 }))}>Next</Button></div></div>
    </>}
  </div>;
}
