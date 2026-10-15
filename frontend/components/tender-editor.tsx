"use client";

import type { SalePaymentAccount } from "@/lib/types";
import type { TenderDraft } from "@/lib/tender-math";
import { Button, Field, Input, Select } from "./ui";

export const newTender = (account?: SalePaymentAccount): TenderDraft => ({ key: crypto.randomUUID(), accountId: account?.id ?? "", native: "", usd: "" });

export function TenderEditor({ row, accounts, title, onPatch, onRemove }: {
  row: TenderDraft; accounts: SalePaymentAccount[]; title: string;
  onPatch: (patch: Partial<TenderDraft>) => void; onRemove: () => void;
}) {
  const account = accounts.find((x) => x.id === row.accountId);
  return <div className="mb-3 space-y-2 rounded-lg border border-border p-3">
    <div className="flex items-center justify-between text-sm font-medium"><span>{title}</span><Button variant="ghost" onClick={onRemove}>Remove</Button></div>
    <Select value={row.accountId} onChange={(e) => onPatch({ accountId: e.target.value, native: "", usd: "" })}>
      <option value="">Choose account</option>
      {accounts.map((item) => <option key={item.id} value={item.id}>{item.name} · {item.currency} {item.kind === "Whish" ? "(Whish)" : "(safe)"}</option>)}
    </Select>
    <div className="grid grid-cols-2 gap-2">
      <Field label={`Native ${account?.currency ?? "amount"}`}><Input inputMode="decimal" value={row.native} onChange={(e) => onPatch({ native: e.target.value, usd: account?.currency === "USD" ? e.target.value : row.usd })} /></Field>
      <Field label="Confirmed USD"><Input inputMode="decimal" value={row.usd} disabled={account?.currency === "USD"} onChange={(e) => onPatch({ usd: e.target.value })} /></Field>
    </div>
  </div>;
}
