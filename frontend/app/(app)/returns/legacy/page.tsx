"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useCustomers, useParts, usePostLegacySaleReturn, useSalePaymentAccounts } from "@/lib/queries";
import { centsNumber, formatCents, usdCents } from "@/lib/sale-math";
import { formatStockQuantity, saleQuantityInBaseUnits } from "@/lib/quantity";
import { parseTenderDrafts, preferredTenderAccount, type TenderDraft } from "@/lib/tender-math";
import { clearPendingLegacyReturn, readPendingLegacyReturn, rememberPendingLegacyReturn,
  type PendingLegacyReturnOperation } from "@/lib/pending-legacy-return-operation";
import { TenderEditor, newTender } from "@/components/tender-editor";
import { Alert, Button, Card, Field, Input, Select, Spinner, Textarea } from "@/components/ui";
import type { CustomerSummary, LegacySaleReturn, Part, PostLegacySaleReturnRequest,
  ReturnCondition } from "@/lib/types";

type LineDraft = { key: string; part: Part; quantity: string; refund: string;
  condition: ReturnCondition; separated: boolean; issueReason: string };
const money = (value: number) => formatCents(BigInt(Math.round(value * 100)));
const errorText = (error: unknown) => error instanceof ApiError
  ? [error.message, ...error.fieldMessages].join(" ")
  : "The response was lost. Check the operation before trying another return.";

