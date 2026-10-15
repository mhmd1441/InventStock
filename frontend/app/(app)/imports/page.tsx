"use client";

import { useMemo, useState } from "react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import {
  useBrands,
  useCategories,
  useCommitSupplierImport,
  useParts,
  useReviewSupplierImportRow,
  useStageSupplierImport,
  useSupplierImport,
  useSuppliers,
} from "@/lib/queries";
import { parseSupplierImportText, sha256Hex } from "@/lib/supplier-import";
import type {
  Part,
  StageSupplierImportRowRequest,
  StockUnit,
  SupplierImportDecision,
  SupplierImportRow,
} from "@/lib/types";
import { Alert, Badge, Button, Card, Field, Input, Select, Spinner, Textarea } from "@/components/ui";

const PAGE_SIZE = 25;

function ExistingPartPicker({ initialSearch, onSelect, onClose }: {
  initialSearch: string;
  onSelect: (part: Part) => void;
  onClose: () => void;
}) {
  const [search, setSearch] = useState(initialSearch);
  const results = useParts({ search, searchMode: "Auto", page: 1, pageSize: 10 });
  return (
    <div className="mt-3 space-y-2 rounded-lg bg-surface-muted p-3">
      <div className="flex gap-2">
        <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search existing SKU…" autoFocus />
        <Button variant="ghost" onClick={onClose}>Close</Button>
      </div>
      {results.data?.items.map((part) => (
        <button key={part.id} type="button" onClick={() => onSelect(part)}
          className="flex w-full justify-between rounded-lg border border-border bg-surface p-2 text-left text-sm hover:bg-surface-muted">
          <span><strong>{part.partNumber}</strong> · {part.brand} {part.name}</span>
          <span className="text-muted">{part.stockConfidence === "NotCounted" ? "Not counted" : part.stockConfidence === "NeedsRecount" ? `${part.quantityOnHand} (recount needed)` : part.quantityOnHand}</span>
        </button>
      ))}
    </div>
  );
}

function ReviewRow({ batchId, row }: { batchId: string; row: SupplierImportRow }) {
  const categories = useCategories();
  const brands = useBrands(false);
  const review = useReviewSupplierImportRow();
  const [decision, setDecision] = useState<SupplierImportDecision>(row.decision);
  const [categoryId, setCategoryId] = useState(row.categoryId ?? "");
  const [brandId, setBrandId] = useState(row.brandId ?? "");
  const [stockUnit, setStockUnit] = useState<StockUnit>(row.stockUnit);
  const [targetPart, setTargetPart] = useState<Pick<Part, "id" | "partNumber" | "name"> | null>(
    row.targetPartId ? { id: row.targetPartId, partNumber: row.targetPartId.slice(0, 8), name: "Previously selected SKU" } : null,
  );
  const [pickerOpen, setPickerOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    if (decision === "Pending") return setError("Choose Create new, Link existing, or Skip.");
    if (decision === "CreateNew" && !categoryId) return setError("Choose a category for a new SKU.");
    if (decision === "LinkExisting" && !targetPart) return setError("Choose the existing SKU to link.");
    setError(null);
    try {
      await review.mutateAsync({ batchId, rowId: row.id, request: {
        decision,
        categoryId: decision === "CreateNew" ? categoryId : null,
        brandId: decision === "CreateNew" ? brandId || null : null,
        targetPartId: decision === "LinkExisting" ? targetPart?.id : null,
        stockUnit,
      }});
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Could not save this review.");
    }
  };

  return (
    <div className="rounded-xl border border-border p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2"><strong>{row.partNumber}</strong><Badge tone={row.decision === "Pending" ? "warning" : "success"}>{row.decision}</Badge></div>
          <p className="text-sm">{row.brand} {row.name} {row.variant}</p>
          <p className="text-xs text-muted">Source row {row.sourceRowNumber} · supplier code {row.supplierCode ?? "—"} · cost {row.catalogueCost === null ? "unknown" : `${row.catalogueCost} ${row.currency}`}</p>
        </div>
        <div className="flex items-end gap-2">
          <Field label="Decision">
            <Select value={decision} onChange={(event) => setDecision(event.target.value as SupplierImportDecision)} disabled={review.isPending}>
              <option value="Pending">Pending</option><option value="CreateNew">Create new SKU</option>
              <option value="LinkExisting">Link existing SKU</option><option value="Skip">Skip row</option>
            </Select>
          </Field>
          <Button onClick={() => void save()} disabled={review.isPending || decision === "Pending"}>{review.isPending ? "Saving…" : "Save review"}</Button>
        </div>
      </div>

      {decision === "CreateNew" && (
        <div className="mt-3 grid gap-3 md:grid-cols-3">
          <Field label="Category"><Select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}><option value="">Choose category</option>{categories.data?.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</Select></Field>
          <Field label="Canonical brand" hint="Unknown is allowed"><Select value={brandId} onChange={(event) => setBrandId(event.target.value)}><option value="">Unknown brand</option>{brands.data?.filter((brand) => brand.name !== "Unknown").map((brand) => <option key={brand.id} value={brand.id}>{brand.name}</option>)}</Select></Field>
          <Field label="Base stock unit"><Select value={stockUnit} onChange={(event) => setStockUnit(event.target.value as StockUnit)}><option value="Piece">Piece</option><option value="Centimeter">Centimeter</option></Select></Field>
        </div>
      )}

      {decision === "LinkExisting" && (
        <div className="mt-3">
          <Button variant="secondary" onClick={() => setPickerOpen(true)}>{targetPart ? `${targetPart.partNumber} · ${targetPart.name}` : "Find existing SKU"}</Button>
          {pickerOpen && <ExistingPartPicker initialSearch={row.partNumber} onClose={() => setPickerOpen(false)} onSelect={(part) => { setTargetPart(part); setPickerOpen(false); }} />}
        </div>
      )}
      {error && <div className="mt-3"><Alert>{error}</Alert></div>}
    </div>
  );
}

