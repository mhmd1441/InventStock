"use client";

import { useParams } from "next/navigation";
import { api } from "@/lib/api";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, Card, Spinner } from "@/components/ui";
import type { Sale } from "@/lib/types";

export default function InternalReceiptPage() {
  const params = useParams<{ saleId: string }>();
  const sale = useQuery({ queryKey: ["sale-receipt", params.saleId],
    queryFn: ({ signal }) => api.get<Sale>(`/api/sales/${params.saleId}`, undefined, signal) });
  if (sale.isLoading) return <Spinner label="Loading internal receipt…" />;
  if (sale.isError || !sale.data) return <Alert>Receipt could not be loaded. Check the sale ID and transaction release setting.</Alert>;
  const item = sale.data;
  return <div className="mx-auto max-w-2xl space-y-5 print:max-w-none">
    <div className="flex items-center justify-between print:hidden"><div><h1 className="text-xl font-semibold">Internal sale receipt</h1><p className="text-sm text-muted">Reprint of an existing posted sale. This is not a legal tax invoice.</p></div><Button onClick={() => window.print()}>Print / reprint</Button></div>
    <Card><div className="space-y-5 p-6"><div className="flex justify-between border-b border-border pb-4"><div><strong className="text-lg">InventStock</strong><p className="text-sm text-muted">Internal sale receipt · not a tax invoice</p></div><div className="text-right"><p className="text-lg font-semibold">#{item.receiptNumber}</p><p className="text-sm text-muted">{new Date(item.postedAtUtc).toLocaleString()}</p></div></div>
      <div className="text-sm"><p><strong>Customer:</strong> {item.customerName ?? "Walk-in customer"}</p></div>
      <div className="space-y-2 text-sm">{item.lines.map((line) => <div key={line.id} className="flex justify-between gap-3"><span>{line.partNumber} · {line.name} · {line.quantity} {line.stockUnit === "Centimeter" ? "cm" : "pc"}</span><span>${line.lineTotalUsd.toFixed(2)}</span></div>)}</div>
      <div className="border-t border-border pt-3 text-right text-lg font-semibold">Total ${item.totalUsd.toFixed(2)}</div>
      <div className="text-xs text-muted">Payment received: ${item.paymentsUsd.toFixed(2)} · Change: ${item.changeUsd.toFixed(2)} · Debt: ${item.unpaidDebtUsd.toFixed(2)}</div>
    </div></Card>
  </div>;
}
