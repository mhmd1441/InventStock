"use client";

import { useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { usePostSaleReturn, useReturnableSale, useSalePaymentAccounts, useSaleSearch } from "@/lib/queries";
import { additionalReturnCents } from "@/lib/return-math";
import { centsNumber, formatCents, usdCents } from "@/lib/sale-math";
import { parseTenderDrafts, preferredTenderAccount, type TenderDraft } from "@/lib/tender-math";
import { clearPendingReturn, readPendingReturn, rememberPendingReturn, type PendingReturnOperation } from "@/lib/pending-return-operation";
import { formatStockQuantity, saleQuantityInBaseUnits } from "@/lib/quantity";
import { TenderEditor, newTender } from "@/components/tender-editor";
import { Alert, Button, Card, Field, Input, Select, Spinner } from "@/components/ui";
import type { PostSaleReturnRequest, ReturnCondition, SaleReturn, SaleSearchQuery } from "@/lib/types";

type LineDraft = { quantity: string; condition: ReturnCondition; separated: boolean; issueReason: string };
type SearchDraft = { from: string; to: string; customer: string; part: string; amount: string };
const emptyLine = (): LineDraft => ({ quantity: "0", condition: "Sellable", separated: false, issueReason: "" });
const emptySearch = (): SearchDraft => ({ from: "", to: "", customer: "", part: "", amount: "" });
const startUtc = (day: string) => day ? new Date(`${day}T00:00:00`).toISOString() : undefined;
const endUtc = (day: string) => {
  if (!day) return undefined;
  const end = new Date(`${day}T00:00:00`); end.setDate(end.getDate() + 1); return end.toISOString();
};
const dollars = (value: number) => formatCents(BigInt(Math.round(value * 100)));
const describeError = (error: unknown) => error instanceof ApiError
  ? [error.message, ...error.fieldMessages].join(" ")
  : "The response was lost. Check this operation before attempting another return.";

export default function ReturnsPage() {
  const { user } = useAuth();
  const [saleInput, setSaleInput] = useState("");
  const [saleId, setSaleId] = useState<string | null>(null);
  const [lines, setLines] = useState<Record<string, LineDraft>>({});
  const [reason, setReason] = useState("");
  const [accountReduction, setAccountReduction] = useState("0");
  const [refunds, setRefunds] = useState<TenderDraft[]>([]);
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const [review, setReview] = useState<PostSaleReturnRequest | null>(null);
  const [posted, setPosted] = useState<SaleReturn | null>(null);
  const [pending, setPending] = useState<PendingReturnOperation | null>(null);
  const [recoveryMessage, setRecoveryMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [searchDraft, setSearchDraft] = useState<SearchDraft>(emptySearch);
  const [searchQuery, setSearchQuery] = useState<SaleSearchQuery>({ page: 1, pageSize: 30 });
  const search = useSaleSearch(searchQuery);
  const availability = useReturnableSale(saleId);
  const accounts = useSalePaymentAccounts();
  const post = usePostSaleReturn();
  const paymentAccounts = accounts.data ?? [];

  useEffect(() => {
    const fromLink = new URLSearchParams(window.location.search).get("sale");
    if (fromLink && /^[0-9a-f-]{36}$/i.test(fromLink)) queueMicrotask(() => { setSaleId(fromLink); setSaleInput(fromLink); });
  }, []);
  useEffect(() => {
    if (!user) return;
    let active = true;
    const item = readPendingReturn(user.id);
    queueMicrotask(() => { if (active) setPending(item); });
    if (item) void api.get<SaleReturn>(`/api/sale-returns/operations/${item.operationId}`).then((result) => {
      if (!active) return;
      clearPendingReturn(user.id, item.operationId);
      setPending(null); setPosted(result); setReview(null); setOperationId(crypto.randomUUID());
      setSaleId(result.saleId); setSaleInput(result.saleId);
    }).catch((caught: unknown) => {
      if (!active) return;
      setRecoveryMessage(caught instanceof ApiError && caught.status === 404
        ? "No committed return found yet. Confirm this before clearing the attempt."
        : "Could not establish return status; keep this attempt pending.");
    });
    return () => { active = false; };
  }, [user]);

  const total = useMemo(() => {
    if (!availability.data) return null;
    let sum = BigInt(0);
    for (const remaining of availability.data.lines) {
      const original = availability.data.sale.lines.find((line) => line.id === remaining.saleLineId)!;
      const input = lines[remaining.saleLineId]?.quantity ?? "0";
      if (!input.trim() || Number(input) === 0) continue;
      const quantity = saleQuantityInBaseUnits(input, original.stockUnit);
      if (quantity === null) return null;
      const cents = additionalReturnCents(original.lineTotalUsd, original.quantity,
        remaining.returnedQuantity, remaining.refundedUsd, quantity);
      if (cents === null) return null;
      sum += cents;
    }
    return sum;
  }, [availability.data, lines]);

  const selectSale = (id: string) => {
    setSaleId(id); setSaleInput(id); setLines({}); setReason(""); setAccountReduction("0");
    setRefunds([]); setReview(null); setPosted(null); setError(null);
  };
  const patchLine = (id: string, patch: Partial<LineDraft>) => {
    setLines((current) => ({ ...current, [id]: { ...(current[id] ?? emptyLine()), ...patch } }));
    setReview(null);
  };
  const checkPending = async (item: PendingReturnOperation) => {
    setRecoveryMessage(null);
    try {
      const result = await api.get<SaleReturn>(`/api/sale-returns/operations/${item.operationId}`);
      if (user) clearPendingReturn(user.id, item.operationId);
      setPending(null); setPosted(result); setReview(null); setOperationId(crypto.randomUUID());
      selectSale(result.saleId); setPosted(result);
    } catch (caught) {
      setRecoveryMessage(caught instanceof ApiError && caught.status === 404
        ? "No committed return found. If you are sure it did not arrive late, clear the attempt before trying again."
        : "Status could not be checked. Do not submit a replacement return yet.");
    }
  };
  const prepare = () => {
    setError(null); setPosted(null);
    try {
      const available = availability.data;
      if (!available) throw new Error("Select a posted sale and wait for its returnable lines.");
      if (!reason.trim() || reason.length > 500) throw new Error("Enter a return reason (maximum 500 characters).");
      const selected = available.lines.flatMap((remaining) => {
        const draft = lines[remaining.saleLineId] ?? emptyLine();
        const original = available.sale.lines.find((line) => line.id === remaining.saleLineId)!;
        if (!draft.quantity.trim() || Number(draft.quantity) === 0) return [];
        const quantity = saleQuantityInBaseUnits(draft.quantity, original.stockUnit);
        if (quantity === null || quantity > remaining.remainingQuantity)
          throw new Error(original.stockUnit === "Centimeter"
            ? "Return length must be in metres to at most two decimals and no greater than the remaining sold length."
            : "Return quantity must be whole pieces no greater than the remaining sold quantity.");
        if (draft.condition === "Quarantined" && (!draft.separated || !draft.issueReason.trim()))
          throw new Error("Physically separate or tag faulty goods and enter their issue before posting.");
        if (draft.issueReason.length > 500) throw new Error("Issue reason is limited to 500 characters.");
        return [{ saleLineId: remaining.saleLineId, quantity, condition: draft.condition,
          physicalSeparationConfirmed: draft.condition === "Quarantined" && draft.separated,
          issueReason: draft.condition === "Quarantined" ? draft.issueReason.trim() : null }];
      });
      if (!selected.length || total === null || total <= BigInt(0)) throw new Error("Select at least one returnable item.");
      const accountCents = usdCents(accountReduction);
      if (accountCents === null || accountCents < BigInt(0)) throw new Error("Account reduction must be a nonnegative USD amount.");
      if (!available.sale.customerId && accountCents > BigInt(0)) throw new Error("Anonymous sales cannot receive an account reduction.");
      const parsed = parseTenderDrafts(refunds, paymentAccounts);
      const refundedCents = parsed.reduce((sum, row) => sum + usdCents(row.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      if (total !== accountCents + refundedCents) throw new Error(`Allocate the full ${formatCents(total)} between explicit refund tenders and customer-account reduction.`);
      setReview({ operationId, saleId: available.sale.id, reason: reason.trim(), confirmedTotalUsd: centsNumber(total),
        accountReductionUsd: centsNumber(accountCents), lines: selected, refunds: parsed });
    } catch (caught) { setError(caught instanceof Error ? caught.message : "Cannot prepare return."); }
  };
  const submit = async () => {
    if (!review || !user || pending || post.isPending) return;
    const journal = { operationId: review.operationId, actorId: user.id, createdAtUtc: new Date().toISOString() };
    rememberPendingReturn(journal); setPending(journal); setError(null);
    try {
      const result = await post.mutateAsync(review);
      clearPendingReturn(user.id, journal.operationId);
      setPending(null); setPosted(result); setReview(null); setLines({}); setRefunds([]);
      setAccountReduction("0"); setReason(""); setOperationId(crypto.randomUUID());
    } catch (caught) { setError(describeError(caught)); }
  };

  return <div className="space-y-6">
    <div><h1 className="text-xl font-semibold">Linked return</h1><p className="text-sm text-muted">Find the original posted sale. Paper-era returns and exchanges are not available in this screen yet.</p></div>
    {pending && <Alert tone="warning"><div className="space-y-2"><p>A return may already have posted. Do not create another one until you check its operation status.</p><p className="text-xs">Operation {pending.operationId}</p><div className="flex gap-2"><Button variant="secondary" onClick={() => void checkPending(pending)}>Check result</Button><Button variant="ghost" onClick={() => { if (user && window.confirm("Clear only after confirming the return did not commit. A late commit may still appear.")) { clearPendingReturn(user.id, pending.operationId); setPending(null); setReview(null); setOperationId(crypto.randomUUID()); } }}>Clear attempt</Button></div>{recoveryMessage && <p>{recoveryMessage}</p>}</div></Alert>}
    {error && <Alert>{error}</Alert>}
    {posted && <Alert tone="success">Return recorded: {dollars(posted.totalUsd)}. Operation {posted.operationId}. Refresh the original sale before another return.</Alert>}
    <Card title="1 · Find original sale"><div className="space-y-4 p-5">
      <div className="flex gap-2"><Input aria-label="Sale ID" placeholder="Paste exact sale ID" value={saleInput} onChange={(event) => setSaleInput(event.target.value)} /><Button variant="secondary" disabled={!/^[0-9a-f-]{36}$/i.test(saleInput)} onClick={() => selectSale(saleInput.trim())}>Find ID</Button></div>
      <div className="grid gap-3 md:grid-cols-5">
        <Field label="From date"><Input type="date" value={searchDraft.from} onChange={(event) => setSearchDraft((x) => ({ ...x, from: event.target.value }))} /></Field>
        <Field label="To date"><Input type="date" value={searchDraft.to} onChange={(event) => setSearchDraft((x) => ({ ...x, to: event.target.value }))} /></Field>
        <Field label="Customer"><Input placeholder="Name, label or phone" value={searchDraft.customer} onChange={(event) => setSearchDraft((x) => ({ ...x, customer: event.target.value }))} /></Field>
        <Field label="Part"><Input placeholder="Number or name" value={searchDraft.part} onChange={(event) => setSearchDraft((x) => ({ ...x, part: event.target.value }))} /></Field>
        <Field label="Sale total · USD"><Input inputMode="decimal" placeholder="Exact amount" value={searchDraft.amount} onChange={(event) => setSearchDraft((x) => ({ ...x, amount: event.target.value }))} /></Field>
      </div>
      <div className="flex gap-2"><Button variant="secondary" onClick={() => setSearchQuery({ fromUtc: startUtc(searchDraft.from), toUtc: endUtc(searchDraft.to), customer: searchDraft.customer.trim() || undefined, part: searchDraft.part.trim() || undefined, amountUsd: searchDraft.amount.trim() || undefined, page: 1, pageSize: 30 })}>Search sales</Button><Button variant="ghost" onClick={() => { setSearchDraft(emptySearch()); setSearchQuery({ page: 1, pageSize: 30 }); }}>Clear</Button></div>
      <p className="text-xs text-muted">Searches are paged and newest-first. Choose a result to inspect its remaining returnable quantities.</p>
      {search.isLoading && <Spinner label="Searching sales…" />}{search.isError && <Alert>Sales could not be searched. Check the filters and pending database migrations.</Alert>}
      <div className="max-h-72 divide-y divide-border overflow-auto rounded-lg border border-border">{search.data?.items.map((sale) => <button key={sale.id} type="button" className="flex w-full justify-between gap-3 p-3 text-left text-sm hover:bg-surface-muted" onClick={() => selectSale(sale.id)}><span>{new Date(sale.postedAtUtc).toLocaleString()} · {sale.customerName ?? "Anonymous"}<span className="block text-xs text-muted">{sale.lines.map((line) => `${line.partNumber} · ${line.name}`).join(" | ")} · {sale.id.slice(0, 8)}</span></span><strong className="shrink-0">{dollars(sale.totalUsd)}</strong></button>)}</div>
      {search.data && <div className="flex items-center justify-between text-xs text-muted"><span>{search.data.totalCount} sale(s) · page {search.data.page} of {Math.max(1, search.data.totalPages)}</span><div className="flex gap-2"><Button variant="ghost" disabled={!search.data.hasPreviousPage} onClick={() => setSearchQuery((x) => ({ ...x, page: x.page - 1 }))}>Previous</Button><Button variant="ghost" disabled={!search.data.hasNextPage} onClick={() => setSearchQuery((x) => ({ ...x, page: x.page + 1 }))}>Next</Button></div></div>}
    </div></Card>
    {availability.isLoading && <Spinner label="Loading returnable sale…" />}
    {availability.isError && <Alert>Sale not found or returnable details unavailable. Check the sale ID and database migration state.</Alert>}
    {availability.data && <><Card title={`2 · Items from ${availability.data.sale.customerName ?? "anonymous sale"}`}><div className="space-y-4 p-5">{availability.data.lines.map((remaining) => { const original = availability.data.sale.lines.find((line) => line.id === remaining.saleLineId)!; const draft = lines[remaining.saleLineId] ?? emptyLine(); return <div key={remaining.saleLineId} className="space-y-3 rounded-lg border border-border p-4"><div><strong>{original.partNumber}</strong> · {original.brand || "Unknown brand"}<p className="text-xs text-muted">{original.name} {original.variant && `· ${original.variant}`}</p><p className="text-xs text-muted">Sold {formatStockQuantity(original.quantity, original.stockUnit)}, already returned {formatStockQuantity(remaining.returnedQuantity, original.stockUnit)}, remaining {formatStockQuantity(remaining.remainingQuantity, original.stockUnit)} · max refund {dollars(remaining.remainingRefundUsd)}</p></div>{remaining.remainingQuantity > 0 ? <><div className="grid gap-3 sm:grid-cols-2"><Field label={original.stockUnit === "Centimeter" ? "Return length · metres" : "Return quantity · pieces"}><Input type="number" min={original.stockUnit === "Centimeter" ? "0.01" : "0"} step={original.stockUnit === "Centimeter" ? "0.01" : "1"} max={original.stockUnit === "Centimeter" ? remaining.remainingQuantity / 100 : remaining.remainingQuantity} value={draft.quantity} onChange={(event) => patchLine(remaining.saleLineId, { quantity: event.target.value })} /></Field><Field label="Returned stock condition"><Select value={draft.condition} onChange={(event) => patchLine(remaining.saleLineId, { condition: event.target.value as ReturnCondition, separated: false })}><option value="Sellable">Sellable</option><option value="Quarantined">Faulty · quarantine</option></Select></Field></div>{original.stockUnit === "Centimeter" && <p className="text-xs text-muted">Enter metres to two decimals; the stock ledger records whole centimeters.</p>}{draft.condition === "Quarantined" && <div className="space-y-2 rounded-lg bg-warning-surface p-3 text-sm"><Field label="Issue with returned goods"><Input maxLength={500} value={draft.issueReason} onChange={(event) => patchLine(remaining.saleLineId, { issueReason: event.target.value })} /></Field><label className="flex items-center gap-2"><input type="checkbox" checked={draft.separated} onChange={(event) => patchLine(remaining.saleLineId, { separated: event.target.checked })} /> Physically separated or tagged</label></div>}</> : <p className="text-sm text-muted">Fully returned</p>}</div>; })}<p className="text-right font-semibold">Return value: {total === null ? "Check quantities" : formatCents(total)}</p></div></Card>
      <Card title="3 · Reason and refund"><div className="space-y-4 p-5"><Field label="Why is this being returned?"><Input maxLength={500} value={reason} onChange={(event) => { setReason(event.target.value); setReview(null); }} placeholder="Wrong part, defective, customer changed mind…" /></Field><p className="text-sm text-muted">The owner permits cash, Whish, account reduction, or a split. Debt-first is not forced. A later exchange will be a separate linked workflow.</p>{availability.data.sale.customerId && <Field label="Reduce customer account · USD" hint="Choose this only if the customer agrees to reduce debt or keep credit instead of receiving money."><Input inputMode="decimal" value={accountReduction} onChange={(event) => { setAccountReduction(event.target.value); setReview(null); }} /></Field>}{refunds.map((row) => <TenderEditor key={row.key} row={row} accounts={paymentAccounts} title="Refund paid to customer" onPatch={(patch) => { setRefunds((items) => items.map((item) => item.key === row.key ? { ...item, ...patch } : item)); setReview(null); }} onRemove={() => { setRefunds((items) => items.filter((item) => item.key !== row.key)); setReview(null); }} />)}<Button variant="secondary" onClick={() => { setRefunds((items) => [...items, newTender(preferredTenderAccount(paymentAccounts))]); setReview(null); }}>Add cash / Whish refund</Button><p className="text-xs text-muted">For LBP/EUR, enter both the native amount handed back and the explicitly agreed USD settlement. Whish refunds remain digital.</p></div></Card>
      <Card title="4 · Review and post"><div className="space-y-3 p-5"><Button disabled={Boolean(pending) || post.isPending} onClick={prepare}>Review return</Button>{review && <div className="space-y-2 rounded-lg border border-accent p-3 text-sm"><p><strong>{dollars(review.confirmedTotalUsd)}</strong> · {review.lines.length} line(s) from sale {review.saleId.slice(0, 8)}</p><p>Refund paid {dollars(review.refunds.reduce((sum, item) => sum + item.confirmedUsdAmount, 0))}; account reduction {dollars(review.accountReductionUsd)}</p><p>Reason: {review.reason}</p><Button disabled={Boolean(pending) || post.isPending} onClick={() => void submit()}>{post.isPending ? "Posting…" : "Confirm & post return"}</Button></div>}</div></Card></>}
  </div>;
}
