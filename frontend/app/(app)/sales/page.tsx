"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useAdminPriceCalculations, useCustomers, useCustomerStatement, useParts, usePostSale, useSalePaymentAccounts, useSellingPriceSuggestions } from "@/lib/queries";
import { centsNumber, formatCents, lineCents, settlementCents, usdCents } from "@/lib/sale-math";
import { clearPendingSale, readPendingSale, rememberPendingSale, type PendingSaleOperation } from "@/lib/pending-sale-operation";
import { formatStockQuantity, salePricePerBaseUnit, saleQuantityInBaseUnits } from "@/lib/quantity";
import { parseTenderDrafts, preferredTenderAccount, type TenderDraft } from "@/lib/tender-math";
import { TenderEditor, newTender } from "@/components/tender-editor";
import type { Part, PostSaleRequest, Sale, SaleOperation, SaleTender, SaleTenderAccountCorrection } from "@/lib/types";
import { Alert, Button, Card, Field, Input, Spinner } from "@/components/ui";

type CartLine = { key: string; part: Part; quantity: string; price: string; confirmed: boolean; reason: string };
const errorText = (error: unknown) => error instanceof ApiError ? [error.message, ...error.fieldMessages].join(" ") : "The request may have reached the server. Check its operation status before trying again.";

export default function SalesPage() {
  const { user, canViewPrivate } = useAuth();
  const [search, setSearch] = useState("");
  const [customerSearch, setCustomerSearch] = useState("");
  const [customerId, setCustomerId] = useState<string | null>(null);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [payments, setPayments] = useState<TenderDraft[]>(() => [newTender()]);
  const [change, setChange] = useState<TenderDraft[]>([]);
  const [credit, setCredit] = useState("0");
  const [debt, setDebt] = useState("0");
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const [review, setReview] = useState<PostSaleRequest | null>(null);
  const [posted, setPosted] = useState<Sale | null>(null);
  const [pending, setPending] = useState<PendingSaleOperation | null>(null);
  const [recoveryMessage, setRecoveryMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [correctingTender, setCorrectingTender] = useState<SaleTender | null>(null);
  const [correctAccountId, setCorrectAccountId] = useState("");
  const [correctionReason, setCorrectionReason] = useState("");
  const [correction, setCorrection] = useState<SaleTenderAccountCorrection | null>(null);
  const [correctionError, setCorrectionError] = useState<string | null>(null);
  const [correcting, setCorrecting] = useState(false);
  const [paperRecovery, setPaperRecovery] = useState(false);
  const [paperOccurredAt, setPaperOccurredAt] = useState(() => new Date().toISOString().slice(0, 16));
  const [paperReference, setPaperReference] = useState("");
  const [paperReason, setPaperReason] = useState("");
  const [paperSellerNote, setPaperSellerNote] = useState("");
  const [rememberedTenderAccountId, setRememberedTenderAccountId] = useState<string | null>(null);
  const parts = useParts({ search, page: 1, pageSize: 10 });
  const customers = useCustomers(customerSearch);
  const statement = useCustomerStatement(customerId);
  const accounts = useSalePaymentAccounts();
  const post = usePostSale();
  const cartPartIds = cart.map((line) => line.part.id);
  const suggestions = useSellingPriceSuggestions(cartPartIds);
  const privateCalculations = useAdminPriceCalculations(cartPartIds);
  useEffect(() => {
    if (!suggestions.data) return;
    const prices = new Map(suggestions.data.map((item) => [item.partId, item.suggestedUsd]));
    queueMicrotask(() => setCart((rows) => rows.map((line) => {
      const suggested = prices.get(line.part.id);
      return line.price || suggested === null || suggested === undefined ? line : {
        ...line, price: line.part.stockUnit === "Centimeter" ? (suggested * 100).toFixed(2) : String(suggested),
      };
    })));
  }, [suggestions.data]);
  const selectedCustomer = statement.data?.summary;

  useEffect(() => {
    if (user) queueMicrotask(() => setRememberedTenderAccountId(localStorage.getItem(`inventstock:last-sale-tender:${user.id}`)));
  }, [user]);

  useEffect(() => {
    if (!user) return;
    let active = true;
    const item = readPendingSale(user.id);
    queueMicrotask(() => { if (active) setPending(item); });
    if (item) void api.get<SaleOperation>(`/api/sales/operations/${item.operationId}`).then((result) => {
      if (!active) return;
      clearPendingSale(user.id, item.operationId);
      setPending(null); setPosted(result.sale); setCart([]); setReview(null); setOperationId(crypto.randomUUID());
    }).catch((caught: unknown) => {
      if (!active) return;
      setRecoveryMessage(caught instanceof ApiError && caught.status === 404
        ? "No committed sale was found yet. Check again before a replacement."
        : "Could not establish the sale's status. Keep this attempt pending.");
    });
    return () => { active = false; };
  }, [user]);
  const totalCents = useMemo(() => cart.reduce<bigint | null>((sum, line) => {
    if (sum === null) return null;
    const quantity = saleQuantityInBaseUnits(line.quantity, line.part.stockUnit);
    const price = salePricePerBaseUnit(line.price, line.part.stockUnit);
    const amount = quantity === null || price === null ? null : lineCents(price, quantity);
    return amount === null ? null : sum + amount;
  }, BigInt(0)), [cart]);

  const patchLine = (key: string, patch: Partial<CartLine>) => {
    setCart((rows) => rows.map((line) => line.key === key ? { ...line, ...patch } : line));
    setReview(null);
  };
  const addPart = (part: Part) => {
    if (cart.some((line) => line.part.id === part.id)) return;
    setCart((rows) => [...rows, { key: crypto.randomUUID(), part, quantity: "1", price: "", confirmed: false, reason: "" }]);
    setSearch(""); setReview(null); setPosted(null);
  };
  const patchTender = (set: React.Dispatch<React.SetStateAction<TenderDraft[]>>, key: string, patch: Partial<TenderDraft>) => {
    set((rows) => rows.map((row) => row.key === key ? { ...row, ...patch } : row));
    setReview(null);
  };
  const paymentAccounts = accounts.data?.filter((x) => x.currency === "USD" || x.currency === "LBP" || x.currency === "EUR") ?? [];
  const preferredAccount = paymentAccounts.find((account) => account.id === rememberedTenderAccountId)
    ?? preferredTenderAccount(paymentAccounts);
  useEffect(() => {
    if (!preferredAccount) return;
    queueMicrotask(() => setPayments((rows) => rows.map((row) =>
      row.accountId || row.native.trim() || row.usd.trim() ? row : { ...row, accountId: preferredAccount.id })));
  }, [preferredAccount]);

  const prepareReview = () => {
    setError(null); setPosted(null);
    try {
      if (!cart.length || totalCents === null || totalCents <= BigInt(0)) throw new Error("Add a part and enter a valid USD price for every line.");
      const lines = cart.map((line) => {
        const quantity = saleQuantityInBaseUnits(line.quantity, line.part.stockUnit);
        const basePrice = salePricePerBaseUnit(line.price, line.part.stockUnit);
        const unit = basePrice === null ? NaN : Number(basePrice);
        if (quantity === null || !Number.isFinite(unit) || unit <= 0 || unit > 999999)
          throw new Error("Enter whole pieces or metres to two decimal places, with a positive USD selling price.");
        const needsConfirmation = line.part.stockConfidence === "NotCounted" || quantity > line.part.quantityOnHand;
        if (line.part.stockConfidence === "NeedsRecount" && quantity > line.part.quantityOnHand)
          throw new Error(`${line.part.partNumber} needs a physical recount before another shortage sale.`);
        if (quantity > line.part.quantityOnHand && line.part.quarantinedQuantity > 0)
          throw new Error(`${line.part.partNumber} has held goods. Count or receive verified separate goods first.`);
        if (needsConfirmation && (!line.confirmed || !line.reason.trim()))
          throw new Error(`Physically confirm ${line.part.partNumber} and give a reason; a supplier promise is not stock.`);
        if (line.reason.length > 500) throw new Error("Stock exception reasons are limited to 500 characters.");
        return { partId: line.part.id, quantity, unitSellingPriceUsd: unit,
          physicalGoodsConfirmed: line.confirmed, stockExceptionReason: line.confirmed ? line.reason.trim() : null };
      });
      const received = parseTenderDrafts(payments, paymentAccounts);
      const given = parseTenderDrafts(change, paymentAccounts);
      const creditCents = usdCents(credit);
      const debtCents = usdCents(debt);
      if (creditCents === null || debtCents === null) throw new Error("Credit and debt must be USD amounts with at most two decimals.");
      if (!customerId && (creditCents > BigInt(0) || debtCents > BigInt(0))) throw new Error("Select a named customer for credit or unpaid debt.");
      if (customerId && (statement.isLoading || !selectedCustomer)) throw new Error("Wait for the customer's current account balance before posting.");
      const availableCredit = selectedCustomer ? Math.max(0, -selectedCustomer.balanceUsd) : 0;
      if (centsNumber(creditCents) > availableCredit) throw new Error("Requested customer credit exceeds the displayed balance. Refresh and review it.");
      const receivedCents = received.reduce((sum, x) => sum + usdCents(x.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      const givenCents = given.reduce((sum, x) => sum + usdCents(x.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      if (receivedCents < givenCents || settlementCents([receivedCents], [givenCents], creditCents, debtCents) !== totalCents)
        throw new Error(`Settlement does not equal the ${formatCents(totalCents)} total. Check tender, change, credit and debt.`);
      setReview({ operationId, customerId, confirmedTotalUsd: centsNumber(totalCents), customerCreditUsedUsd: centsNumber(creditCents),
        unpaidDebtUsd: centsNumber(debtCents), lines, payments: received, change: given });
    } catch (caught) { setError(caught instanceof Error ? caught.message : "Cannot prepare this sale."); }
  };

  const checkPending = async (item: PendingSaleOperation) => {
    setRecoveryMessage(null);
    try {
      const recovered = await api.get<SaleOperation>(`/api/sales/operations/${item.operationId}`);
      setPosted(recovered.sale);
      setReview(null);
      setCart([]);
      if (user) clearPendingSale(user.id, item.operationId);
      setPending(null);
      setOperationId(crypto.randomUUID());
    } catch (caught) {
      setRecoveryMessage(caught instanceof ApiError && caught.status === 404
        ? "No committed sale was found yet. Check again before starting a replacement sale."
        : "Could not establish the sale's status. Keep this operation pending and retry when connected.");
    }
  };
  const submit = async () => {
    if (!review || !user || pending || post.isPending) return;
    const journal = { operationId: review.operationId, actorId: user.id, createdAtUtc: new Date().toISOString() };
    rememberPendingSale(journal); setPending(journal); setError(null);
    try {
      if (paperRecovery && (!paperReference.trim() || !paperReason.trim() || Number.isNaN(new Date(paperOccurredAt).getTime())))
        throw new Error("Paper recovery needs the original time, a unique paper reference, and a reason.");
      const sale = paperRecovery
        ? await api.post<Sale>("/api/sales/paper-recovery", {
            sale: review, originalOccurredAtUtc: new Date(paperOccurredAt).toISOString(),
            paperReference: paperReference.trim(), reason: paperReason.trim(),
            originalSellerNote: paperSellerNote.trim() || null,
          })
        : await post.mutateAsync(review);
      clearPendingSale(user.id, journal.operationId); setPending(null);
      setPosted(sale); setReview(null); setCart([]); setChange([]); setCredit("0"); setDebt("0");
      setPayments([newTender(preferredAccount)]); setPaperRecovery(false); setPaperReference(""); setPaperReason(""); setPaperSellerNote("");
      setOperationId(crypto.randomUUID());
    } catch (caught) {
      if (caught instanceof ApiError) { clearPendingSale(user.id, journal.operationId); setPending(null); setOperationId(crypto.randomUUID()); setReview(null); }
      setError(errorText(caught));
    }
  };

  const beginTenderCorrection = (tender: SaleTender) => {
    setCorrectingTender(tender);
    setCorrectAccountId("");
    setCorrectionReason("");
    setCorrection(null);
    setCorrectionError(null);
  };

  const submitTenderCorrection = async () => {
    if (!correctingTender || !correctAccountId || !correctionReason.trim()) {
      setCorrectionError("Choose the correct account and enter a reason.");
      return;
    }
    setCorrecting(true); setCorrectionError(null);
    try {
      const result = await api.post<SaleTenderAccountCorrection>(
        `/api/sales/tenders/${correctingTender.id}/account-corrections`,
        { operationId: crypto.randomUUID(), correctCashAccountId: correctAccountId, reason: correctionReason.trim() },
      );
      setCorrection(result);
      setCorrectingTender(null);
    } catch (caught) {
      setCorrectionError(errorText(caught));
    } finally { setCorrecting(false); }
  };

  return <div className="space-y-6">
    <div><h1 className="text-xl font-semibold tracking-tight">Counter sale</h1><p className="text-sm text-muted">Search by any known number, choose the physical item, then settle the sale. This is not yet a receipt or a completed shop rollout.</p></div>
    {pending && <Alert tone="warning"><div className="space-y-2"><p>A sale has an uncertain result. Do not submit it again with a new ID until you check the server.</p><p className="text-xs">Operation {pending.operationId}</p><div className="flex gap-2"><Button variant="secondary" onClick={() => void checkPending(pending)}>Check result</Button><Button variant="ghost" onClick={() => { if (user && window.confirm("Only clear this after confirming it did not commit. A late commit could make a new sale a duplicate.")) { clearPendingSale(user.id, pending.operationId); setPending(null); setReview(null); setOperationId(crypto.randomUUID()); } }}>Clear unresolved attempt</Button></div>{recoveryMessage && <p>{recoveryMessage}</p>}</div></Alert>}
    {error && <Alert>{error}</Alert>}
    {posted && <><Alert tone="success">Sale #{posted.receiptNumber} recorded: {formatCents(BigInt(Math.round(posted.totalUsd * 100)))}. {posted.lines.some((line) => line.stockShortage) ? "Stock discrepancy: recount required." : "Stock and payment were posted together."} {posted.lines.some((line) => line.priceOverridden) && "Manual selling-price override recorded."} <Link href={`/sales/${posted.id}/receipt`} className="underline">Print / reprint internal receipt</Link> · <Link href={`/returns?sale=${posted.id}`} className="underline">Return an item from this sale</Link></Alert>
      {canViewPrivate && <Card title="Admin · correct a tender account"><div className="space-y-3 p-5 text-sm">
        <p className="text-muted">Use only when this posted sale was assigned to the wrong cash or Whish account. It records a new, dated correction; it never edits the sale, customer balance, change or a closed day.</p>
        {posted.tenders.filter((tender) => !tender.isChange).map((tender) => <div key={tender.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border p-3"><span>{tender.accountName} · {tender.nativeAmount} {tender.currency} (USD {tender.confirmedUsdAmount.toFixed(2)})</span><Button variant="secondary" onClick={() => beginTenderCorrection(tender)}>Correct account</Button></div>)}
        {correctingTender && <div className="space-y-3 rounded-lg border border-warning p-3"><p><strong>Original:</strong> {correctingTender.accountName} · {correctingTender.nativeAmount} {correctingTender.currency}. The amount and currency cannot change.</p>
          <Field label="Correct account"><select value={correctAccountId} onChange={(event) => setCorrectAccountId(event.target.value)} className="w-full rounded-md border border-border bg-surface px-3 py-2 text-sm"><option value="">Choose an account</option>{paymentAccounts.filter((account) => account.currency === correctingTender.currency && account.id !== correctingTender.cashAccountId).map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}</select></Field>
          <Field label="Reason"><Input maxLength={500} value={correctionReason} onChange={(event) => setCorrectionReason(event.target.value)} placeholder="For example: customer paid through Whish, not cash" /></Field>
          <div className="flex gap-2"><Button disabled={correcting} onClick={() => void submitTenderCorrection()}>{correcting ? "Posting…" : "Post account correction"}</Button><Button variant="ghost" onClick={() => { setCorrectingTender(null); setCorrectionError(null); }}>Cancel</Button></div>
        </div>}
        {correctionError && <Alert>{correctionError}</Alert>}
        {correction && <Alert tone="success">Correction recorded from the original account to {paymentAccounts.find((account) => account.id === correction.correctCashAccountId)?.name ?? "the selected account"}. It is in the exceptions history.</Alert>}
      </div></Card>}
    </>}

    <div className="grid gap-6 lg:grid-cols-[minmax(0,1.5fr)_minmax(300px,1fr)]">
      <div className="space-y-6">
        <Card title="1 · Find a part"><div className="space-y-3 p-5"><Field label="Part number or name" hint="Exact, partial, and suffix search are available. Press Enter to add the first result."><Input value={search} onChange={(e) => setSearch(e.target.value)} onKeyDown={(event) => { if (event.key === "Enter" && parts.data?.items[0]) { event.preventDefault(); addPart(parts.data.items[0]); } }} placeholder="2045012782 or ~/12582" autoFocus /></Field>
          {parts.isLoading && <Spinner label="Searching parts…" />}
          {parts.isError && <Alert>Could not load parts. The new database migrations may not be applied yet.</Alert>}
          <div className="max-h-72 divide-y divide-border overflow-auto rounded-lg border border-border">{parts.data?.items.map((part) => <button key={part.id} type="button" onClick={() => addPart(part)} disabled={!part.isActive || cart.some((line) => line.part.id === part.id)} className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-surface-muted disabled:opacity-50"><span><strong>{part.partNumber}</strong> · {part.brand || "Unknown brand"} {part.variant && `· ${part.variant}`}<span className="block text-xs text-muted">{part.name}</span></span><span className="shrink-0 text-right text-xs">{part.stockConfidence === "NotCounted" ? "Not counted" : formatStockQuantity(part.quantityOnHand, part.stockUnit)}{part.stockConfidence === "NeedsRecount" && <span className="block text-danger">Recount needed</span>}{part.quarantinedQuantity > 0 && <span className="block text-danger">{part.quarantinedQuantity} held</span>}</span></button>)}</div>
        </div></Card>
        <Card title={`2 · Cart (${cart.length})`}><div className="space-y-4 p-5">
          {cart.length === 0 && <p className="text-sm text-muted">Select a specific brand and variant above. Shared numbers do not prove two parts are interchangeable.</p>}
          {cart.map((line) => {
            const quantity = saleQuantityInBaseUnits(line.quantity, line.part.stockUnit);
            const basePrice = salePricePerBaseUnit(line.price, line.part.stockUnit);
            const needsConfirm = line.part.stockConfidence === "NotCounted" || quantity !== null && quantity > line.part.quantityOnHand;
            const subtotal = quantity === null || basePrice === null ? null : lineCents(basePrice, quantity);
            return <div key={line.key} className="space-y-3 rounded-lg border border-border p-4">
              <div className="flex justify-between gap-2"><div><strong>{line.part.partNumber}</strong> · {line.part.brand || "Unknown brand"}
                <p className="text-xs text-muted">{line.part.name} {line.part.variant && `· ${line.part.variant}`}</p></div>
                <Button variant="ghost" onClick={() => { setCart((rows) => rows.filter((x) => x.key !== line.key)); setReview(null); }}>Remove</Button></div>
              <div className="grid gap-3 sm:grid-cols-2">
                <Field label={line.part.stockUnit === "Centimeter" ? "Length · metres" : "Quantity · pieces"}>
                  <Input type="number" min={line.part.stockUnit === "Centimeter" ? "0.01" : "1"}
                    step={line.part.stockUnit === "Centimeter" ? "0.01" : "1"} value={line.quantity}
                    onChange={(e) => patchLine(line.key, { quantity: e.target.value })} /></Field>
                <Field label={line.part.stockUnit === "Centimeter" ? "Selling price · USD/metre" : "Selling price · USD/piece"}>
                  <Input type="number" min="0.01" step={line.part.stockUnit === "Centimeter" ? "0.01" : "0.000001"}
                    value={line.price} onChange={(e) => patchLine(line.key, { price: e.target.value })} /></Field>
              </div>
              {line.part.stockUnit === "Centimeter" && <p className="text-xs text-muted">{quantity === null ? "Enter length to 0.01 metre (1 cm)." : `${line.quantity} m = ${quantity} cm recorded in stock.`} Confirm enough continuous length physically before selling.</p>}
              <p className="text-sm">Line total: <strong>{subtotal === null ? "Enter valid price and quantity" : formatCents(subtotal)}</strong></p>
              {needsConfirm && <div className="space-y-2 rounded-lg bg-warning-surface p-3 text-sm">
                <p>Book available: {formatStockQuantity(line.part.quantityOnHand, line.part.stockUnit)}. Confirm the sellable goods are physically in hand. Supplier promises are not stock.</p>
                <label className="flex items-center gap-2"><input type="checkbox" checked={line.confirmed} onChange={(e) => patchLine(line.key, { confirmed: e.target.checked })} /> Physically confirmed</label>
                <Field label="Why does this stock need confirmation?"><Input maxLength={500} value={line.reason}
                  onChange={(e) => patchLine(line.key, { reason: e.target.value })} placeholder="Goods in hand; stock not counted yet" /></Field>
                {line.part.quarantinedQuantity > 0 && <p className="text-danger">Held goods cannot satisfy a shortage.</p>}
                {line.part.stockConfidence === "NeedsRecount" && <p className="text-danger">A prior discrepancy needs a full count before another shortage sale.</p>}
              </div>}
            </div>;
          })}
          <div className="text-right text-lg font-semibold">Total {totalCents === null ? "—" : formatCents(totalCents)}</div>
        </div></Card>
        {cart.length > 0 && <Card title="Price guidance"><div className="space-y-2 p-5 text-sm">
          {cart.map((line) => {
            const suggested = suggestions.data?.find((item) => item.partId === line.part.id)?.suggestedUsd;
            const detail = canViewPrivate ? privateCalculations.data?.find((item) => item.partId === line.part.id) : undefined;
            const basePrice = salePricePerBaseUnit(line.price, line.part.stockUnit);
            const overridden = suggested !== null && suggested !== undefined && basePrice !== null && basePrice !== "" && Number(basePrice) !== suggested;
            return <div key={line.key} className="rounded-lg border border-border p-3">
              <p><strong>{line.part.partNumber}</strong> · {suggested === null || suggested === undefined ? "No suggestion; enter a price" :
                line.part.stockUnit === "Centimeter" ? `Suggested $${(suggested * 100).toFixed(2)}/metre` : `Suggested $${suggested.toFixed(2)}/piece`}
                {overridden && <span className="ml-2 text-warning">Manual price {Number(basePrice) < suggested! ? "below" : "above"} suggestion</span>}</p>
              {detail && <p className="mt-1 text-xs text-muted">{detail.unavailableReason ??
                (line.part.stockUnit === "Centimeter" ? `Direct owner-set selling rate: $${detail.directSellingRateUsdPerMeter?.toFixed(2)}/metre; no TVA, conversion or markup.` :
                  `Owner calculation: ${detail.latestActualPackageCost} ${detail.costCurrency} per ${detail.baseUnitsPerPackage} base unit(s), ${detail.taxTreatment} TVA; USD base cost ${detail.baseUnitCostUsd?.toFixed(4)}`)}</p>}
            </div>;
          })}
          <p className="text-xs text-muted">A suggestion is advisory. The entered selling price is the amount posted.</p>
        </div></Card>}
      </div>
      <div className="space-y-6">
        <Card title="3 · Customer"><div className="space-y-3 p-5"><p className="text-xs text-muted">Anonymous cash sale is allowed. Debt or credit requires a named customer.</p>{customerId ? <div className="rounded-lg border border-border p-3"><strong>{selectedCustomer?.customer.name ?? "Loading customer…"}</strong><p className="text-xs text-muted">{selectedCustomer?.customer.distinguishingLabel} {selectedCustomer?.customer.phone}</p><p className="mt-1 text-sm">Balance: {selectedCustomer ? `${selectedCustomer.balanceUsd.toFixed(2)} USD ${selectedCustomer.balanceUsd < 0 ? "credit" : "debt"}` : "…"}</p><Button variant="ghost" onClick={() => { setCustomerId(null); setCredit("0"); setDebt("0"); setReview(null); }}>Use anonymous sale</Button></div> : <><Input value={customerSearch} onChange={(e) => setCustomerSearch(e.target.value)} placeholder="Search customer name or phone" />{customers.data?.items.map((item) => <button key={item.customer.id} type="button" onClick={() => { setCustomerId(item.customer.id); setCustomerSearch(""); setReview(null); }} className="block w-full rounded-lg border border-border p-2 text-left text-sm hover:bg-surface-muted">{item.customer.name} · {item.customer.distinguishingLabel ?? item.customer.phone ?? item.customer.id.slice(0, 8)} <span className="text-muted">({item.balanceUsd.toFixed(2)} USD)</span></button>)}<Link href="/customers" className="text-sm text-accent hover:underline">Create or review a customer →</Link></>}</div></Card>
        <Card title="4 · Payment"><div className="space-y-4 p-5">{accounts.isError && <Alert>Payment accounts are unavailable until the pending backend migrations are applied.</Alert>}{payments.map((row) => <TenderEditor key={row.key} row={row} accounts={paymentAccounts} title="Received" onPatch={(patch) => { patchTender(setPayments, row.key, patch); if (patch.accountId && user) { localStorage.setItem(`inventstock:last-sale-tender:${user.id}`, patch.accountId); setRememberedTenderAccountId(patch.accountId); } }} onRemove={() => { setPayments((items) => items.filter((x) => x.key !== row.key)); setReview(null); }} />)}<Button variant="secondary" onClick={() => { setPayments((rows) => [...rows, newTender(preferredAccount)]); setReview(null); }}>Add payment</Button><div className="border-t border-border pt-3"><p className="mb-2 text-sm font-medium">Explicit change given</p>{change.map((row) => <TenderEditor key={row.key} row={row} accounts={paymentAccounts} title="Change" onPatch={(patch) => patchTender(setChange, row.key, patch)} onRemove={() => { setChange((items) => items.filter((x) => x.key !== row.key)); setReview(null); }} />)}<Button variant="secondary" onClick={() => { setChange((rows) => [...rows, newTender(preferredAccount)]); setReview(null); }}>Add change</Button></div>{customerId && <div className="grid gap-3 sm:grid-cols-2"><Field label="Use customer credit · USD"><Input value={credit} onChange={(e) => { setCredit(e.target.value); setReview(null); }} inputMode="decimal" /></Field><Field label="Leave unpaid debt · USD"><Input value={debt} onChange={(e) => { setDebt(e.target.value); setReview(null); }} inputMode="decimal" /></Field></div>}<p className="text-xs text-muted">LBP/EUR: enter the native amount and the USD amount agreed with the customer. Whish is separate from physical cash. Change must be recorded explicitly.</p></div></Card>
        <Card title="5 · Review and post"><div className="space-y-3 p-5"><Button className="w-full" disabled={Boolean(pending) || post.isPending || cart.length === 0} onClick={prepareReview} onKeyDown={(event) => { if (review && event.key === "Enter") { event.preventDefault(); void submit(); } }}>Review sale</Button>{review && <div className="space-y-2 rounded-lg border border-accent p-3 text-sm"><p><strong>{formatCents(BigInt(Math.round(review.confirmedTotalUsd * 100)))}</strong> · {review.lines.length} line(s) · {selectedCustomer?.customer.name ?? "Anonymous"}</p><p>Received ${review.payments.reduce((sum, x) => sum + x.confirmedUsdAmount, 0).toFixed(2)}; change ${review.change.reduce((sum, x) => sum + x.confirmedUsdAmount, 0).toFixed(2)}; credit ${review.customerCreditUsedUsd.toFixed(2)}; debt ${review.unpaidDebtUsd.toFixed(2)}</p>{canViewPrivate && <div className="space-y-3 rounded-lg bg-warning-surface p-3"><label className="flex items-center gap-2"><input type="checkbox" checked={paperRecovery} onChange={(event) => setPaperRecovery(event.target.checked)} /> Record this as a sale completed on paper</label>{paperRecovery && <><Field label="Original paper date and time"><Input type="datetime-local" value={paperOccurredAt} onChange={(event) => setPaperOccurredAt(event.target.value)} /></Field><Field label="Paper reference"><Input maxLength={100} value={paperReference} onChange={(event) => setPaperReference(event.target.value)} placeholder="Daily book page / receipt reference" /></Field><Field label="Why was it entered later?"><Input maxLength={500} value={paperReason} onChange={(event) => setPaperReason(event.target.value)} placeholder="Server was offline" /></Field><Field label="Original seller note · optional"><Input maxLength={500} value={paperSellerNote} onChange={(event) => setPaperSellerNote(event.target.value)} placeholder="Name from the paper book" /></Field><p className="text-xs text-muted">This posts the same stock and money rules at the original time. It is permanently flagged for owner review.</p></>}</div>}<Button className="w-full" disabled={post.isPending || Boolean(pending)} onClick={() => void submit()}>{post.isPending ? "Posting…" : paperRecovery ? "Post paper-recovery sale" : "Confirm & post sale"}</Button></div>}</div></Card>
      </div>
    </div>
  </div>;
}