export default function LegacyReturnPage() {
  const { user } = useAuth();
  const [search, setSearch] = useState("");
  const [cart, setCart] = useState<LineDraft[]>([]);
  const [customerSearch, setCustomerSearch] = useState("");
  const [customer, setCustomer] = useState<CustomerSummary | null>(null);
  const [originalDate, setOriginalDate] = useState("");
  const [originalReference, setOriginalReference] = useState("");
  const [reason, setReason] = useState("");
  const [accountReduction, setAccountReduction] = useState("0");
  const [refunds, setRefunds] = useState<TenderDraft[]>([]);
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const [review, setReview] = useState<PostLegacySaleReturnRequest | null>(null);
  const [posted, setPosted] = useState<LegacySaleReturn | null>(null);
  const [pending, setPending] = useState<PendingLegacyReturnOperation | null>(null);
  const [recovery, setRecovery] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const parts = useParts({ search, page: 1, pageSize: 12 });
  const customers = useCustomers(customerSearch);
  const accounts = useSalePaymentAccounts();
  const post = usePostLegacySaleReturn();
  const paymentAccounts = accounts.data ?? [];

  useEffect(() => {
    if (!user) return;
    let active = true;
    const item = readPendingLegacyReturn(user.id);
    queueMicrotask(() => { if (active) setPending(item); });
    if (item) void api.get<LegacySaleReturn>(`/api/sale-returns/legacy/operations/${item.operationId}`)
      .then((result) => {
        if (!active) return;
        clearPendingLegacyReturn(user.id, item.operationId); setPending(null); setPosted(result);
        setReview(null); setOperationId(crypto.randomUUID());
      }).catch((caught: unknown) => {
        if (!active) return;
        setRecovery(caught instanceof ApiError && caught.status === 404
          ? "No committed return found yet. Confirm before clearing this attempt."
          : "Could not establish return status; keep this attempt pending.");
      });
    return () => { active = false; };
  }, [user]);

  const total = useMemo(() => cart.reduce<bigint | null>((sum, row) => {
    if (sum === null) return null;
    const cents = usdCents(row.refund);
    return cents === null || cents <= BigInt(0) ? null : sum + cents;
  }, cart.length ? BigInt(0) : null), [cart]);
  const patchLine = (key: string, patch: Partial<LineDraft>) => {
    setCart((rows) => rows.map((row) => row.key === key ? { ...row, ...patch } : row)); setReview(null);
  };
  const addPart = (part: Part) => {
    if (cart.some((row) => row.part.id === part.id)) return;
    setCart((rows) => [...rows, { key: crypto.randomUUID(), part, quantity: part.stockUnit === "Centimeter" ? "1.00" : "1",
      refund: "", condition: "Sellable", separated: false, issueReason: "" }]);
    setReview(null);
  };
  const checkPending = async (item: PendingLegacyReturnOperation) => {
    setRecovery(null);
    try {
      const result = await api.get<LegacySaleReturn>(`/api/sale-returns/legacy/operations/${item.operationId}`);
      if (user) clearPendingLegacyReturn(user.id, item.operationId);
      setPending(null); setPosted(result); setReview(null); setOperationId(crypto.randomUUID());
    } catch (caught) {
      setRecovery(caught instanceof ApiError && caught.status === 404
        ? "No committed return found. Clear only after confirming it did not arrive late."
        : "Status could not be checked. Do not submit a replacement yet.");
    }
  };
  const prepare = () => {
    setError(null); setPosted(null);
    try {
      if (!cart.length || total === null) throw new Error("Add at least one known SKU and enter each returned value.");
      if (!reason.trim() || reason.length > 500) throw new Error("Enter a reason (maximum 500 characters).");
      if (originalReference.length > 200) throw new Error("Old receipt/reference is limited to 200 characters.");
      const lines = cart.map((row) => {
        const quantity = saleQuantityInBaseUnits(row.quantity, row.part.stockUnit);
        const refund = usdCents(row.refund);
        if (quantity === null || quantity <= 0) throw new Error(row.part.stockUnit === "Centimeter"
          ? "Returned hose length must be metres to at most two decimal places."
          : "Returned piece quantity must be a positive whole number.");
        if (refund === null || refund <= BigInt(0)) throw new Error("Each item needs a positive USD return value.");
        if (row.condition === "Quarantined" && (!row.separated || !row.issueReason.trim()))
          throw new Error("Physically separate or tag faulty goods and enter the issue.");
        return { partId: row.part.id, quantity, refundUsd: centsNumber(refund), condition: row.condition,
          physicalSeparationConfirmed: row.condition === "Quarantined" && row.separated,
          issueReason: row.condition === "Quarantined" ? row.issueReason.trim() : null };
      });
      const account = usdCents(accountReduction);
      if (account === null || account < BigInt(0)) throw new Error("Account reduction must be a nonnegative USD amount.");
      if (!customer && account > BigInt(0)) throw new Error("Choose a customer before reducing an account balance.");
      const parsed = parseTenderDrafts(refunds, paymentAccounts);
      const refunded = parsed.reduce((sum, row) => sum + usdCents(row.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      if (total !== account + refunded) throw new Error(`Allocate the full ${formatCents(total)} between refunds and account reduction.`);
      setReview({ operationId, customerId: customer?.customer.id ?? null,
        originalSaleDate: originalDate || null, originalReference: originalReference.trim() || null,
        reason: reason.trim(), confirmedTotalUsd: centsNumber(total), accountReductionUsd: centsNumber(account),
        lines, refunds: parsed });
    } catch (caught) { setError(caught instanceof Error ? caught.message : "Cannot prepare this return."); }
  };
  const submit = async () => {
    if (!review || !user || pending || post.isPending) return;
    const journal = { operationId: review.operationId, actorId: user.id, createdAtUtc: new Date().toISOString() };
    rememberPendingLegacyReturn(journal); setPending(journal); setError(null);
    try {
      const result = await post.mutateAsync(review);
      clearPendingLegacyReturn(user.id, journal.operationId); setPending(null); setPosted(result);
      setReview(null); setCart([]); setRefunds([]); setCustomer(null); setAccountReduction("0");
      setReason(""); setOriginalDate(""); setOriginalReference(""); setOperationId(crypto.randomUUID());
    } catch (caught) { setError(errorText(caught)); }
  };

  return <div className="space-y-6">
    <div className="flex flex-wrap items-start justify-between gap-3"><div><h1 className="text-xl font-semibold">Sold before InventStock</h1><p className="text-sm text-muted">Use only when no InventStock sale exists. This creates a clearly flagged manual return record.</p></div><Link href="/returns" className="rounded-lg border border-border px-3.5 py-2 text-sm font-medium hover:bg-surface-muted">Use linked return</Link></div>
    <Alert tone="warning">Search for the original InventStock sale first. This manual path has no historical quantity or refund cap, so the old receipt/reference and reason are permanent audit evidence.</Alert>
    {pending && <Alert tone="warning"><div className="space-y-2"><p>This return may already have posted. Do not submit another.</p><p className="text-xs">Operation {pending.operationId}</p><div className="flex gap-2"><Button variant="secondary" onClick={() => void checkPending(pending)}>Check result</Button><Button variant="ghost" onClick={() => { if (user && window.confirm("Clear only after confirming it did not commit.")) { clearPendingLegacyReturn(user.id, pending.operationId); setPending(null); setReview(null); setOperationId(crypto.randomUUID()); } }}>Clear attempt</Button></div>{recovery && <p>{recovery}</p>}</div></Alert>}
    {error && <Alert>{error}</Alert>}
    {posted && <Alert tone="success">Sold-before-InventStock return recorded: {money(posted.totalUsd)}. Operation {posted.operationId}.</Alert>}

    <Card title="1 · Identify returned stock"><div className="space-y-4 p-5">
      <Field label="Part number or name" hint="A return must use a known SKU so stock and quarantine remain traceable."><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="2045012782 or ~/12582" autoFocus /></Field>
      {parts.isLoading && <Spinner label="Searching parts…" />}{parts.isError && <Alert>Could not load parts.</Alert>}
      <div className="max-h-64 divide-y divide-border overflow-auto rounded-lg border border-border">{parts.data?.items.map((part) => <button key={part.id} type="button" disabled={!part.isActive || cart.some((row) => row.part.id === part.id)} onClick={() => addPart(part)} className="flex w-full justify-between gap-3 p-3 text-left text-sm hover:bg-surface-muted disabled:opacity-50"><span><strong>{part.partNumber}</strong> · {part.brand || "Unknown brand"}<span className="block text-xs text-muted">{part.name} {part.variant && `· ${part.variant}`}</span></span><span className="shrink-0 text-xs">{part.stockConfidence === "NotCounted" ? "Not counted" : formatStockQuantity(part.quantityOnHand, part.stockUnit)}</span></button>)}</div>
      {cart.map((row) => <div key={row.key} className="space-y-3 rounded-lg border border-border p-4"><div className="flex justify-between gap-2"><div><strong>{row.part.partNumber}</strong> · {row.part.brand || "Unknown brand"}<p className="text-xs text-muted">{row.part.name}</p></div><Button variant="ghost" onClick={() => { setCart((rows) => rows.filter((x) => x.key !== row.key)); setReview(null); }}>Remove</Button></div><div className="grid gap-3 sm:grid-cols-3"><Field label={row.part.stockUnit === "Centimeter" ? "Returned length · metres" : "Returned quantity · pieces"}><Input type="number" min={row.part.stockUnit === "Centimeter" ? "0.01" : "1"} step={row.part.stockUnit === "Centimeter" ? "0.01" : "1"} value={row.quantity} onChange={(event) => patchLine(row.key, { quantity: event.target.value })} /></Field><Field label="Manual return value · USD"><Input inputMode="decimal" value={row.refund} onChange={(event) => patchLine(row.key, { refund: event.target.value })} /></Field><Field label="Stock condition"><Select value={row.condition} onChange={(event) => patchLine(row.key, { condition: event.target.value as ReturnCondition, separated: false })}><option value="Sellable">Sellable</option><option value="Quarantined">Faulty · quarantine</option></Select></Field></div>{row.part.stockUnit === "Centimeter" && <p className="text-xs text-muted">Enter metres to 0.01m; stock records whole centimetres.</p>}{row.condition === "Quarantined" && <div className="space-y-2 rounded-lg bg-warning-surface p-3"><Field label="Fault / issue"><Input maxLength={500} value={row.issueReason} onChange={(event) => patchLine(row.key, { issueReason: event.target.value })} /></Field><label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={row.separated} onChange={(event) => patchLine(row.key, { separated: event.target.checked })} /> Physically separated or tagged</label></div>}</div>)}
      <p className="text-right font-semibold">Entered return value: {total === null ? "—" : formatCents(total)}</p>
    </div></Card>

    <Card title="2 · Old-sale evidence"><div className="grid gap-4 p-5 md:grid-cols-2"><Field label="Original sale date · optional"><Input type="date" max={new Date().toISOString().slice(0, 10)} value={originalDate} onChange={(event) => { setOriginalDate(event.target.value); setReview(null); }} /></Field><Field label="Old receipt / book reference · optional"><Input maxLength={200} value={originalReference} onChange={(event) => { setOriginalReference(event.target.value); setReview(null); }} placeholder="Receipt 104 or book page 41" /></Field><div className="md:col-span-2"><Field label="Why is this manual return allowed?"><Textarea maxLength={500} value={reason} onChange={(event) => { setReason(event.target.value); setReview(null); }} placeholder="Original sale predates InventStock; wrong part returned…" /></Field></div></div></Card>

    <Card title="3 · Customer and settlement"><div className="space-y-4 p-5">
      {customer ? <div className="rounded-lg border border-border p-3"><strong>{customer.customer.name}</strong><p className="text-xs text-muted">{customer.customer.distinguishingLabel ?? customer.customer.phone ?? customer.customer.id.slice(0, 8)} · balance {customer.balanceUsd.toFixed(2)} USD</p><Button variant="ghost" onClick={() => { setCustomer(null); setAccountReduction("0"); setReview(null); }}>Remove customer</Button></div> : <><Field label="Customer · optional" hint="Required only when reducing debt or keeping customer credit."><Input value={customerSearch} onChange={(event) => setCustomerSearch(event.target.value)} placeholder="Search name, label or phone" /></Field>{customers.data?.items.map((item) => <button key={item.customer.id} type="button" onClick={() => { setCustomer(item); setCustomerSearch(""); setReview(null); }} className="block w-full rounded-lg border border-border p-2 text-left text-sm hover:bg-surface-muted">{item.customer.name} · {item.customer.distinguishingLabel ?? item.customer.phone ?? item.customer.id.slice(0, 8)}</button>)}</>}
      {customer && <Field label="Reduce customer account · USD"><Input inputMode="decimal" value={accountReduction} onChange={(event) => { setAccountReduction(event.target.value); setReview(null); }} /></Field>}
      {refunds.map((row) => <TenderEditor key={row.key} row={row} accounts={paymentAccounts} title="Refund paid to customer" onPatch={(patch) => { setRefunds((items) => items.map((item) => item.key === row.key ? { ...item, ...patch } : item)); setReview(null); }} onRemove={() => { setRefunds((items) => items.filter((item) => item.key !== row.key)); setReview(null); }} />)}
      <Button variant="secondary" onClick={() => { setRefunds((items) => [...items, newTender(preferredTenderAccount(paymentAccounts))]); setReview(null); }}>Add cash / Whish refund</Button>
      <p className="text-xs text-muted">Whish refunds are digital. LBP/EUR preserve the native amount and the USD value agreed with the customer.</p>
    </div></Card>

    <Card title="4 · Review and post"><div className="space-y-3 p-5"><Button disabled={Boolean(pending) || post.isPending || !cart.length} onClick={prepare}>Review manual return</Button>{review && <div className="space-y-2 rounded-lg border border-accent p-3 text-sm"><p><strong>{money(review.confirmedTotalUsd)}</strong> · {review.lines.length} SKU(s) · {customer?.customer.name ?? "Anonymous"}</p><p>Refund {money(review.refunds.reduce((sum, item) => sum + item.confirmedUsdAmount, 0))}; account reduction {money(review.accountReductionUsd)}</p><p>Reason: {review.reason}</p><Button disabled={Boolean(pending) || post.isPending} onClick={() => void submit()}>{post.isPending ? "Posting…" : "Confirm sold-before-InventStock return"}</Button></div>}</div></Card>
  </div>;
}
