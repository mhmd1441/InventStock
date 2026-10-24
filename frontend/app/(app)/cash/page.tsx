"use client";

import { useMemo, useState } from "react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import {
  useCashAccounts, useCreateExpenseCategory, useExpenseCategories, useOutgoings,
  usePostOutgoing, useRenameExpenseCategory, useReverseOutgoing,
  useSetCashOpeningBalance, useSetExpenseCategoryActive, useCashReconciliation,
  usePostCashReconciliation, usePostSupplierPayment, useReverseSupplierPayment,
  useSupplierPayments, useSuppliers, useCashTransfers, usePostCashTransfer, useReverseCashTransfer,
} from "@/lib/queries";
import type { MoneyCurrency, PaymentFundingSource, PostSupplierPaymentRequest } from "@/lib/types";
import { Alert, Badge, Button, Card, Field, Input, Select, Spinner, Table, Td, Th } from "@/components/ui";

function localDate() {
  const now = new Date();
  const offset = now.getTimezoneOffset() * 60_000;
  return new Date(now.getTime() - offset).toISOString().slice(0, 10);
}

export default function CashPage() {
  const { canViewPrivate } = useAuth();
  const today = localDate();
  const initial = new Date(`${today}T12:00:00`);
  const [year, setYear] = useState(initial.getFullYear());
  const [month, setMonth] = useState(initial.getMonth() + 1);
  const accounts = useCashAccounts();
  const categories = useExpenseCategories(true);
  const outgoings = useOutgoings(year, month);
  const post = usePostOutgoing();
  const reverse = useReverseOutgoing();
  const setOpening = useSetCashOpeningBalance();
  const createCategory = useCreateExpenseCategory();
  const renameCategory = useRenameExpenseCategory();
  const setCategoryActive = useSetExpenseCategoryActive();
  const [reconciliationDate, setReconciliationDate] = useState(today);
  const reconciliation = useCashReconciliation(reconciliationDate);
  const postReconciliation = usePostCashReconciliation();

  const [categoryId, setCategoryId] = useState("");
  const [fundingSource, setFundingSource] = useState<PaymentFundingSource>("ShopAccount");
  const [accountId, setAccountId] = useState("");
  const [currency, setCurrency] = useState<MoneyCurrency>("USD");
  const [amount, setAmount] = useState("");
  const [usdValue, setUsdValue] = useState("");
  const [paidOn, setPaidOn] = useState(today);
  const [payee, setPayee] = useState("");
  const [description, setDescription] = useState("");
  const [forPeriod, setForPeriod] = useState("");
  const [receiptReference, setReceiptReference] = useState("");
  // Keep the command identity stable across an uncertain network response. A retry returns the
  // original payment instead of recording cash out twice.
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const [newCategory, setNewCategory] = useState("");
  const [openingValues, setOpeningValues] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const matchingAccounts = useMemo(
    () => accounts.data?.filter((account) => account.isActive && account.currency === currency) ?? [],
    [accounts.data, currency],
  );

  if (!canViewPrivate) return <Alert tone="warning">Reveal admin costs to access private cash and outgoing-payment records.</Alert>;
  if (accounts.isLoading || categories.isLoading || outgoings.isLoading || reconciliation.isLoading) return <Spinner label="Loading cash records…" />;

  const fail = (caught: unknown, fallback: string) =>
    setError(caught instanceof ApiError ? [caught.message, ...caught.fieldMessages].join(" ") : fallback);

  const submit = async () => {
    setError(null); setMessage(null);
    if (!categoryId || !amount || !usdValue || !paidOn || (fundingSource === "ShopAccount" && !accountId)) {
      setError("Category, amount, agreed USD value, payment date and funding source are required.");
      return;
    }
    try {
      await post.mutateAsync({
        operationId, expenseCategoryId: categoryId, fundingSource,
        cashAccountId: fundingSource === "ShopAccount" ? accountId : null,
        originalAmount: Number(amount), currency, agreedUsdValue: Number(usdValue), paidOn,
        payee: payee || null, description: description || null, forPeriod: forPeriod || null,
        receiptReference: receiptReference || null,
      });
      setAmount(""); setUsdValue(""); setPayee(""); setDescription(""); setForPeriod(""); setReceiptReference("");
      setOperationId(crypto.randomUUID());
      setMessage("Outgoing payment recorded.");
    } catch (caught) { fail(caught, "Could not record the outgoing payment."); }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Cash outgoings</h1>
        <p className="text-sm text-muted">Actual paid expenses and withdrawals only. Planned payments are not recorded here.</p>
      </div>
      <Alert tone="warning">Balances are incomplete until sales, customer payments and all other cash movements are implemented. No expected safe balance is shown.</Alert>
      {error && <Alert>{error}</Alert>}
      {message && <Alert tone="success">{message}</Alert>}

      <Card title="Opening cash counts">
        <div className="grid gap-3 p-5 md:grid-cols-2 xl:grid-cols-4">
          {accounts.data?.map((account) => (
            <div key={account.id} className="rounded-lg border border-border p-3">
              <strong className="text-sm">{account.name}</strong>
              {account.openingBalance ? (
                <p className="mt-2 text-sm text-muted">Opened with <span className="font-medium text-foreground">{account.openingBalance.countedAmount.toLocaleString()} {account.currency}</span></p>
              ) : (
                <div className="mt-2 flex gap-2">
                  <Input type="number" min={0} step="0.000001" placeholder={`Counted ${account.currency}`}
                    value={openingValues[account.id] ?? ""}
                    onChange={(event) => setOpeningValues((current) => ({ ...current, [account.id]: event.target.value }))} />
                  <Button disabled={setOpening.isPending || !openingValues[account.id]?.trim()}
                    onClick={async () => {
                      try {
                        await setOpening.mutateAsync({ accountId: account.id, countedAmount: Number(openingValues[account.id]) });
                        setMessage(`${account.name} opening count recorded.`);
                      } catch (caught) { fail(caught, "Could not record opening count."); }
                    }}>Set</Button>
                </div>
              )}
            </div>
          ))}
        </div>
      </Card>

      <Card title="Daily reconciliation" className="daily-reconciliation-card">
        <div className="flex flex-wrap items-end gap-3 border-b border-border p-5">
          <Field label="Business day (Beirut)"><Input type="date" max={today} value={reconciliationDate} onChange={(event) => setReconciliationDate(event.target.value)} /></Field>
          <p className="max-w-xl text-sm text-muted">Expected uses recorded movements for this Beirut business day. It is not the physical cash count. A close is permanent; anything recorded after it is shown separately for correction.</p>
        </div>
        {reconciliation.data?.accounts.map((account) => <ReconciliationAccount
          key={account.cashAccountId} account={account} businessDate={reconciliationDate} today={today}
          pending={postReconciliation.isPending}
          onPost={async (request) => {
            try {
              await postReconciliation.mutateAsync({ accountId: account.cashAccountId, request });
              setMessage(`${account.accountName} was reconciled for ${reconciliationDate}.`);
            } catch (caught) { fail(caught, "Could not close this account."); }
          }} />)}
      </Card>

      <Card title="Record an actual payment">
        <div className="grid gap-4 p-5 md:grid-cols-3">
          <Field label="Category"><Select value={categoryId} onChange={(e) => setCategoryId(e.target.value)}><option value="">Choose category</option>{categories.data?.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</Select></Field>
          <Field label="Paid from"><Select value={fundingSource} onChange={(e) => { const value = e.target.value as PaymentFundingSource; setFundingSource(value); if (value === "PersonalPocket") setAccountId(""); }}><option value="ShopAccount">Shop safe / Whish</option><option value="PersonalPocket">Family member personally</option></Select></Field>
          <Field label="Payment date" hint="The date money actually left"><Input type="date" max={today} value={paidOn} onChange={(e) => setPaidOn(e.target.value)} /></Field>
          <Field label="Currency"><Select value={currency} onChange={(e) => { const next = e.target.value as MoneyCurrency; setCurrency(next); setAccountId(""); if (next !== "USD") setUsdValue(""); else setUsdValue(amount); }}><option>USD</option><option>LBP</option><option>EUR</option></Select></Field>
          <Field label={`Original amount (${currency})`}><Input type="number" min={0.000001} step="0.000001" value={amount} onChange={(e) => { setAmount(e.target.value); if (currency === "USD") setUsdValue(e.target.value); }} /></Field>
          <Field label="Agreed USD value" hint="Confirmed amount; original currency is preserved"><Input type="number" min={0.000001} step="0.000001" value={usdValue} disabled={currency === "USD"} onChange={(e) => setUsdValue(e.target.value)} /></Field>
          {fundingSource === "ShopAccount" && <Field label="Shop account"><Select value={accountId} onChange={(e) => setAccountId(e.target.value)}><option value="">Choose account</option>{matchingAccounts.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</Select></Field>}
          <Field label="Recipient / payee" hint="Optional"><Input value={payee} onChange={(e) => setPayee(e.target.value)} maxLength={300} /></Field>
          <Field label="For period" hint="Example: 2023"><Input value={forPeriod} onChange={(e) => setForPeriod(e.target.value)} maxLength={300} /></Field>
          <Field label="Description" hint="Optional"><Input value={description} onChange={(e) => setDescription(e.target.value)} maxLength={300} /></Field>
          <Field label="Receipt reference" hint="Text/reference only for now"><Input value={receiptReference} onChange={(e) => setReceiptReference(e.target.value)} maxLength={120} /></Field>
          <div className="flex items-end"><Button className="w-full" disabled={post.isPending} onClick={() => void submit()}>{post.isPending ? "Recording…" : "Record payment"}</Button></div>
        </div>
      </Card>

      <Card title="Monthly view">
        <div className="flex flex-wrap gap-3 border-b border-border p-5">
          <Input className="max-w-28" type="number" min={2000} max={2200} value={year} onChange={(e) => setYear(Number(e.target.value))} />
          <Select className="max-w-40" value={month} onChange={(e) => setMonth(Number(e.target.value))}>{Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>{new Date(2020, i).toLocaleString(undefined, { month: "long" })}</option>)}</Select>
        </div>
        <div className="grid gap-3 p-5 md:grid-cols-3">
          {outgoings.data?.totals.map((total) => <div key={total.expenseCategoryId} className="rounded-lg bg-surface-muted p-3"><p className="text-xs text-muted">{total.categoryName}</p><strong>${total.agreedUsdTotal.toFixed(2)}</strong><span className="ml-2 text-xs text-muted">{total.paymentCount} payment{total.paymentCount === 1 ? "" : "s"}</span></div>)}
          {outgoings.data?.totals.length === 0 && <p className="text-sm text-muted">No active outgoing payments in this month.</p>}
        </div>
        <Table><thead><tr><Th>Date</Th><Th>Category</Th><Th>Paid from</Th><Th>Original</Th><Th>USD value</Th><Th>Details</Th><Th /></tr></thead><tbody>
          {outgoings.data?.payments.map((payment) => <tr key={payment.id} className={payment.isReversed ? "opacity-50" : ""}>
            <Td>{payment.paidOn}</Td><Td>{payment.categoryName}{payment.isReversed && <span className="ml-2"><Badge tone="danger">Reversed</Badge></span>}</Td>
            <Td>{payment.cashAccountName ?? "Paid personally"}</Td><Td>{payment.originalAmount.toLocaleString()} {payment.currency}</Td><Td>${payment.agreedUsdValue.toFixed(2)}</Td>
            <Td><span>{payment.payee ?? "—"}</span>{payment.forPeriod && <p className="text-xs text-muted">For {payment.forPeriod}</p>}{payment.description && <p className="text-xs text-muted">{payment.description}</p>}</Td>
            <Td>{!payment.isReversed && <Button variant="ghost" onClick={async () => { const reason = window.prompt("Reason for reversing this payment?"); if (!reason?.trim()) return; try { await reverse.mutateAsync({ id: payment.id, reason: reason.trim() }); setMessage("Payment reversed with an audit link."); } catch (caught) { fail(caught, "Could not reverse payment."); } }}>Reverse</Button>}</Td>
          </tr>)}</tbody></Table>
      </Card>

      <SupplierPaymentsCard
        accounts={accounts.data ?? []}
        year={year}
        month={month}
        today={today}
        onMessage={setMessage}
        onError={(caught) => fail(caught, "Could not record the supplier payment.")}
      />

      <CashTransfersCard
        accounts={accounts.data ?? []}
        year={year}
        month={month}
        today={today}
        onMessage={setMessage}
        onError={(caught) => fail(caught, "Could not record the transfer.")}
      />

      <Card title="Expense categories">
        <div className="space-y-3 p-5">
          <div className="flex gap-2"><Input value={newCategory} onChange={(e) => setNewCategory(e.target.value)} placeholder="New category" maxLength={100} /><Button onClick={async () => { if (!newCategory.trim()) return; try { await createCategory.mutateAsync(newCategory.trim()); setNewCategory(""); } catch (caught) { fail(caught, "Could not create category."); } }}>Add</Button></div>
          {categories.data?.map((category) => <CategoryEditor key={category.id} category={category} onRename={(name) => renameCategory.mutateAsync({ id: category.id, name })} onToggle={() => setCategoryActive.mutateAsync({ id: category.id, isActive: !category.isActive })} />)}
        </div>
      </Card>
    </div>
  );
}

function SupplierPaymentsCard({ accounts, year, month, today, onMessage, onError }: {
  accounts: import("@/lib/types").CashAccount[];
  year: number;
  month: number;
  today: string;
  onMessage: (message: string) => void;
  onError: (caught: unknown) => void;
}) {
  const suppliers = useSuppliers(false);
  const payments = useSupplierPayments(year, month);
  const post = usePostSupplierPayment();
  const reverse = useReverseSupplierPayment();
  const [supplierId, setSupplierId] = useState("");
  const [accountId, setAccountId] = useState("");
  const [paidOn, setPaidOn] = useState(today);
  const [settlementAmount, setSettlementAmount] = useState("");
  const [settlementUsd, setSettlementUsd] = useState("");
  const [feeAmount, setFeeAmount] = useState("");
  const [feeUsd, setFeeUsd] = useState("");
  const [note, setNote] = useState("");
  const [receiptReference, setReceiptReference] = useState("");
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const selectedAccount = accounts.find((account) => account.id === accountId);

  const submit = async () => {
    if (!supplierId || !selectedAccount || !paidOn || !settlementAmount || !settlementUsd) return;
    const lines: PostSupplierPaymentRequest["lines"] = [{ kind: "Settlement", originalAmount: Number(settlementAmount), currency: selectedAccount.currency, agreedUsdValue: Number(settlementUsd) }];
    if (feeAmount.trim() || feeUsd.trim()) {
      if (!feeAmount || !feeUsd) return;
      lines.push({ kind: "Fee" as const, originalAmount: Number(feeAmount), currency: selectedAccount.currency, agreedUsdValue: Number(feeUsd) });
    }
    try {
      await post.mutateAsync({ operationId, supplierId, cashAccountId: selectedAccount.id, paidOn,
        note: note || null, receiptReference: receiptReference || null, lines });
      setSettlementAmount(""); setSettlementUsd(""); setFeeAmount(""); setFeeUsd(""); setNote(""); setReceiptReference("");
      setOperationId(crypto.randomUUID());
      onMessage("Supplier payment recorded. Any Whish fee is kept as a separate line.");
    } catch (caught) { onError(caught); }
  };

  return <Card title="Supplier payments" className="supplier-payments-card">
    <div className="border-b border-border p-5">
      <p className="mb-4 text-sm text-muted">A supplier payment is not an expense or receipt. A transfer fee is recorded separately, never hidden in the supplier amount.</p>
      <div className="grid gap-4 md:grid-cols-3">
        <Field label="Supplier"><Select value={supplierId} onChange={(event) => setSupplierId(event.target.value)}><option value="">Choose supplier</option>{suppliers.data?.filter((supplier) => supplier.isActive).map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.name}</option>)}</Select></Field>
        <Field label="Paid from"><Select value={accountId} onChange={(event) => { setAccountId(event.target.value); setSettlementUsd(""); setFeeUsd(""); }}><option value="">Choose safe / Whish account</option>{accounts.filter((account) => account.isActive).map((account) => <option key={account.id} value={account.id}>{account.name} ({account.currency})</option>)}</Select></Field>
        <Field label="Payment date" hint="The day money actually left"><Input type="date" max={today} value={paidOn} onChange={(event) => setPaidOn(event.target.value)} /></Field>
        <Field label={`Supplier settlement (${selectedAccount?.currency ?? "currency"})`}><Input type="number" min={0.000001} step="0.000001" value={settlementAmount} onChange={(event) => { setSettlementAmount(event.target.value); if (selectedAccount?.currency === "USD") setSettlementUsd(event.target.value); }} /></Field>
        <Field label="Settlement agreed USD"><Input type="number" min={0.000001} step="0.000001" disabled={selectedAccount?.currency === "USD"} value={settlementUsd} onChange={(event) => setSettlementUsd(event.target.value)} /></Field>
        <Field label={`Separate transfer fee (${selectedAccount?.currency ?? "currency"})`} hint="Optional"><Input type="number" min={0.000001} step="0.000001" value={feeAmount} onChange={(event) => { setFeeAmount(event.target.value); if (selectedAccount?.currency === "USD") setFeeUsd(event.target.value); }} /></Field>
        <Field label="Fee agreed USD" hint="Required only with a fee"><Input type="number" min={0.000001} step="0.000001" disabled={selectedAccount?.currency === "USD"} value={feeUsd} onChange={(event) => setFeeUsd(event.target.value)} /></Field>
        <Field label="Note" hint="Optional"><Input maxLength={500} value={note} onChange={(event) => setNote(event.target.value)} /></Field>
        <Field label="Related receipt references" hint="Informational only"><Input maxLength={500} value={receiptReference} onChange={(event) => setReceiptReference(event.target.value)} /></Field>
        <div className="flex items-end"><Button className="w-full" disabled={post.isPending || !supplierId || !accountId || !settlementAmount || !settlementUsd} onClick={() => void submit()}>{post.isPending ? "Recording…" : "Record supplier payment"}</Button></div>
      </div>
    </div>
    <Table><thead><tr><Th>Date</Th><Th>Supplier</Th><Th>Paid from</Th><Th>Settlement / fee</Th><Th>Details</Th><Th /></tr></thead><tbody>
      {payments.data?.map((payment) => <tr key={payment.id} className={payment.isReversed ? "opacity-50" : ""}>
        <Td>{payment.paidOn}</Td><Td>{payment.supplierName}{payment.isReversed && <span className="ml-2"><Badge tone="danger">Reversed</Badge></span>}</Td><Td>{payment.cashAccountName}</Td>
        <Td>{payment.lines.map((line) => <p key={line.id}>{line.kind === "Fee" ? "Fee" : "Supplier"}: {line.originalAmount.toLocaleString()} {line.currency} <span className="text-xs text-muted">(${line.agreedUsdValue.toFixed(2)})</span></p>)}</Td>
        <Td>{payment.note && <p>{payment.note}</p>}{payment.receiptReference && <p className="text-xs text-muted">Refs: {payment.receiptReference}</p>}</Td>
        <Td>{!payment.isReversed && <Button variant="ghost" onClick={async () => { const reason = window.prompt("Reason for reversing this supplier payment?"); if (!reason?.trim()) return; try { await reverse.mutateAsync({ id: payment.id, operationId: crypto.randomUUID(), reason: reason.trim() }); onMessage("Supplier payment reversed with an audit link."); } catch (caught) { onError(caught); } }}>Reverse</Button>}</Td>
      </tr>)}
      {payments.data?.length === 0 && <tr><td colSpan={6} className="p-3"><span className="text-sm text-muted">No supplier payments in this month.</span></td></tr>}
    </tbody></Table>
  </Card>;
}

