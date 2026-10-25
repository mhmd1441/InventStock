"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useParts, usePostSaleExchange, useReturnableSale, useSalePaymentAccounts } from "@/lib/queries";
import { centsNumber, formatCents, lineCents, settlementCents, usdCents } from "@/lib/sale-math";
import { additionalReturnCents } from "@/lib/return-math";
import { formatStockQuantity, salePricePerBaseUnit, saleQuantityInBaseUnits } from "@/lib/quantity";
import { parseTenderDrafts, preferredTenderAccount, type TenderDraft } from "@/lib/tender-math";
import { clearPendingSaleExchange, readPendingSaleExchange, rememberPendingSaleExchange, type PendingSaleExchangeOperation } from "@/lib/pending-sale-exchange-operation";
import { TenderEditor, newTender } from "@/components/tender-editor";
import { Alert, Button, Card, Field, Input, Select, Spinner } from "@/components/ui";
import type { Part, PostSaleExchangeRequest, ReturnCondition, SaleExchange } from "@/lib/types";

type ReturnDraft = { quantity: string; condition: ReturnCondition; separated: boolean; issueReason: string };
type ReplacementDraft = { key: string; part: Part; quantity: string; price: string; confirmed: boolean; reason: string };
const blankReturn = (): ReturnDraft => ({ quantity: "0", condition: "Sellable", separated: false, issueReason: "" });
const errorText = (value: unknown) => value instanceof ApiError ? [value.message, ...value.fieldMessages].join(" ") : "The response may have reached the server. Check its operation status before trying again.";

