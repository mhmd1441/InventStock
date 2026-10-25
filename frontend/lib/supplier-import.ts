import type {
  MoneyCurrency,
  StageSupplierImportRowRequest,
  TaxTreatment,
} from "./types";

const headerAliases: Record<string, keyof StageSupplierImportRowRequest> = {
  partnumber: "partNumber",
  partno: "partNumber",
  number: "partNumber",
  sku: "partNumber",
  name: "name",
  description: "name",
  product: "name",
  brand: "brand",
  manufacturer: "brand",
  variant: "variant",
  option: "variant",
  suppliercode: "supplierCode",
  supplierpartnumber: "supplierCode",
  suppliernumber: "supplierCode",
  cost: "catalogueCost",
  price: "catalogueCost",
  cataloguecost: "catalogueCost",
  currency: "currency",
  tax: "taxTreatment",
  tva: "taxTreatment",
  taxtreatment: "taxTreatment",
};

function normalizedHeader(value: string) {
  return value.trim().toLowerCase().replace(/[^a-z0-9]/g, "");
}

function parseLine(line: string, delimiter: string) {
  const cells: string[] = [];
  let value = "";
  let quoted = false;
  for (let index = 0; index < line.length; index++) {
    const character = line[index];
    if (character === '"') {
      if (quoted && line[index + 1] === '"') {
        value += '"';
        index++;
      } else {
        quoted = !quoted;
      }
    } else if (character === delimiter && !quoted) {
      cells.push(value.trim());
      value = "";
    } else {
      value += character;
    }
  }
  if (quoted) throw new Error("An imported row contains an unclosed quote.");
  cells.push(value.trim());
  return cells;
}

function parseCurrency(value: string): MoneyCurrency | null {
  if (!value.trim()) return null;
  const normalized = value.trim().toUpperCase();
  if (normalized === "$" || normalized === "USD") return "USD";
  if (normalized === "€" || normalized === "EUR" || normalized === "EURO") return "EUR";
  if (["LBP", "L.L", "LL", "ل.ل"].includes(normalized)) return "LBP";
  throw new Error(`Unsupported currency “${value}”. Use USD, EUR or LBP.`);
}

function parseTax(value: string): TaxTreatment {
  const normalized = value.trim().toLowerCase();
  if (!normalized || normalized === "unknown") return "Unknown";
  if (["included", "incl", "with", "yes"].includes(normalized)) return "Included";
  if (["excluded", "excl", "without", "no"].includes(normalized)) return "Excluded";
  throw new Error(`Unsupported TVA value “${value}”. Use Included, Excluded or Unknown.`);
}

function parseCost(value: string, row: number) {
  const normalized = value.trim().replace(/\s/g, "");
  if (!normalized) return null;
  if (!/^\d+(?:[.,]\d+)?$/.test(normalized))
    throw new Error(`Row ${row} has an invalid cost. Use digits with one dot or comma decimal separator and no thousands separators.`);
  const amount = Number(normalized.replace(",", "."));
  if (!Number.isFinite(amount) || amount < 0) throw new Error(`Row ${row} has an invalid non-negative cost.`);
  return amount;
}

export function parseSupplierImportText(text: string): StageSupplierImportRowRequest[] {
  const lines = text.replace(/^\uFEFF/, "").split(/\r?\n/).filter((line) => line.trim());
  if (lines.length < 2) throw new Error("The file needs a header row and at least one data row.");
  const first = lines[0];
  const candidates = ["\t", ",", ";"];
  const delimiter = candidates.sort((a, b) => first.split(b).length - first.split(a).length)[0];
  const rawHeaders = parseLine(first, delimiter);
  const headers = rawHeaders.map((header) => headerAliases[normalizedHeader(header)] ?? null);
  if (!headers.includes("partNumber") || !headers.includes("name"))
    throw new Error("Required columns are PartNumber and Name (Number/SKU and Description are accepted aliases).");

  const parsed = lines.slice(1).map((line, rowIndex) => {
    const cells = parseLine(line, delimiter);
    if (cells.length > rawHeaders.length && cells.slice(rawHeaders.length).some((cell) => cell.trim()))
      throw new Error(`Row ${rowIndex + 2} contains more values than the header. Quote values containing the delimiter.`);
    const values = new Map<keyof StageSupplierImportRowRequest, string>();
    headers.forEach((header, index) => {
      if (header) values.set(header, cells[index] ?? "");
    });
    const partNumber = values.get("partNumber")?.trim() ?? "";
    const name = values.get("name")?.trim() ?? "";
    if (!partNumber || !name) throw new Error(`Row ${rowIndex + 2} requires both PartNumber and Name.`);
    if (partNumber.length > 100 || name.length > 200 || (values.get("brand")?.trim().length ?? 0) > 100 ||
        (values.get("variant")?.trim().length ?? 0) > 120 || (values.get("supplierCode")?.trim().length ?? 0) > 100)
      throw new Error(`Row ${rowIndex + 2} contains a field longer than the supported import limit.`);
    const rawCost = values.get("catalogueCost")?.trim() ?? "";
    const catalogueCost = parseCost(rawCost, rowIndex + 2);
    const currency = parseCurrency(values.get("currency") ?? "");
    if ((catalogueCost !== null) !== (currency !== null))
      throw new Error(`Row ${rowIndex + 2} must provide cost and currency together, or leave both blank.`);

    return {
      sourceRowNumber: rowIndex + 2,
      partNumber,
      name,
      brand: values.get("brand")?.trim() || null,
      variant: values.get("variant")?.trim() || null,
      supplierCode: values.get("supplierCode")?.trim() || null,
      catalogueCost,
      currency,
      taxTreatment: parseTax(values.get("taxTreatment") ?? ""),
    };
  });
  const seen = new Set<string>();
  for (const row of parsed) {
    const { sourceRowNumber, ...identity } = row;
    void sourceRowNumber;
    const key = JSON.stringify(identity, Object.keys(identity).sort());
    if (seen.has(key)) throw new Error(`Duplicate supplier row detected at source row ${row.sourceRowNumber}. Remove it before review.`);
    seen.add(key);
  }
  return parsed;
}

export async function sha256Hex(text: string) {
  const bytes = new TextEncoder().encode(text);
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, "0")).join("").toUpperCase();
}
