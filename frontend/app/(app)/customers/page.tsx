"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useCreateCustomer, useCustomerStatement, useCustomers, useCorrectCustomerOpening, useCustomerOpeningStatus, useCorrectCustomerPayment, usePostCustomerPayment, usePostPaperRecoveryCustomerPayment, useSalePaymentAccounts } from "@/lib/queries";
import { centsNumber, excessCreditCents, formatCents, signedUsdCents, usdCents } from "@/lib/sale-math";
import { parseTenderDrafts, preferredTenderAccount, type TenderDraft } from "@/lib/tender-math";
import { readPendingCustomerOperation, rememberPendingCustomerOperation, clearPendingCustomerOperation, type PendingCustomerOperation } from "@/lib/pending-customer-operation";
import { TenderEditor, newTender } from "@/components/tender-editor";
import type { CustomerPayment, CustomerProfile, CustomerStatementEntry } from "@/lib/types";
import { Alert, Badge, Button, Card, Field, Input, Spinner } from "@/components/ui";

function message(error: unknown) { return error instanceof ApiError ? [error.message, ...error.fieldMessages].join(" ") : "The server result is uncertain. Check the operation before recording another one."; }

export default function CustomersPage() {
  const { user, isAdmin, canViewPrivate } = useAuth();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [label, setLabel] = useState("");
  const [phone, setPhone] = useState("");
  const [address, setAddress] = useState("");
  const [createId, setCreateId] = useState(() => crypto.randomUUID());
  const [openingAmount, setOpeningAmount] = useState("");
  const [openingReason, setOpeningReason] = useState("");
  const [openingAsOf, setOpeningAsOf] = useState(() => new Date().toISOString().slice(0, 10));
  const [openingId, setOpeningId] = useState(() => crypto.randomUUID());
  const [correctedOpening, setCorrectedOpening] = useState("");
  const [openingCorrectionReason, setOpeningCorrectionReason] = useState("");
  const [openingCorrectionId, setOpeningCorrectionId] = useState(() => crypto.randomUUID());
  const [payments, setPayments] = useState<TenderDraft[]>(() => [newTender()]);
  const [change, setChange] = useState<TenderDraft[]>([]);
  const [paymentNote, setPaymentNote] = useState("");
  const [paymentId, setPaymentId] = useState(() => crypto.randomUUID());
  const [confirmExcess, setConfirmExcess] = useState(false);
  const [reviewPayment, setReviewPayment] = useState<{ credit: number; excess: number } | null>(null);
  const [paperPayments, setPaperPayments] = useState<TenderDraft[]>(() => [newTender()]);
  const [paperChange, setPaperChange] = useState<TenderDraft[]>([]);
  const [paperNote, setPaperNote] = useState("");
  const [paperReference, setPaperReference] = useState("");
  const [paperReason, setPaperReason] = useState("");
  const [paperOriginalSeller, setPaperOriginalSeller] = useState("");
  const [paperOccurredAt, setPaperOccurredAt] = useState(() => new Date().toISOString().slice(0, 16));
  const [paperPaymentId, setPaperPaymentId] = useState(() => crypto.randomUUID());
  const [confirmPaperExcess, setConfirmPaperExcess] = useState(false);
  const [reviewPaperPayment, setReviewPaperPayment] = useState<{ credit: number; excess: number } | null>(null);
  const [correcting, setCorrecting] = useState<CustomerStatementEntry | null>(null);
  const [correctionReason, setCorrectionReason] = useState("");
  const [pending, setPending] = useState<PendingCustomerOperation | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [lastPayment, setLastPayment] = useState<CustomerPayment | null>(null);
  const customers = useCustomers(search);
  const statement = useCustomerStatement(selectedId);
  const openingStatus = useCustomerOpeningStatus(selectedId, isAdmin);
  const accounts = useSalePaymentAccounts();
  const create = useCreateCustomer();
  const pay = usePostCustomerPayment();
  const postPaperPayment = usePostPaperRecoveryCustomerPayment();
  const correct = useCorrectCustomerPayment();
  const correctOpening = useCorrectCustomerOpening();
  const summary = statement.data?.summary;
  const activeAccounts = accounts.data ?? [];
  const preferredAccount = preferredTenderAccount(activeAccounts);

  useEffect(() => {
    if (!preferredAccount) return;
    queueMicrotask(() => {
      const setDefault = (rows: TenderDraft[]) => rows.map((row) =>
        row.accountId || row.native.trim() || row.usd.trim() ? row : { ...row, accountId: preferredAccount.id });
      setPayments(setDefault);
      setPaperPayments(setDefault);
    });
  }, [preferredAccount]);

  useEffect(() => {
    if (!user) return;
    let active = true;
    const item = readPendingCustomerOperation(user.id);
    queueMicrotask(() => { if (active) setPending(item); });
    if (item) {
      const path = item.kind === "profile" ? "profiles" : item.kind === "payment" || item.kind === "paperPayment" ? "payments" : item.kind === "correction" ? "corrections" : item.kind === "openingCorrection" ? "opening-corrections" : "openings";
      void api.get<unknown>(`/api/customers/operations/${path}/${item.operationId}`).then((result) => {
        if (!active) return;
        clearPendingCustomerOperation(user.id, item.operationId);
        setPending(null); setSelectedId(item.kind === "profile" ? (result as CustomerProfile).id : item.customerId);
        setNotice("Previously pending account operation was already recorded.");
        void queryClient.invalidateQueries({ queryKey: ["customer-statement"] });
        void queryClient.invalidateQueries({ queryKey: ["customer-opening"] });
      }).catch((caught: unknown) => {
        if (!active) return;
        setError(caught instanceof ApiError && caught.status === 404
          ? "No committed account operation was found yet. Check again before replacing it."
          : "Could not establish the account operation's status. Keep it pending.");
      });
    }
    return () => { active = false; };
  }, [user, queryClient]);
  const correctedIds = useMemo(() => new Set(statement.data?.entries.items.filter((x) => x.kind === "PaymentCorrection").map((x) => x.customerPaymentId) ?? []), [statement.data]);
  const patchTender = (set: React.Dispatch<React.SetStateAction<TenderDraft[]>>, key: string, patch: Partial<TenderDraft>) => {
    set((rows) => rows.map((x) => x.key === key ? { ...x, ...patch } : x)); setReviewPayment(null); setConfirmExcess(false);
  };

  const createCustomer = async () => {
    setError(null); setNotice(null);
    if (pending) return;
    if (!name.trim()) return setError("Customer name is required.");
    if (!user) return setError("Sign in before creating a customer.");
    const item: PendingCustomerOperation = { kind: "profile", operationId: createId, actorId: user.id, customerId: "", createdAtUtc: new Date().toISOString() };
    rememberPendingCustomerOperation(item); setPending(item);
    try {
      const customer = await create.mutateAsync({ operationId: createId, name: name.trim(), distinguishingLabel: label.trim() || null,
        phone: phone.trim() || null, address: address.trim() || null, expectedRevision: 0 });
      clearPendingCustomerOperation(user.id, createId); setPending(null);
      setSelectedId(customer.id); setName(""); setLabel(""); setPhone(""); setAddress(""); setCreateId(crypto.randomUUID());
      setNotice(`${customer.name} created. Customer IDs remain distinct even when names match.`);
    } catch (caught) {
      if (caught instanceof ApiError) { clearPendingCustomerOperation(user.id, createId); setPending(null); setCreateId(crypto.randomUUID()); }
      setError(message(caught));
    }
  };

  const journalFor = (kind: PendingCustomerOperation["kind"], operationId: string): PendingCustomerOperation => ({
    kind, operationId, actorId: user!.id, customerId: selectedId!, createdAtUtc: new Date().toISOString(),
  });
  const checkPending = async (item: PendingCustomerOperation) => {
    setError(null);
    const path = item.kind === "profile" ? "profiles" : item.kind === "payment" || item.kind === "paperPayment" ? "payments" : item.kind === "correction" ? "corrections" : item.kind === "openingCorrection" ? "opening-corrections" : "openings";
    try {
      const result = await api.get<unknown>(`/api/customers/operations/${path}/${item.operationId}`);
      if (user) clearPendingCustomerOperation(user.id, item.operationId);
      setPending(null); setNotice("The operation was already recorded. Account history is being refreshed.");
      setSelectedId(item.kind === "profile" ? (result as CustomerProfile).id : item.customerId);
      await queryClient.invalidateQueries({ queryKey: ["customer-statement"] });
      await queryClient.invalidateQueries({ queryKey: ["customer-opening"] });
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 404
        ? "No committed result was found yet. Check again before replacing the payment or correction."
        : "Could not establish the result. Keep this operation pending and retry when connected.");
    }
  };
  const postOpening = async () => {
    if (!selectedId || !user || pending) return;
    setError(null);
    const parsed = signedUsdCents(openingAmount);
    if (parsed === null || !openingReason.trim()) return setError("Enter a reviewed USD opening amount and reason. A negative amount means customer credit.");
    const item = journalFor("opening", openingId);
    rememberPendingCustomerOperation(item); setPending(item);
    try {
      await api.post(`/api/customers/${selectedId}/opening-balance`, { operationId: openingId,
        balanceUsd: centsNumber(parsed), asOfUtc: new Date(`${openingAsOf}T00:00:00Z`).toISOString(), reason: openingReason.trim() });
      clearPendingCustomerOperation(user.id, openingId); setPending(null); setOpeningId(crypto.randomUUID());
      setOpeningAmount(""); setOpeningReason(""); setNotice("Reviewed opening balance recorded."); await statement.refetch();
      await openingStatus.refetch();
    } catch (caught) {
      if (caught instanceof ApiError) { clearPendingCustomerOperation(user.id, openingId); setPending(null); setOpeningId(crypto.randomUUID()); }
      setError(message(caught));
    }
  };

  const postOpeningCorrection = async () => {
    if (!selectedId || !user || !summary || !openingStatus.data || pending) return;
    setError(null); setNotice(null);
    const target = signedUsdCents(correctedOpening);
    const currentOpening = signedUsdCents(openingStatus.data.currentOpeningBalanceUsd.toFixed(2));
    const currentBalance = signedUsdCents(summary.balanceUsd.toFixed(2));
    if (target === null || currentOpening === null || currentBalance === null || !openingCorrectionReason.trim())
      return setError("Enter a valid corrected opening and a reason.");
    const resulting = centsNumber(currentBalance + target - currentOpening);
    if (!window.confirm(`Correct opening from ${openingStatus.data.currentOpeningBalanceUsd.toFixed(2)} to ${centsNumber(target).toFixed(2)} USD? Current account balance will become ${resulting.toFixed(2)} USD. Later transactions remain unchanged.`)) return;
    const item = journalFor("openingCorrection", openingCorrectionId);
    rememberPendingCustomerOperation(item); setPending(item);
    try {
      await correctOpening.mutateAsync({ openingEntryId: openingStatus.data.originalOpeningEntryId,
        operationId: openingCorrectionId, expectedCurrentOpeningBalanceUsd: centsNumber(currentOpening),
        correctedOpeningBalanceUsd: centsNumber(target), confirmedBalanceAfterUsd: resulting,
        reason: openingCorrectionReason.trim() });
      clearPendingCustomerOperation(user.id, openingCorrectionId); setPending(null);
      setOpeningCorrectionId(crypto.randomUUID()); setCorrectedOpening(""); setOpeningCorrectionReason("");
      setNotice("Opening corrected. The original and subsequent account activity remain in history.");
    } catch (caught) {
      if (caught instanceof ApiError) { clearPendingCustomerOperation(user.id, openingCorrectionId); setPending(null); setOpeningCorrectionId(crypto.randomUUID()); }
      setError(message(caught));
    }
  };

  const preparePayment = () => {
    setError(null); setNotice(null);
    try {
      if (!selectedId || !summary) throw new Error("Select a customer and wait for the current balance.");
      const received = parseTenderDrafts(payments, activeAccounts);
      const given = parseTenderDrafts(change, activeAccounts);
      const inCents = received.reduce((sum, x) => sum + usdCents(x.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      const outCents = given.reduce((sum, x) => sum + usdCents(x.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      if (inCents <= outCents) throw new Error("The payment after change must be greater than zero.");
      const net = inCents - outCents;
      const balance = signedUsdCents(summary.balanceUsd.toFixed(2))!;
      const excess = excessCreditCents(net, balance);
      setReviewPayment({ credit: centsNumber(net), excess: centsNumber(excess) });
      setConfirmExcess(false);
    } catch (caught) { setError(caught instanceof Error ? caught.message : "Cannot prepare payment."); }
  };
  const postPayment = async () => {
    if (!selectedId || !user || !reviewPayment || pending || pay.isPending) return;
    if (reviewPayment.excess > 0 && !confirmExcess) return setError("Confirm the extra amount kept as customer credit, or enter change instead.");
    const item = journalFor("payment", paymentId);
    rememberPendingCustomerOperation(item); setPending(item); setError(null);
    try {
      const result = await pay.mutateAsync({ customerId: selectedId, request: { operationId: paymentId,
        confirmedUsdCredit: reviewPayment.credit, confirmedExcessCreditUsd: reviewPayment.excess,
        payments: parseTenderDrafts(payments, activeAccounts), change: parseTenderDrafts(change, activeAccounts), note: paymentNote.trim() || null } });
      clearPendingCustomerOperation(user.id, paymentId); setPending(null); setPaymentId(crypto.randomUUID());
      setLastPayment(result); setReviewPayment(null); setChange([]); setPaymentNote(""); setConfirmExcess(false);
      setPayments([newTender(preferredAccount)]);
      setNotice(`Payment recorded. New balance ${result.balanceAfterUsd.toFixed(2)} USD.`);
    } catch (caught) {
      if (caught instanceof ApiError) { clearPendingCustomerOperation(user.id, paymentId); setPending(null); setPaymentId(crypto.randomUUID()); setReviewPayment(null); }
      setError(message(caught));
    }
  };
  const preparePaperPayment = () => {
    setError(null); setNotice(null);
    try {
      if (!selectedId || !summary) throw new Error("Select a customer and wait for the current balance.");
      if (!paperReference.trim() || !paperReason.trim()) throw new Error("Paper reference and recovery reason are required.");
      const occurred = new Date(paperOccurredAt);
      if (Number.isNaN(occurred.getTime()) || occurred > new Date()) throw new Error("Enter the original payment date and time; it cannot be in the future.");
      const received = parseTenderDrafts(paperPayments, activeAccounts);
      const given = parseTenderDrafts(paperChange, activeAccounts);
      const inCents = received.reduce((sum, x) => sum + usdCents(x.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      const outCents = given.reduce((sum, x) => sum + usdCents(x.confirmedUsdAmount.toFixed(2))!, BigInt(0));
      if (inCents <= outCents) throw new Error("The payment after change must be greater than zero.");
      const net = inCents - outCents;
      const balance = signedUsdCents(summary.balanceUsd.toFixed(2))!;
      const excess = excessCreditCents(net, balance);
      setReviewPaperPayment({ credit: centsNumber(net), excess: centsNumber(excess) });
      setConfirmPaperExcess(false);
    } catch (caught) { setError(caught instanceof Error ? caught.message : "Cannot prepare paper payment."); }
  };
  const postPaperRecoveryPayment = async () => {
    if (!selectedId || !user || !isAdmin || !canViewPrivate || !reviewPaperPayment || pending || postPaperPayment.isPending) return;
    if (reviewPaperPayment.excess > 0 && !confirmPaperExcess) return setError("Confirm the extra amount kept as customer credit, or enter change instead.");
    const occurred = new Date(paperOccurredAt);
    if (Number.isNaN(occurred.getTime())) return setError("Enter a valid original payment date and time.");
    const item = journalFor("paperPayment", paperPaymentId);
    rememberPendingCustomerOperation(item); setPending(item); setError(null);
    try {
      const result = await postPaperPayment.mutateAsync({ customerId: selectedId, request: {
        payment: { operationId: paperPaymentId, confirmedUsdCredit: reviewPaperPayment.credit,
          confirmedExcessCreditUsd: reviewPaperPayment.excess, payments: parseTenderDrafts(paperPayments, activeAccounts),
          change: parseTenderDrafts(paperChange, activeAccounts), note: paperNote.trim() || null },
        originalOccurredAtUtc: occurred.toISOString(), paperReference: paperReference.trim(), reason: paperReason.trim(),
        originalSellerNote: paperOriginalSeller.trim() || null,
      } });
      clearPendingCustomerOperation(user.id, paperPaymentId); setPending(null); setPaperPaymentId(crypto.randomUUID());
      setLastPayment(result); setReviewPaperPayment(null); setPaperChange([]); setPaperNote(""); setPaperReference("");
      setPaperReason(""); setPaperOriginalSeller(""); setConfirmPaperExcess(false); setPaperPayments([newTender(preferredAccount)]);
      setNotice(`Paper payment recorded. New balance ${result.balanceAfterUsd.toFixed(2)} USD.`);
    } catch (caught) {
      if (caught instanceof ApiError) { clearPendingCustomerOperation(user.id, paperPaymentId); setPending(null); setPaperPaymentId(crypto.randomUUID()); setReviewPaperPayment(null); }
      setError(message(caught));
    }
  };
  const correctPayment = async () => {
    if (!user || !selectedId || !summary || !correcting || !correctionReason.trim() || pending) return;
    const id = crypto.randomUUID();
    const item = journalFor("correction", id);
    rememberPendingCustomerOperation(item); setPending(item); setError(null);
    try {
      const current = usdCents(summary.balanceUsd.toFixed(2));
      const restore = usdCents((-correcting.amountUsd).toFixed(2));
      if (current === null || restore === null) throw new Error("Invalid account balance. Refresh before correction.");
      await correct.mutateAsync({ paymentId: correcting.customerPaymentId!, operationId: id,
        reason: correctionReason.trim(), confirmedBalanceAfterUsd: centsNumber(current + restore) });
      clearPendingCustomerOperation(user.id, id); setPending(null); setCorrecting(null); setCorrectionReason("");
      setNotice("Payment corrected with linked account and cash reversals.");
    } catch (caught) {
      if (caught instanceof ApiError) { clearPendingCustomerOperation(user.id, id); setPending(null); }
      setError(message(caught));
    }
  };

  return <div className="space-y-6">
    <div><h1 className="text-xl font-semibold tracking-tight">Customers & accounts</h1><p className="text-sm text-muted">Find the correct person before recording debt or payment. Similar names are never merged automatically.</p></div>
    {error && <Alert>{error}</Alert>}{notice && <Alert tone="success">{notice}</Alert>}
    {pending && <Alert tone="warning"><p>A {pending.kind} has an uncertain result. Do not create a replacement until you check it.</p><p className="text-xs">Operation {pending.operationId}</p><div className="mt-2 flex gap-2"><Button variant="secondary" onClick={() => void checkPending(pending)}>Check result</Button><Button variant="ghost" onClick={() => { if (user && window.confirm("Only clear this after confirming it did not commit. A late commit could duplicate your next entry.")) { clearPendingCustomerOperation(user.id, pending.operationId); setPending(null); } }}>Clear unresolved attempt</Button></div></Alert>}
    <div className="grid gap-6 lg:grid-cols-[minmax(300px,1fr)_minmax(0,1.6fr)]">
      <div className="space-y-6">
        <Card title="Find a customer"><div className="space-y-3 p-5"><Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Name, garage label or phone" />{customers.isLoading && <Spinner label="Searching customers…" />}{customers.isError && <Alert>Customer APIs may not be migrated on this database yet.</Alert>}{customers.data?.items.map((item) => <button key={item.customer.id} type="button" onClick={() => { setSelectedId(item.customer.id); setReviewPayment(null); setLastPayment(null); setCorrecting(null); setCorrectedOpening(""); setOpeningCorrectionReason(""); setConfirmExcess(false); }} className={`block w-full rounded-lg border p-3 text-left text-sm ${selectedId === item.customer.id ? "border-accent" : "border-border hover:bg-surface-muted"}`}><strong>{item.customer.name}</strong><span className="block text-xs text-muted">{item.customer.distinguishingLabel || "No distinguishing label"} · {item.customer.phone || item.customer.id.slice(0, 8)}</span><span>{item.balanceUsd.toFixed(2)} USD {item.balanceUsd < 0 ? "credit" : "debt"}</span></button>)}</div></Card>
        <Card title="Create customer"><div className="space-y-3 p-5"><Alert tone="warning">Check similar names above first. A new name creates a separate account, even if the spelling matches.</Alert><Field label="Name"><Input maxLength={200} value={name} onChange={(e) => setName(e.target.value)} /></Field><Field label="Garage / distinguishing label" hint="Optional, but helps identify two people named Ahmad"><Input maxLength={200} value={label} onChange={(e) => setLabel(e.target.value)} /></Field><Field label="Phone (optional)"><Input maxLength={50} value={phone} onChange={(e) => setPhone(e.target.value)} /></Field><Field label="Address (optional)"><Input maxLength={500} value={address} onChange={(e) => setAddress(e.target.value)} /></Field><Button disabled={create.isPending || Boolean(pending)} onClick={() => void createCustomer()}>Create customer</Button></div></Card>
      </div>
      <div className="space-y-6">
        {!selectedId ? <Card title="Account"><p className="p-5 text-sm text-muted">Search and select one customer to view their ledger or record a payment.</p></Card> : <>
          <Card title={summary ? `${summary.customer.name} · account` : "Loading account…"}><div className="space-y-2 p-5">{statement.isLoading && <Spinner label="Loading account…" />}{statement.isError && <Alert>Could not load this account.</Alert>}{summary && <><p className="text-2xl font-semibold tabular-nums">{summary.balanceUsd.toFixed(2)} USD</p><Badge tone={summary.balanceUsd < 0 ? "success" : summary.balanceUsd > 0 ? "warning" : "neutral"}>{summary.balanceUsd < 0 ? "Customer credit" : summary.balanceUsd > 0 ? "Customer owes" : "Settled"}</Badge><p className="text-xs text-muted">{summary.customer.distinguishingLabel} {summary.customer.phone} · ID {summary.customer.id}</p></>}</div></Card>
          {isAdmin && statement.data?.entries.totalCount === 0 && <Card title="Reviewed opening balance"><div className="space-y-3 p-5"><Alert tone="warning">Opening balances are only for verified legacy debt or credit before the first transaction.</Alert><Field label="Opening balance · USD" hint="Positive = customer debt; negative = customer credit"><Input value={openingAmount} onChange={(e) => setOpeningAmount(e.target.value)} inputMode="decimal" placeholder="500.00 or -25.00" /></Field><Field label="Balance valid as of"><Input type="date" min="2000-01-01" max={new Date().toISOString().slice(0, 10)} value={openingAsOf} onChange={(e) => setOpeningAsOf(e.target.value)} /></Field><Field label="Review source / reason"><Input maxLength={500} value={openingReason} onChange={(e) => setOpeningReason(e.target.value)} /></Field><Button disabled={Boolean(pending)} onClick={() => void postOpening()}>Post opening balance</Button></div></Card>}
          {isAdmin && openingStatus.data && <Card title="Correct reviewed opening"><div className="space-y-3 p-5"><p className="text-sm">Original: {openingStatus.data.originalBalanceUsd.toFixed(2)} USD · current opening: {openingStatus.data.currentOpeningBalanceUsd.toFixed(2)} USD</p><p className="text-xs text-muted">A correction adjusts only the opening. Sales and payments remain intact; the original stays visible in history.</p><Field label="Corrected opening · USD"><Input inputMode="decimal" value={correctedOpening} onChange={(e) => setCorrectedOpening(e.target.value)} placeholder="Correct debt or negative credit" /></Field><Field label="Why is it wrong?"><Input maxLength={500} value={openingCorrectionReason} onChange={(e) => setOpeningCorrectionReason(e.target.value)} /></Field><Button disabled={Boolean(pending) || correctOpening.isPending || !openingCorrectionReason.trim()} onClick={() => void postOpeningCorrection()}>Review & correct opening</Button></div></Card>}
          <Card title="Record customer payment"><div className="space-y-3 p-5"><p className="text-xs text-muted">An ordinary payment reduces USD debt. Excess may be kept as credit only after confirmation. Native cash/Whish and agreed USD are saved separately.</p>{payments.map((row) => <TenderEditor key={row.key} row={row} accounts={activeAccounts} title="Received" onPatch={(patch) => patchTender(setPayments, row.key, patch)} onRemove={() => { setPayments((items) => items.filter((x) => x.key !== row.key)); setReviewPayment(null); }} />)}<Button variant="secondary" onClick={() => setPayments((rows) => [...rows, newTender(preferredAccount)])}>Add payment</Button>{change.map((row) => <TenderEditor key={row.key} row={row} accounts={activeAccounts} title="Change given" onPatch={(patch) => patchTender(setChange, row.key, patch)} onRemove={() => { setChange((items) => items.filter((x) => x.key !== row.key)); setReviewPayment(null); }} />)}<Button variant="secondary" onClick={() => setChange((rows) => [...rows, newTender(preferredAccount)])}>Add change</Button><Field label="Note (optional)"><Input maxLength={500} value={paymentNote} onChange={(e) => setPaymentNote(e.target.value)} /></Field><Button className="w-full" disabled={Boolean(pending)} onClick={preparePayment}>Review payment</Button>{reviewPayment && <div className="space-y-2 rounded-lg border border-accent p-3 text-sm"><p>Credit account by <strong>{formatCents(usdCents(reviewPayment.credit.toFixed(2))!)}</strong>; new balance approximately {(summary!.balanceUsd - reviewPayment.credit).toFixed(2)} USD.</p>{reviewPayment.excess > 0 && <label className="flex items-start gap-2 rounded bg-warning-surface p-2"><input type="checkbox" checked={confirmExcess} onChange={(e) => setConfirmExcess(e.target.checked)} /><span>Keep extra ${reviewPayment.excess.toFixed(2)} as customer credit. Otherwise enter change above.</span></label>}<Button className="w-full" disabled={pay.isPending || Boolean(pending) || (reviewPayment.excess > 0 && !confirmExcess)} onClick={() => void postPayment()}>{pay.isPending ? "Posting…" : "Confirm & record payment"}</Button></div>}{lastPayment && <p className="text-xs text-muted">Last payment operation: {lastPayment.operationId}</p>}</div></Card>
          {isAdmin && canViewPrivate && <Card title="Record paper-era customer payment"><div className="space-y-3 p-5"><Alert tone="warning">Use only for a payment already taken on paper. This creates a flagged, append-only recovery entry using the normal payment rules; it never edits the old day.</Alert><Field label="Original payment date and time"><Input type="datetime-local" value={paperOccurredAt} onChange={(e) => setPaperOccurredAt(e.target.value)} /></Field><Field label="Paper reference"><Input maxLength={120} value={paperReference} onChange={(e) => { setPaperReference(e.target.value); setReviewPaperPayment(null); }} placeholder="Receipt, notebook page, or reference" /></Field><Field label="Why is this being recovered?"><Input maxLength={500} value={paperReason} onChange={(e) => { setPaperReason(e.target.value); setReviewPaperPayment(null); }} /></Field><Field label="Original seller note (optional)"><Input maxLength={200} value={paperOriginalSeller} onChange={(e) => setPaperOriginalSeller(e.target.value)} placeholder="Text only; logged-in admin remains the actor" /></Field>{paperPayments.map((row) => <TenderEditor key={row.key} row={row} accounts={activeAccounts} title="Paper payment received" onPatch={(patch) => { patchTender(setPaperPayments, row.key, patch); setReviewPaperPayment(null); }} onRemove={() => { setPaperPayments((items) => items.filter((x) => x.key !== row.key)); setReviewPaperPayment(null); }} />)}<Button variant="secondary" onClick={() => setPaperPayments((rows) => [...rows, newTender(preferredAccount)])}>Add paper payment tender</Button>{paperChange.map((row) => <TenderEditor key={row.key} row={row} accounts={activeAccounts} title="Paper change given" onPatch={(patch) => { patchTender(setPaperChange, row.key, patch); setReviewPaperPayment(null); }} onRemove={() => { setPaperChange((items) => items.filter((x) => x.key !== row.key)); setReviewPaperPayment(null); }} />)}<Button variant="secondary" onClick={() => setPaperChange((rows) => [...rows, newTender(preferredAccount)])}>Add paper change</Button><Field label="Note (optional)"><Input maxLength={500} value={paperNote} onChange={(e) => setPaperNote(e.target.value)} /></Field><Button className="w-full" disabled={Boolean(pending)} onClick={preparePaperPayment}>Review paper payment</Button>{reviewPaperPayment && <div className="space-y-2 rounded-lg border border-accent p-3 text-sm"><p>Credit account by <strong>{formatCents(usdCents(reviewPaperPayment.credit.toFixed(2))!)}</strong>; new balance approximately {(summary!.balanceUsd - reviewPaperPayment.credit).toFixed(2)} USD.</p>{reviewPaperPayment.excess > 0 && <label className="flex items-start gap-2 rounded bg-warning-surface p-2"><input type="checkbox" checked={confirmPaperExcess} onChange={(e) => setConfirmPaperExcess(e.target.checked)} /><span>Keep extra ${reviewPaperPayment.excess.toFixed(2)} as customer credit. Otherwise enter paper change above.</span></label>}<Button className="w-full" disabled={postPaperPayment.isPending || Boolean(pending) || (reviewPaperPayment.excess > 0 && !confirmPaperExcess)} onClick={() => void postPaperRecoveryPayment()}>{postPaperPayment.isPending ? "Posting…" : "Confirm & record paper payment"}</Button></div>}</div></Card>}
          <Card title="Account history"><div className="divide-y divide-border">{statement.data?.entries.items.map((entry) => <div key={entry.id} className="flex flex-wrap items-center justify-between gap-2 px-5 py-3 text-sm"><div><strong>{entry.kind.replaceAll(/([A-Z])/g, " $1").trim()}</strong><p className="text-xs text-muted">{new Date(entry.recordedAtUtc).toLocaleString()} {entry.reason && `· ${entry.reason}`}</p>{entry.effectiveAtUtc && <p className="text-xs text-muted">As of {new Date(entry.effectiveAtUtc).toLocaleDateString()}</p>}{entry.saleId && <Link href={`/returns?sale=${entry.saleId}`} className="text-xs text-accent hover:underline">View sale / return an item →</Link>}</div><div className="text-right"><span className="font-medium tabular-nums">{entry.amountUsd >= 0 ? "+" : ""}{entry.amountUsd.toFixed(2)} USD</span>{entry.kind === "AccountPayment" && entry.customerPaymentId && !correctedIds.has(entry.customerPaymentId) && <Button variant="ghost" onClick={() => { setCorrecting(entry); setCorrectionReason(""); }}>Correct</Button>}</div></div>)}{statement.data?.entries.items.length === 0 && <p className="p-5 text-sm text-muted">No account entries yet.</p>}</div></Card>
          {correcting && <Card title="Correct mistaken payment"><div className="space-y-3 p-5"><Alert tone="warning">This reverses the customer credit and the exact original native cash/Whish movements. It does not erase the payment.</Alert><p className="text-sm">Payment to reverse: {(-correcting.amountUsd).toFixed(2)} USD</p><Field label="Reason"><Input maxLength={500} value={correctionReason} onChange={(e) => setCorrectionReason(e.target.value)} /></Field><div className="flex gap-2"><Button variant="danger" disabled={correct.isPending || Boolean(pending) || !correctionReason.trim()} onClick={() => void correctPayment()}>Confirm correction</Button><Button variant="secondary" onClick={() => setCorrecting(null)}>Cancel</Button></div></div></Card>}
        </>}
      </div>
    </div>
  </div>;
}
