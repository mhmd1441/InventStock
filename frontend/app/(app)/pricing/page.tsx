"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/lib/auth";
import { useCutHoseSellingRate, useParts, usePricingPolicy, useSetCutHoseSellingRate, useUpdatePricingPolicy } from "@/lib/queries";
import { ApiError } from "@/lib/api";
import { Alert, Button, Card, Field, Input, Spinner } from "@/components/ui";

export default function PricingPage() {
  const { canViewPrivate } = useAuth();
  const policy = usePricingPolicy();
  const update = useUpdatePricingPolicy();
  const [hoseSearch, setHoseSearch] = useState("");
  const [selectedHoseId, setSelectedHoseId] = useState<string | null>(null);
  const [hoseRateInput, setHoseRateInput] = useState("");
  const hoseParts = useParts({ search: hoseSearch, page: 1, pageSize: 10 });
  const hoseRate = useCutHoseSellingRate(selectedHoseId);
  const setHoseRate = useSetCutHoseSellingRate();
  const [markup, setMarkup] = useState("");
  const [tva, setTva] = useState("");
  const [eur, setEur] = useState("");
  const [lbp, setLbp] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    if (!policy.data) return;
    const value = policy.data;
    queueMicrotask(() => { setMarkup(String(value.markupPercent)); setTva(String(value.tvaPercent));
      setEur(value.eurUsdRate?.toString() ?? ""); setLbp(value.lbpPerUsdRate?.toString() ?? ""); });
  }, [policy.data]);
  useEffect(() => {
    if (!hoseRate.data) return;
    queueMicrotask(() => setHoseRateInput(hoseRate.data?.usdPerMeter?.toString() ?? ""));
  }, [hoseRate.data]);
  if (!canViewPrivate) return <Alert tone="warning">Reveal admin costs to edit private pricing rules.</Alert>;
  if (policy.isLoading) return <Spinner label="Loading pricing rules…" />;
  if (!policy.data) return <Alert>Pricing rules are unavailable. The new migration may not be applied yet.</Alert>;

  const save = async () => {
    setMessage(""); setError("");
    const values = [markup, tva, eur, lbp].map((x) => x.trim() === "" ? null : Number(x));
    if (values[0] === null || values[1] === null || values.some((x) => x !== null && !Number.isFinite(x))) {
      setError("Enter valid markup and TVA percentages; leave exchange rates blank only if they are unknown."); return;
    }
    try {
      await update.mutateAsync({ markupPercent: values[0], tvaPercent: values[1], eurUsdRate: values[2],
        lbpPerUsdRate: values[3], expectedRevision: policy.data.revision });
      setMessage("Pricing rule saved. Existing sales and purchase history were not changed.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Could not save pricing rule. Reload before trying again.");
    }
  };
  const saveHoseRate = async (clear: boolean) => {
    setMessage(""); setError("");
    if (!selectedHoseId || !hoseRate.data) return;
    if (!clear && (!/^\d+(?:\.\d{1,2})?$/.test(hoseRateInput) || Number(hoseRateInput) <= 0)) {
      setError("Enter a positive USD selling price per metre, with at most two decimals."); return;
    }
    try {
      await setHoseRate.mutateAsync({ partId: selectedHoseId, expectedCurrentRateId: hoseRate.data.currentRateId,
        usdPerMeter: clear ? null : Number(hoseRateInput) });
      setMessage(clear ? "Cut-hose rate cleared; earlier rates remain in history." : "Direct USD/metre selling rate saved. No TVA or markup was applied.");
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Could not save the rate. Reload and try again.");
    }
  };
  return <div className="max-w-2xl space-y-5"><div><h1 className="text-xl font-semibold">Suggested selling price</h1>
    <p className="text-sm text-muted">Advisory only. The counter can choose a different selling price. Piece-item suggestions use actual purchases; cut hose uses your direct USD/metre rate.</p></div>
    {error && <Alert>{error}</Alert>}{message && <Alert tone="success">{message}</Alert>}
    <Card title="Piece-item calculation"><div className="space-y-4 p-5">
      <Field label="Markup percentage"><Input type="number" min="0" max="1000" step="0.001" value={markup} onChange={(e) => setMarkup(e.target.value)} /></Field>
      <Field label="TVA percentage" hint="Applied only to purchases explicitly marked TVA excluded"><Input type="number" min="0" max="100" step="0.001" value={tva} onChange={(e) => setTva(e.target.value)} /></Field>
      <Field label="USD per EUR" hint="Leave blank until you enter an agreed current rate"><Input type="number" min="0" step="0.000001" value={eur} onChange={(e) => setEur(e.target.value)} /></Field>
      <Field label="LBP per USD" hint="Leave blank until you enter an agreed current rate"><Input type="number" min="0" step="0.001" value={lbp} onChange={(e) => setLbp(e.target.value)} /></Field>
      <p className="text-xs text-muted">Cost per base unit → USD conversion → excluded TVA → markup → round up to a whole USD. Rates here affect future suggestions, not historical cash settlements.</p>
      <Button disabled={update.isPending} onClick={() => void save()}>{update.isPending ? "Saving…" : "Save rule"}</Button>
    </div></Card>
    <Card title="Cut hose · direct selling rate"><div className="space-y-3 p-5">
      <p className="text-sm text-muted">Set the final USD selling price per metre for each cut hose. It is not calculated from purchase cost, TVA, EUR or markup. For example, $6/metre × 0.5 metre = $3.</p>
      <Field label="Find cut hose"><Input value={hoseSearch} onChange={(e) => setHoseSearch(e.target.value)} placeholder="Part number or name" /></Field>
      {hoseParts.data?.items.filter((part) => part.stockUnit === "Centimeter").map((part) =>
        <Button key={part.id} type="button" variant={selectedHoseId === part.id ? "primary" : "secondary"}
          onClick={() => { setSelectedHoseId(part.id); setMessage(""); setError(""); }}>
          {part.partNumber} · {part.name}
        </Button>)}
      {selectedHoseId && hoseRate.data && <div className="space-y-3 rounded-lg border border-border p-3">
        <p className="text-xs text-muted">Current rate: {hoseRate.data.usdPerMeter === null ? "not set" : `$${hoseRate.data.usdPerMeter.toFixed(2)}/metre`}. Revision {hoseRate.data.revision}.</p>
        <Field label="Final selling price · USD/metre"><Input type="number" min="0.01" step="0.01" value={hoseRateInput} onChange={(e) => setHoseRateInput(e.target.value)} /></Field>
        <div className="flex gap-2"><Button disabled={setHoseRate.isPending} onClick={() => void saveHoseRate(false)}>Save cut-hose rate</Button>
          <Button variant="secondary" disabled={setHoseRate.isPending || hoseRate.data.usdPerMeter === null} onClick={() => void saveHoseRate(true)}>Clear rate</Button></div>
      </div>}
    </div></Card></div>;
}