export default function SupplierImportsPage() {
  const { canViewPrivate } = useAuth();
  const suppliers = useSuppliers(false);
  const stage = useStageSupplierImport();
  const commit = useCommitSupplierImport();
  const [supplierId, setSupplierId] = useState("");
  const [sourceName, setSourceName] = useState("pasted-supplier-import.csv");
  const [sourceText, setSourceText] = useState("");
  const [preview, setPreview] = useState<StageSupplierImportRowRequest[]>([]);
  const [batchId, setBatchId] = useState<string | null>(null);
  const [openBatchId, setOpenBatchId] = useState("");
  const [page, setPage] = useState(1);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const batchQuery = useSupplierImport(batchId);
  const batch = batchQuery.data;

  const visibleRows = useMemo(() => batch?.rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE) ?? [], [batch, page]);
  const pending = batch?.rows.filter((row) => row.decision === "Pending").length ?? 0;
  const totalPages = Math.max(1, Math.ceil((batch?.rows.length ?? 0) / PAGE_SIZE));

  if (!canViewPrivate) return <Alert>Use “Reveal admin costs” to open private supplier imports.</Alert>;

  const prepare = () => {
    setError(null); setMessage(null);
    try { setPreview(parseSupplierImportText(sourceText)); }
    catch (caught) { setPreview([]); setError(caught instanceof Error ? caught.message : "Could not parse the supplier rows."); }
  };

  const stageRows = async () => {
    if (!supplierId || preview.length === 0) return setError("Choose a supplier and prepare valid rows first.");
    setError(null); setMessage(null);
    try {
      const staged = await stage.mutateAsync({ supplierId, sourceFileName: sourceName, sourceSha256: await sha256Hex(sourceText), rows: preview });
      setBatchId(staged.id); setOpenBatchId(staged.id); setPage(1);
      setMessage(`Staged ${staged.rows.length} rows. Batch ID: ${staged.id}. Review every row before committing.`);
    } catch (caught) {
      setError(caught instanceof ApiError ? [caught.message, ...caught.fieldMessages].join(" ") : "Could not stage this import.");
    }
  };

  const commitBatch = async () => {
    if (!batch || pending > 0) return;
    if (!confirm(`Commit ${batch.rows.length} reviewed rows? This creates catalogue records and costs, but no stock.`)) return;
    setError(null);
    try {
      const result = await commit.mutateAsync(batch.id);
      setMessage(`Committed: ${result.createdParts} created, ${result.linkedParts} linked, ${result.skippedRows} skipped. No stock was added.`);
    } catch (caught) { setError(caught instanceof ApiError ? caught.message : "Could not commit the import."); }
  };

  return (
    <div className="space-y-6">
      <div><h1 className="text-xl font-semibold tracking-tight">Supplier catalogue imports</h1><p className="text-sm text-muted">Stage, review and commit catalogue evidence. Supplier stock quantities are ignored and never become shop stock.</p></div>
      {error && <Alert>{error}</Alert>}{message && <Alert tone="success">{message}</Alert>}

      <Card title="1. Prepare extracted rows">
        <div className="space-y-4 p-5">
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Supplier"><Select value={supplierId} onChange={(event) => setSupplierId(event.target.value)}><option value="">Choose supplier</option>{suppliers.data?.map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.name}</option>)}</Select></Field>
            <Field label="CSV / TSV file"><Input type="file" accept=".csv,.tsv,.txt,text/csv,text/tab-separated-values" onChange={async (event) => { const file = event.target.files?.[0]; if (!file) return; setSourceName(file.name); setSourceText(await file.text()); setPreview([]); }} /></Field>
          </div>
          <Field label="Extracted rows" hint="Required: PartNumber, Name. Optional: Brand, Variant, SupplierCode, Cost, Currency, TVA. Unknown supplier columns, including stock, are ignored.">
            <Textarea rows={8} value={sourceText} onChange={(event) => { setSourceText(event.target.value); setPreview([]); }} placeholder={"PartNumber,Name,Brand,SupplierCode,Cost,Currency,TVA\n2045012782,Coolant hose,FEBI,433,15,USD,Excluded"} />
          </Field>
          <div className="flex flex-wrap items-center gap-3"><Button variant="secondary" onClick={prepare}>Prepare preview</Button><Button onClick={() => void stageRows()} disabled={stage.isPending || preview.length === 0}>{stage.isPending ? "Staging…" : `Stage ${preview.length || ""} rows`}</Button>{preview.length > 0 && <span className="text-sm text-muted">Preview ready: {preview.length} rows. No database changes until Stage.</span>}</div>
        </div>
      </Card>

      <Card title="Open an existing staged batch">
        <div className="flex gap-2 p-5"><Input value={openBatchId} onChange={(event) => setOpenBatchId(event.target.value)} placeholder="Batch ID" /><Button variant="secondary" onClick={() => { setBatchId(openBatchId.trim() || null); setPage(1); }}>Open</Button></div>
      </Card>

      {batchQuery.isLoading && <Spinner label="Loading import batch…" />}
      {batch && (
        <Card title={`2. Review ${batch.sourceFileName}`} action={<Badge tone={batch.status === "Committed" ? "success" : pending ? "warning" : "accent"}>{batch.status === "Committed" ? "Committed" : `${pending} pending`}</Badge>}>
          <div className="space-y-4 p-5">
            <p className="text-sm text-muted">Supplier: {batch.supplierName} · {batch.rows.length} rows · page {page}/{totalPages}</p>
            {visibleRows.map((row) => <ReviewRow key={row.id} batchId={batch.id} row={row} />)}
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border pt-4">
              <div className="flex gap-2"><Button variant="secondary" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</Button><Button variant="secondary" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>Next</Button></div>
              {batch.status === "Staged" && <Button onClick={() => void commitBatch()} disabled={pending > 0 || commit.isPending}>{pending > 0 ? `Review ${pending} remaining rows` : commit.isPending ? "Committing…" : "Commit reviewed import"}</Button>}
            </div>
          </div>
        </Card>
      )}
    </div>
  );
}
