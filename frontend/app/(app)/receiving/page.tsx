"use client";

import { useMemo, useState } from "react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useParts, usePostPurchaseReceipt, useSuppliers } from "@/lib/queries";
import type { MoneyCurrency, Part, TaxTreatment } from "@/lib/types";
import { Alert, Button, Card, Field, Input, Select, Spinner } from "@/components/ui";

type DraftLine = {
  part: Part;
  packageCount: number;
  baseUnitsPerPackage: number;
  cost: string;
  currency: MoneyCurrency;
  taxTreatment: TaxTreatment;
  supplierCode: string;
};

export default function ReceivingPage() {
  const { canViewPrivate } = useAuth();
  const suppliers = useSuppliers(false);
  const post = usePostPurchaseReceipt();
  const [supplierId, setSupplierId] = useState("");
  const [reference, setReference] = useState("");
  const [notes, setNotes] = useState("");
  const [search, setSearch] = useState("");
  const [lines, setLines] = useState<DraftLine[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [completed, setCompleted] = useState<string | null>(null);
  // Keep this stable while the draft is unresolved. A retry after a lost response must return the
  // original receipt rather than post the stock twice.
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const results = useParts({ search, searchMode: "Auto", page: 1, pageSize: 10 });

  const totalUnits = useMemo(
    () => lines.reduce((sum, line) => sum + line.packageCount * line.baseUnitsPerPackage, 0),
    [lines],
  );

  if (!canViewPrivate) return <Alert>Use “Reveal admin costs” to receive supplier stock.</Alert>;
  if (suppliers.isLoading) return <Spinner label="Loading receiving…" />;

  const addPart = (part: Part) => {
    if (lines.some((line) => line.part.id === part.id)) return;
    setLines((current) => [...current, {
      part, packageCount: 1, baseUnitsPerPackage: 1, cost: "", currency: "USD",
      taxTreatment: "Unknown", supplierCode: "",
    }]);
    setSearch("");
  };

  const updateLine = (index: number, patch: Partial<DraftLine>) =>
    setLines((current) => current.map((line, lineIndex) => lineIndex === index ? { ...line, ...patch } : line));

  const submit = async () => {
    setError(null);
    setCompleted(null);
    if (!supplierId || lines.length === 0) {
      setError("Choose a supplier and add at least one part.");
      return;
    }
    if (lines.some((line) => line.packageCount < 1 || line.baseUnitsPerPackage < 1)) {
      setError("Package counts and base units must be at least 1.");
      return;
    }

    try {
      const receipt = await post.mutateAsync({
        operationId,
        supplierId,
        supplierReference: reference || null,
        notes: notes || null,
        receivedAtUtc: new Date().toISOString(),
        lines: lines.map((line) => ({
          partId: line.part.id,
          packageCount: line.packageCount,
          baseUnitsPerPackage: line.baseUnitsPerPackage,
          costPerPackage: line.cost === "" ? null : Number(line.cost),
          currency: line.cost === "" ? null : line.currency,
          taxTreatment: line.taxTreatment,
          supplierCode: line.supplierCode || null,
        })),
      });
      setCompleted(`Receipt ${receipt.supplierReference ?? receipt.id.slice(0, 8)} posted: ${receipt.lines.length} lines, ${receipt.lines.reduce((sum, line) => sum + line.baseQuantity, 0)} base units.`);
      setLines([]);
      setReference("");
      setNotes("");
      setOperationId(crypto.randomUUID());
    } catch (caught) {
      setError(caught instanceof ApiError ? [caught.message, ...caught.fieldMessages].join(" ") : "Could not post the receipt.");
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Receive supplier stock</h1>
        <p className="text-sm text-muted">Posts stock and private purchase-cost evidence together. It does not mark the supplier as paid.</p>
      </div>

      {error && <Alert>{error}</Alert>}
      {completed && <Alert tone="success">{completed}</Alert>}

      <Card title="Receipt details">
        <div className="grid gap-4 p-5 md:grid-cols-3">
          <Field label="Supplier">
            <Select value={supplierId} onChange={(event) => setSupplierId(event.target.value)}>
              <option value="">Choose supplier</option>
              {suppliers.data?.map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.name}</option>)}
            </Select>
          </Field>
          <Field label="Supplier invoice / reference" hint="Optional">
            <Input value={reference} onChange={(event) => setReference(event.target.value)} maxLength={100} />
          </Field>
          <Field label="Receipt notes" hint="Optional; not payment status">
            <Input value={notes} onChange={(event) => setNotes(event.target.value)} maxLength={500} />
          </Field>
        </div>
      </Card>

      <Card title="Add parts">
        <div className="space-y-3 p-5">
          <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search number, suffix, name or reference…" />
          {search.trim() && (
            <div className="grid gap-2">
              {results.data?.items.map((part) => (
                <button key={part.id} type="button" onClick={() => addPart(part)}
                  className="flex items-center justify-between rounded-lg border border-border p-3 text-left hover:bg-surface-muted">
                  <span><strong>{part.partNumber}</strong> <span className="text-muted">{part.brand} {part.name}</span></span>
                  <span className="text-xs text-muted">{part.stockConfidence === "Counted" ? `${part.quantityOnHand} ${part.stockUnit}` : "Not counted"}</span>
                </button>
              ))}
              {results.data?.items.length === 0 && <p className="text-sm text-muted">No matching part.</p>}
            </div>
          )}
        </div>
      </Card>

      <Card title={`Receipt lines (${lines.length})`}>
        <div className="space-y-4 p-5">
          {lines.length === 0 && <p className="text-sm text-muted">Search above and add the parts that physically arrived.</p>}
          {lines.map((line, index) => (
            <div key={line.part.id} className="rounded-lg border border-border p-4">
              <div className="mb-3 flex items-center justify-between gap-3">
                <div><strong>{line.part.partNumber}</strong><p className="text-sm text-muted">{line.part.name} · base unit: {line.part.stockUnit}</p></div>
                <Button variant="ghost" onClick={() => setLines((current) => current.filter((_, i) => i !== index))}>Remove</Button>
              </div>
              <div className="grid gap-3 md:grid-cols-6">
                <Field label="Packages"><Input type="number" min={1} value={line.packageCount} onChange={(e) => updateLine(index, { packageCount: Number(e.target.value) })} /></Field>
                <Field label={`Base ${line.part.stockUnit}s / package`}><Input type="number" min={1} value={line.baseUnitsPerPackage} onChange={(e) => updateLine(index, { baseUnitsPerPackage: Number(e.target.value) })} /></Field>
                <Field label="Cost / package" hint="Blank if unknown"><Input type="number" min={0} step="0.000001" value={line.cost} onChange={(e) => updateLine(index, { cost: e.target.value })} /></Field>
                <Field label="Currency"><Select disabled={line.cost === ""} value={line.currency} onChange={(e) => updateLine(index, { currency: e.target.value as MoneyCurrency })}><option>USD</option><option>EUR</option><option>LBP</option></Select></Field>
                <Field label="TVA"><Select value={line.taxTreatment} onChange={(e) => updateLine(index, { taxTreatment: e.target.value as TaxTreatment })}><option value="Unknown">Unknown</option><option value="Included">Included</option><option value="Excluded">Excluded</option></Select></Field>
                <Field label="Supplier code"><Input value={line.supplierCode} onChange={(e) => updateLine(index, { supplierCode: e.target.value })} maxLength={100} /></Field>
              </div>
              <p className="mt-3 text-sm text-muted">Adds <strong className="text-foreground">{line.packageCount * line.baseUnitsPerPackage}</strong> base units.</p>
            </div>
          ))}
          <div className="flex items-center justify-between border-t border-border pt-4">
            <span className="text-sm text-muted">Total: {totalUnits} base units</span>
            <Button onClick={() => void submit()} disabled={post.isPending || lines.length === 0}>{post.isPending ? "Posting…" : "Post receipt"}</Button>
          </div>
        </div>
      </Card>
    </div>
  );
}