function CashTransfersCard({ accounts, year, month, today, onMessage, onError }: {
  accounts: import("@/lib/types").CashAccount[];
  year: number;
  month: number;
  today: string;
  onMessage: (message: string) => void;
  onError: (caught: unknown) => void;
}) {
  const transfers = useCashTransfers(year, month);
  const post = usePostCashTransfer();
  const reverse = useReverseCashTransfer();
  const [sourceId, setSourceId] = useState("");
  const [destinationId, setDestinationId] = useState("");
  const [feeAccountId, setFeeAccountId] = useState("");
  const [transferredOn, setTransferredOn] = useState(today);
  const [sourceAmount, setSourceAmount] = useState("");
  const [destinationAmount, setDestinationAmount] = useState("");
  const [agreedRate, setAgreedRate] = useState("");
  const [feeAmount, setFeeAmount] = useState("");
  const [note, setNote] = useState("");
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const source = accounts.find((account) => account.id === sourceId);
  const destination = accounts.find((account) => account.id === destinationId);
  const feeAccount = accounts.find((account) => account.id === feeAccountId);
  const setSource = (id: string) => {
    setSourceId(id); setFeeAccountId("");
    const account = accounts.find((item) => item.id === id);
    if (account && destination?.currency === account.currency) setAgreedRate("1");
  };
  const setDestination = (id: string) => {
    setDestinationId(id);
    const account = accounts.find((item) => item.id === id);
    if (account && source?.currency === account.currency) setAgreedRate("1");
  };
  const submit = async () => {
    if (!source || !destination || source.id === destination.id || !sourceAmount || !destinationAmount || !agreedRate) return;
    if ((feeAmount.trim() && !feeAccount) || (!feeAmount.trim() && feeAccount)) return;
    try {
      await post.mutateAsync({ operationId, sourceCashAccountId: source.id, destinationCashAccountId: destination.id,
        transferredOn, sourceAmount: Number(sourceAmount), sourceCurrency: source.currency,
        destinationAmount: Number(destinationAmount), destinationCurrency: destination.currency,
        agreedRate: Number(agreedRate), feeCashAccountId: feeAccount?.id ?? null,
        feeAmount: feeAmount.trim() ? Number(feeAmount) : null, feeCurrency: feeAccount?.currency ?? null,
        note: note.trim() || null });
      setSourceAmount(""); setDestinationAmount(""); setAgreedRate(""); setFeeAmount(""); setFeeAccountId(""); setNote(""); setOperationId(crypto.randomUUID());
      onMessage("Transfer recorded as separate source, destination and optional fee movements.");
    } catch (caught) { onError(caught); }
  };
  return <Card title="Transfers and Whish cash-out">
    <div className="border-b border-border p-5">
      <p className="mb-4 text-sm text-muted">Move money between the shop’s own safe and Whish accounts, or exchange currencies. Amounts in and out stay explicit; a cash-out fee is a separate movement, never hidden inside either amount.</p>
      <div className="grid gap-4 md:grid-cols-3">
        <Field label="Source account"><Select value={sourceId} onChange={(event) => setSource(event.target.value)}><option value="">Choose source</option>{accounts.filter((account) => account.isActive).map((account) => <option key={account.id} value={account.id}>{account.name} ({account.currency})</option>)}</Select></Field>
        <Field label="Destination account"><Select value={destinationId} onChange={(event) => setDestination(event.target.value)}><option value="">Choose destination</option>{accounts.filter((account) => account.isActive && account.id !== sourceId).map((account) => <option key={account.id} value={account.id}>{account.name} ({account.currency})</option>)}</Select></Field>
        <Field label="Transfer date"><Input type="date" max={today} value={transferredOn} onChange={(event) => setTransferredOn(event.target.value)} /></Field>
        <Field label={`Amount out (${source?.currency ?? "currency"})`}><Input type="number" min={0.000001} step="0.000001" value={sourceAmount} onChange={(event) => setSourceAmount(event.target.value)} /></Field>
        <Field label={`Amount in (${destination?.currency ?? "currency"})`}><Input type="number" min={0.000001} step="0.000001" value={destinationAmount} onChange={(event) => setDestinationAmount(event.target.value)} /></Field>
        <Field label="Agreed rate / reference" hint={source?.currency === destination?.currency ? "1 for the same currency" : "Record the agreed historical exchange rate"}><Input type="number" min={0.000001} step="0.000001" value={agreedRate} onChange={(event) => setAgreedRate(event.target.value)} /></Field>
        <Field label="Fee paid from" hint="Optional"><Select value={feeAccountId} onChange={(event) => setFeeAccountId(event.target.value)}><option value="">No fee</option>{accounts.filter((account) => account.isActive).map((account) => <option key={account.id} value={account.id}>{account.name} ({account.currency})</option>)}</Select></Field>
        <Field label={`Separate fee (${feeAccount?.currency ?? "currency"})`} hint="Leave both fee fields empty if none"><Input type="number" min={0.000001} step="0.000001" value={feeAmount} onChange={(event) => setFeeAmount(event.target.value)} /></Field>
        <Field label="Note" hint="Optional"><Input maxLength={500} value={note} onChange={(event) => setNote(event.target.value)} /></Field>
        <div className="flex items-end"><Button className="w-full" disabled={post.isPending || !source || !destination || source.id === destination.id || !sourceAmount || !destinationAmount || !agreedRate || (Boolean(feeAmount.trim()) !== Boolean(feeAccount))} onClick={() => void submit()}>{post.isPending ? "Recording…" : "Record transfer"}</Button></div>
      </div>
    </div>
    <Table><thead><tr><Th>Date</Th><Th>Out</Th><Th>In</Th><Th>Rate / fee</Th><Th>Note</Th><Th /></tr></thead><tbody>
      {transfers.data?.map((transfer) => <tr key={transfer.id} className={transfer.isReversed ? "opacity-50" : ""}>
        <Td>{transfer.transferredOn}</Td><Td>{transfer.sourceAccountName}: {transfer.sourceAmount.toLocaleString()} {transfer.sourceCurrency}</Td><Td>{transfer.destinationAccountName}: {transfer.destinationAmount.toLocaleString()} {transfer.destinationCurrency}</Td>
        <Td>Rate {transfer.agreedRate.toLocaleString()}{transfer.feeAmount !== null && <p className="text-xs text-muted">Fee: {transfer.feeAmount.toLocaleString()} {transfer.feeCurrency} from {transfer.feeAccountName}</p>}</Td><Td>{transfer.note ?? "—"}{transfer.isReversed && <p className="text-xs text-danger">Reversed: {transfer.reversalReason}</p>}</Td>
        <Td>{!transfer.isReversed && <Button variant="ghost" onClick={async () => { const reason = window.prompt("Reason for reversing this transfer?"); if (!reason?.trim()) return; try { await reverse.mutateAsync({ id: transfer.id, operationId: crypto.randomUUID(), reason: reason.trim() }); onMessage("Transfer reversed with linked inverse movements."); } catch (caught) { onError(caught); } }}>Reverse</Button>}</Td>
      </tr>)}
      {transfers.data?.length === 0 && <tr><td colSpan={6} className="p-3"><span className="text-sm text-muted">No transfers in this month.</span></td></tr>}
    </tbody></Table>
  </Card>;
}