export default function ExchangePage() {
  const { user } = useAuth();
  const [saleInput, setSaleInput] = useState("");
  const [saleId, setSaleId] = useState<string | null>(null);
  const [returns, setReturns] = useState<Record<string, ReturnDraft>>({});
  const [search, setSearch] = useState("");
  const [replacement, setReplacement] = useState<ReplacementDraft[]>([]);
  const [reason, setReason] = useState("");
  const [applied, setApplied] = useState("0");
  const [accountReduction, setAccountReduction] = useState("0");
  const [refunds, setRefunds] = useState<TenderDraft[]>([]);
  const [payments, setPayments] = useState<TenderDraft[]>([newTender()]);
  const [change, setChange] = useState<TenderDraft[]>([]);
  const [credit, setCredit] = useState("0");
  const [debt, setDebt] = useState("0");
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const [review, setReview] = useState<PostSaleExchangeRequest | null>(null);
  const [posted, setPosted] = useState<SaleExchange | null>(null);
  const [pending, setPending] = useState<PendingSaleExchangeOperation | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const available = useReturnableSale(saleId);
  const parts = useParts({ search, page: 1, pageSize: 10 });
  const accounts = useSalePaymentAccounts();
  const post = usePostSaleExchange();
  const paymentAccounts = accounts.data ?? [];
  const preferred = preferredTenderAccount(paymentAccounts);

  useEffect(() => {
    const source = new URLSearchParams(window.location.search).get("sale");
    if (source && /^[0-9a-f-]{36}$/i.test(source)) queueMicrotask(() => {
      setSaleId(source); setSaleInput(source);
    });
  }, []);
  useEffect(() => {
    if (!preferred) return;
    queueMicrotask(() => setPayments((rows) => rows.map((row) => row.accountId || row.native || row.usd ? row : { ...row, accountId: preferred.id })));
  }, [preferred]);
  useEffect(() => {
    if (!user) return;
    const item = readPendingSaleExchange(user.id);
    queueMicrotask(() => setPending(item));
    if (item) void api.get<SaleExchange>(`/api/sale-exchanges/operations/${item.operationId}`).then((result) => {
      clearPendingSaleExchange(user.id, item.operationId); setPending(null); setPosted(result); setReview(null); setOperationId(crypto.randomUUID());
    }).catch((caught: unknown) => setMessage(caught instanceof ApiError && caught.status === 404
      ? "No committed exchange was found yet. Check again before replacing it." : "Could not establish exchange status. Keep this attempt pending."));
  }, [user]);

  const returnTotal = useMemo(() => {
    if (!available.data) return null;
    let total = BigInt(0);
    for (const remaining of available.data.lines) {
      const original = available.data.sale.lines.find((line) => line.id === remaining.saleLineId)!;
      const value = returns[remaining.saleLineId]?.quantity ?? "0";
      if (!value.trim() || Number(value) === 0) continue;
      const quantity = saleQuantityInBaseUnits(value, original.stockUnit);
      if (quantity === null) return null;
      const cents = additionalReturnCents(original.lineTotalUsd, original.quantity, remaining.returnedQuantity, remaining.refundedUsd, quantity);
      if (cents === null) return null;
      total += cents;
    }
    return total;
  }, [available.data, returns]);
  const replacementTotal = useMemo(() => replacement.reduce<bigint | null>((sum, row) => {
    if (sum === null) return null;
    const quantity = saleQuantityInBaseUnits(row.quantity, row.part.stockUnit);
    const price = salePricePerBaseUnit(row.price, row.part.stockUnit);
    if (quantity === null || price === null) return null;
    const cents = lineCents(price, quantity);
    return cents === null ? null : sum + cents;
  }, BigInt(0)), [replacement]);

  const resetReview = () => setReview(null);
  const selectSale = () => { if (/^[0-9a-f-]{36}$/i.test(saleInput)) { setSaleId(saleInput.trim()); setReturns({}); setReplacement([]); resetReview(); } };
  const patchReturn = (id: string, patch: Partial<ReturnDraft>) => { setReturns((all) => ({ ...all, [id]: { ...(all[id] ?? blankReturn()), ...patch } })); resetReview(); };
  const addReplacement = (part: Part) => { if (!replacement.some((row) => row.part.id === part.id)) setReplacement((rows) => [...rows, { key: crypto.randomUUID(), part, quantity: part.stockUnit === "Centimeter" ? "1.00" : "1", price: "", confirmed: false, reason: "" }]); setSearch(""); resetReview(); };
  const patchReplacement = (key: string, patch: Partial<ReplacementDraft>) => { setReplacement((rows) => rows.map((row) => row.key === key ? { ...row, ...patch } : row)); resetReview(); };
  const patchTender = (set: React.Dispatch<React.SetStateAction<TenderDraft[]>>, key: string, patch: Partial<TenderDraft>) => { set((rows) => rows.map((row) => row.key === key ? { ...row, ...patch } : row)); resetReview(); };

  const prepare = () => {
    setError(null); setPosted(null);
    try {
      if (!available.data || returnTotal === null || returnTotal <= BigInt(0)) throw new Error("Select at least one returnable item and a valid quantity.");
      if (!replacement.length || replacementTotal === null || replacementTotal <= BigInt(0)) throw new Error("Add a replacement item and a valid selling price.");
      if (!reason.trim() || reason.length > 500) throw new Error("Enter an exchange reason (maximum 500 characters).");
      const returnLines = available.data.lines.flatMap((remaining) => {
        const original = available.data!.sale.lines.find((line) => line.id === remaining.saleLineId)!;
        const row = returns[remaining.saleLineId] ?? blankReturn();
        if (!row.quantity.trim() || Number(row.quantity) === 0) return [];
        const quantity = saleQuantityInBaseUnits(row.quantity, original.stockUnit);
        if (quantity === null || quantity > remaining.remainingQuantity) throw new Error(`${original.partNumber} exceeds the returnable quantity.`);
        if (row.condition === "Quarantined" && (!row.separated || !row.issueReason.trim())) throw new Error("Physically separate faulty returns and record the issue.");
        return [{ saleLineId: remaining.saleLineId, quantity, condition: row.condition, physicalSeparationConfirmed: row.condition === "Quarantined" && row.separated, issueReason: row.condition === "Quarantined" ? row.issueReason.trim() : null }];
      });
      const replacementLines = replacement.map((row) => {
        const quantity = saleQuantityInBaseUnits(row.quantity, row.part.stockUnit);
        const price = salePricePerBaseUnit(row.price, row.part.stockUnit);
        if (quantity === null || price === null || Number(price) <= 0) throw new Error(`Enter a valid quantity and USD price for ${row.part.partNumber}.`);
        const shortage = row.part.stockConfidence === "NotCounted" || quantity > row.part.quantityOnHand;
        if (row.part.stockConfidence === "NeedsRecount" && quantity > row.part.quantityOnHand) throw new Error(`${row.part.partNumber} needs a physical recount before another shortage.`);
        if (quantity > row.part.quantityOnHand && row.part.quarantinedQuantity > 0) throw new Error(`${row.part.partNumber} has held goods that cannot satisfy a shortage.`);
        if (shortage && (!row.confirmed || !row.reason.trim())) throw new Error(`Physically confirm ${row.part.partNumber} and explain the stock exception.`);
        return { partId: row.part.id, quantity, unitSellingPriceUsd: Number(price), physicalGoodsConfirmed: row.confirmed, stockExceptionReason: row.confirmed ? row.reason.trim() : null };
      });
      const appliedCents = usdCents(applied), accountCents = usdCents(accountReduction), creditCents = usdCents(credit), debtCents = usdCents(debt);
      if ([appliedCents, accountCents, creditCents, debtCents].some((value) => value === null || value! < 0)) throw new Error("Exchange USD allocations must be nonnegative amounts with two decimals.");
      const refundsParsed = parseTenderDrafts(refunds, paymentAccounts);
      const paymentsParsed = parseTenderDrafts(payments, paymentAccounts);
      const changeParsed = parseTenderDrafts(change, paymentAccounts);
      const refundCents = refundsParsed.reduce((sum, row) => sum + usdCents(row.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      const receivedCents = paymentsParsed.reduce((sum, row) => sum + usdCents(row.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      const changeCents = changeParsed.reduce((sum, row) => sum + usdCents(row.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      if (returnTotal !== appliedCents! + accountCents! + refundCents) throw new Error(`Allocate all ${formatCents(returnTotal)} return value between replacement credit, refund and customer account reduction.`);
      if (settlementCents([appliedCents!, receivedCents], [changeCents], creditCents!, debtCents!) !== replacementTotal) throw new Error(`Replacement settlement must equal ${formatCents(replacementTotal)}.`);
      if (!available.data.sale.customerId && (accountCents! > 0 || creditCents! > 0 || debtCents! > 0)) throw new Error("Anonymous exchanges cannot change a customer account.");
      setReview({ operationId, originalSaleId: available.data.sale.id, reason: reason.trim(), confirmedReturnTotalUsd: centsNumber(returnTotal), returnAppliedUsd: centsNumber(appliedCents!), accountReductionUsd: centsNumber(accountCents!), returnLines, refunds: refundsParsed, confirmedReplacementTotalUsd: centsNumber(replacementTotal), customerCreditUsedUsd: centsNumber(creditCents!), unpaidDebtUsd: centsNumber(debtCents!), replacementLines, payments: paymentsParsed, change: changeParsed });
    } catch (caught) { setError(caught instanceof Error ? caught.message : "Cannot review this exchange."); }
  };
  const submit = async () => {
    if (!review || !user || pending || post.isPending) return;
    const journal = { operationId: review.operationId, actorId: user.id, createdAtUtc: new Date().toISOString() };
    rememberPendingSaleExchange(journal); setPending(journal); setError(null);
    try { const result = await post.mutateAsync(review); clearPendingSaleExchange(user.id, journal.operationId); setPending(null); setPosted(result); setReview(null); setOperationId(crypto.randomUUID()); }
    catch (caught) { setError(errorText(caught)); }
  };
  const checkPending = async () => { if (!pending) return; try { const result = await api.get<SaleExchange>(`/api/sale-exchanges/operations/${pending.operationId}`); if (user) clearPendingSaleExchange(user.id, pending.operationId); setPending(null); setPosted(result); setReview(null); setOperationId(crypto.randomUUID()); } catch { setMessage("No committed exchange was found yet. Keep the pending operation until confirmed."); } };

  return <div className="space-y-6">
    <div className="flex flex-wrap items-start justify-between gap-3"><div><h1 className="text-xl font-semibold">Exchange an item</h1><p className="text-sm text-muted">One atomic transaction: return the original item, add its replacement, then explicitly settle any difference.</p></div><Link href="/returns" className="rounded-lg border border-border px-3.5 py-2 text-sm hover:bg-surface-muted">Use linked return</Link></div>
    <Alert tone="warning">Confirm compatibility and the physical item first. Faulty returned goods must be separated before posting.</Alert>
    {error && <Alert>{error}</Alert>}{message && <Alert tone="warning">{message}</Alert>}{posted && <Alert tone="success">Exchange recorded: returned {formatCents(BigInt(Math.round(posted.returnTotalUsd * 100)))}; replacement sale #{posted.replacementSale.receiptNumber} is posted.</Alert>}
    {pending && <Alert tone="warning"><p>An exchange may already have posted. Do not submit a replacement until status is checked.</p><Button variant="secondary" onClick={() => void checkPending()}>Check exchange status</Button></Alert>}
    <Card title="1 · Original sale"><div className="space-y-3 p-5"><div className="flex gap-2"><Input aria-label="Original sale ID" value={saleInput} onChange={(e) => setSaleInput(e.target.value)} placeholder="Paste original sale ID" /><Button variant="secondary" onClick={selectSale} disabled={!/^[0-9a-f-]{36}$/i.test(saleInput)}>Load sale</Button></div>{available.isLoading && <Spinner label="Loading original sale…" />}{available.data?.lines.map((remaining) => { const original = available.data.sale.lines.find((line) => line.id === remaining.saleLineId)!; const row = returns[remaining.saleLineId] ?? blankReturn(); return <div key={remaining.saleLineId} className="space-y-2 rounded-lg border border-border p-3"><p><strong>{original.partNumber}</strong> · remaining {formatStockQuantity(remaining.remainingQuantity, original.stockUnit)} · max return {formatCents(BigInt(Math.round(remaining.remainingRefundUsd * 100)))}</p><Field label={original.stockUnit === "Centimeter" ? "Return length · metres" : "Return quantity · pieces"}><Input type="number" value={row.quantity} onChange={(e) => patchReturn(remaining.saleLineId, { quantity: e.target.value })} /></Field><Select value={row.condition} onChange={(e) => patchReturn(remaining.saleLineId, { condition: e.target.value as ReturnCondition, separated: false })}><option value="Sellable">Sellable return</option><option value="Quarantined">Faulty · quarantine</option></Select>{row.condition === "Quarantined" && <><Field label="Fault / issue"><Input value={row.issueReason} onChange={(e) => patchReturn(remaining.saleLineId, { issueReason: e.target.value })} /></Field><label className="flex gap-2 text-sm"><input type="checkbox" checked={row.separated} onChange={(e) => patchReturn(remaining.saleLineId, { separated: e.target.checked })} /> Physically separated or tagged</label></>}</div>; })}<p className="text-right font-semibold">Return value: {returnTotal === null ? "—" : formatCents(returnTotal)}</p></div></Card>
    {available.data && <><Card title="2 · Replacement"><div className="space-y-3 p-5"><Field label="Replacement part number or name"><Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search stock" /></Field><div className="max-h-48 divide-y divide-border overflow-auto rounded border border-border">{parts.data?.items.map((part) => <button key={part.id} type="button" onClick={() => addReplacement(part)} className="block w-full p-2 text-left text-sm hover:bg-surface-muted"><strong>{part.partNumber}</strong> · {part.name}</button>)}</div>{replacement.map((row) => <div key={row.key} className="space-y-2 rounded border border-border p-3"><div className="flex justify-between"><strong>{row.part.partNumber}</strong><Button variant="ghost" onClick={() => { setReplacement((all) => all.filter((x) => x.key !== row.key)); resetReview(); }}>Remove</Button></div><Field label={row.part.stockUnit === "Centimeter" ? "Length · metres" : "Quantity · pieces"}><Input value={row.quantity} onChange={(e) => patchReplacement(row.key, { quantity: e.target.value })} /></Field><Field label={row.part.stockUnit === "Centimeter" ? "Selling price · USD/metre" : "Selling price · USD/piece"}><Input value={row.price} onChange={(e) => patchReplacement(row.key, { price: e.target.value })} /></Field>{(row.part.stockConfidence === "NotCounted" || Number(row.quantity) > row.part.quantityOnHand) && <><label className="flex gap-2 text-sm"><input type="checkbox" checked={row.confirmed} onChange={(e) => patchReplacement(row.key, { confirmed: e.target.checked })} /> Physically confirmed</label><Field label="Stock exception reason"><Input value={row.reason} onChange={(e) => patchReplacement(row.key, { reason: e.target.value })} /></Field></>}</div>)}<p className="text-right font-semibold">Replacement total: {replacementTotal === null ? "—" : formatCents(replacementTotal)}</p></div></Card>
      <Card title="3 · Settle the difference"><div className="space-y-3 p-5"><Field label="Exchange credit applied to replacement · USD"><Input value={applied} onChange={(e) => { setApplied(e.target.value); resetReview(); }} /></Field>{available.data.sale.customerId && <><Field label="Reduce customer account · USD"><Input value={accountReduction} onChange={(e) => { setAccountReduction(e.target.value); resetReview(); }} /></Field><Field label="Use customer credit · USD"><Input value={credit} onChange={(e) => { setCredit(e.target.value); resetReview(); }} /></Field><Field label="Leave unpaid debt · USD"><Input value={debt} onChange={(e) => { setDebt(e.target.value); resetReview(); }} /></Field></>}{refunds.map((row) => <TenderEditor key={row.key} row={row} accounts={paymentAccounts} title="Return refund" onPatch={(patch) => patchTender(setRefunds, row.key, patch)} onRemove={() => setRefunds((all) => all.filter((x) => x.key !== row.key))} />)}<Button variant="secondary" onClick={() => setRefunds((all) => [...all, newTender(preferred)])}>Add refund</Button>{payments.map((row) => <TenderEditor key={row.key} row={row} accounts={paymentAccounts} title="Replacement payment" onPatch={(patch) => patchTender(setPayments, row.key, patch)} onRemove={() => setPayments((all) => all.filter((x) => x.key !== row.key))} />)}<Button variant="secondary" onClick={() => setPayments((all) => [...all, newTender(preferred)])}>Add payment</Button>{change.map((row) => <TenderEditor key={row.key} row={row} accounts={paymentAccounts} title="Change given" onPatch={(patch) => patchTender(setChange, row.key, patch)} onRemove={() => setChange((all) => all.filter((x) => x.key !== row.key))} />)}<Button variant="secondary" onClick={() => setChange((all) => [...all, newTender(preferred)])}>Add change</Button><Field label="Why is this exchange happening?"><Input value={reason} onChange={(e) => { setReason(e.target.value); resetReview(); }} placeholder="Wrong fitment, faulty item, customer selected another brand…" /></Field></div></Card>
      <Card title="4 · Review and post"><div className="space-y-3 p-5"><Button onClick={prepare} disabled={Boolean(pending) || post.isPending}>Review exchange</Button>{review && <div className="space-y-2 rounded border border-accent p-3 text-sm"><p>Return {formatCents(BigInt(Math.round(review.confirmedReturnTotalUsd * 100)))}; apply {formatCents(BigInt(Math.round(review.returnAppliedUsd * 100)))}; replacement {formatCents(BigInt(Math.round(review.confirmedReplacementTotalUsd * 100)))}.</p><Button onClick={() => void submit()} disabled={Boolean(pending) || post.isPending}>{post.isPending ? "Posting…" : "Confirm & post exchange"}</Button></div>}</div></Card></>}
  </div>;
}
