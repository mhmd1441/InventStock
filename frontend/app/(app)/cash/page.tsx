"use client";

import { useMemo, useState } from "react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import {
  useCashAccounts, useCreateExpenseCategory, useExpenseCategories, useOutgoings,
  usePostOutgoing, useRenameExpenseCategory, useReverseOutgoing,
  useSetCashOpeningBalance, useSetExpenseCategoryActive,
} from "@/lib/queries";
import type { MoneyCurrency, PaymentFundingSource } from "@/lib/types";
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
  if (accounts.isLoading || categories.isLoading || outgoings.isLoading) return <Spinner label="Loading cash records…" />;

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

      <Card title="Expense categories">
        <div className="space-y-3 p-5">
          <div className="flex gap-2"><Input value={newCategory} onChange={(e) => setNewCategory(e.target.value)} placeholder="New category" maxLength={100} /><Button onClick={async () => { if (!newCategory.trim()) return; try { await createCategory.mutateAsync(newCategory.trim()); setNewCategory(""); } catch (caught) { fail(caught, "Could not create category."); } }}>Add</Button></div>
          {categories.data?.map((category) => <CategoryEditor key={category.id} category={category} onRename={(name) => renameCategory.mutateAsync({ id: category.id, name })} onToggle={() => setCategoryActive.mutateAsync({ id: category.id, isActive: !category.isActive })} />)}
        </div>
      </Card>
    </div>
  );
}

function CategoryEditor({ category, onRename, onToggle }: { category: { id: string; name: string; isActive: boolean }; onRename: (name: string) => Promise<unknown>; onToggle: () => Promise<unknown> }) {
  const [name, setName] = useState(category.name);
  return <div className="flex gap-2"><Input value={name} onChange={(e) => setName(e.target.value)} disabled={!category.isActive} /><Button variant="secondary" disabled={!category.isActive || !name.trim()} onClick={() => void onRename(name.trim())}>Save</Button><Button variant="ghost" onClick={() => void onToggle()}>{category.isActive ? "Deactivate" : "Reactivate"}</Button></div>;
}