function ReconciliationAccount({ account, businessDate, today, pending, onPost }: {
  account: import("@/lib/types").CashReconciliationAccount;
  businessDate: string;
  today: string;
  pending: boolean;
  onPost: (request: import("@/lib/types").PostCashReconciliationRequest) => Promise<void>;
}) {
  const [counted, setCounted] = useState("");
  const [opening, setOpening] = useState("");
  const [reason, setReason] = useState("");
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const closed = account.reconciliation;
  const canFinalizePartial = Boolean(closed?.isPartialDay && businessDate < today);
  const expected = account.expectedAmount;
  const difference = counted.trim() && expected !== null ? Number(counted) - expected : null;
  const submit = async () => {
    if (!counted.trim()) return;
    if (expected === null && !opening.trim()) return;
    if (difference !== null && difference !== 0 && !reason.trim()) return;
    await onPost({ operationId, businessDate, openingAmount: expected === null ? Number(opening) : null,
      countedAmount: Number(counted), differenceReason: difference !== null && difference !== 0 ? reason.trim() : null,
      countedAtUtc: new Date().toISOString() });
    setOperationId(crypto.randomUUID());
  };
  return <div data-testid={`reconciliation-${account.accountCode}`} className="cash-reconciliation-account space-y-3 border-b border-border p-5 last:border-0">
    <div className="flex flex-wrap items-center justify-between gap-2"><div><strong>{account.accountName}</strong><span className="ml-2 text-xs text-muted">{account.currency}</span></div>{closed && <Badge tone={closed.isPartialDay ? "warning" : "success"}>{closed.isPartialDay ? "Partial close" : "Closed"}</Badge>}</div>
    <div className="grid gap-3 text-sm md:grid-cols-3">
      <div><p className="text-xs text-muted">Expected from records</p><strong>{expected === null ? "Opening count needed" : `${expected.toLocaleString()} ${account.currency}`}</strong></div>
      <div><p className="text-xs text-muted">Last physical count</p><strong>{account.lastCountedAmount === null ? "No count yet" : `${account.lastCountedAmount.toLocaleString()} ${account.currency}`}</strong></div>
      <div><p className="text-xs text-muted">Opening source</p><strong>{closed ? (closed.openingSource === "PreviousClose" ? "Previous close" : "Admin count") : "—"}</strong></div>
    </div>
    {account.hasPostCloseMovements && <Alert tone="warning">{account.postCloseMovementCount} post-close movement{account.postCloseMovementCount === 1 ? "" : "s"} ({account.postCloseNetAmount.toLocaleString()} {account.currency}) need an explicit correction. The closed count was not changed.</Alert>}
    {closed && !canFinalizePartial ? <div className="rounded-lg bg-surface-muted p-3 text-sm"><span>Physical count: <strong>{closed.countedAmount.toLocaleString()} {account.currency}</strong></span><span className="ml-4">Difference: <strong>{closed.differenceAmount.toLocaleString()} {account.currency}</strong></span>{closed.differenceReason && <p className="mt-1 text-muted">Reason: {closed.differenceReason}</p>}{closed.isPartialDay && <p className="mt-1 text-muted">This is an in-day snapshot. After the Beirut day ends, return here to post its linked final close.</p>}</div> : <div className="grid gap-3 md:grid-cols-4">
      {expected === null && <Field label={`Opening count (${account.currency})`}><Input type="number" min={0} step="0.000001" value={opening} onChange={(event) => setOpening(event.target.value)} /></Field>}
      <Field label={`Physical count (${account.currency})`}><Input type="number" min={0} step="0.000001" value={counted} onChange={(event) => setCounted(event.target.value)} /></Field>
      <Field label="Difference reason" hint="Required when the count differs"><Input value={reason} onChange={(event) => setReason(event.target.value)} maxLength={500} /></Field>
      <div className="flex items-end"><Button className="w-full" disabled={pending || !counted.trim() || (expected === null && !opening.trim()) || (difference !== null && difference !== 0 && !reason.trim())} onClick={() => void submit()}>{pending ? "Closing…" : canFinalizePartial ? "Post final close" : "Record physical close"}</Button></div>
    </div>}
  </div>;
}

function CategoryEditor({ category, onRename, onToggle }: { category: { id: string; name: string; isActive: boolean }; onRename: (name: string) => Promise<unknown>; onToggle: () => Promise<unknown> }) {
  const [name, setName] = useState(category.name);
  return <div className="flex gap-2"><Input value={name} onChange={(e) => setName(e.target.value)} disabled={!category.isActive} /><Button variant="secondary" disabled={!category.isActive || !name.trim()} onClick={() => void onRename(name.trim())}>Save</Button><Button variant="ghost" onClick={() => void onToggle()}>{category.isActive ? "Deactivate" : "Reactivate"}</Button></div>;
}
