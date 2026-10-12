export type Role = "Admin" | "Staff";

export type MovementType = "In" | "Out" | "Adjustment" | "PhysicalCount";
export type StockUnit = "Piece" | "Centimeter";
export type StockConfidence = "NotCounted" | "Counted";
export type MoneyCurrency = "USD" | "EUR" | "LBP";
export type TaxTreatment = "Unknown" | "Included" | "Excluded";
export type CashAccountKind = "PhysicalSafe" | "Whish";
export type PaymentFundingSource = "ShopAccount" | "PersonalPocket";

export interface User {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  roles: Role[];
}

export interface AuthResult {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  sessionId: string;
  user: User;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface Category {
  id: string;
  name: string;
  description: string | null;
  partCount: number;
}

export interface Supplier {
  id: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  address: string | null;
  isActive: boolean;
  partCount: number;
}

export interface ProductBrand {
  id: string;
  name: string;
  isActive: boolean;
  aliases: string[];
}

export interface CounterPart {
  id: string;
  partNumber: string;
  brand: string;
  brandId: string;
  variant: string;
  name: string;
  description: string | null;
  categoryId: string;
  categoryName: string;
  lowStockThreshold: number;
  barcode: string | null;
  isActive: boolean;
  quantityOnHand: number;
  physicalQuantityOnHand: number;
  quarantinedQuantity: number;
  stockConfidence: StockConfidence;
  stockUnit: StockUnit;
  createdAtUtc: string;
  isLowStock: boolean;
}

export interface AdminPart extends CounterPart {
  supplierId: string | null;
  supplierName: string | null;
  unitPrice: number;
  stockValue: number | null;
}

export type Part = CounterPart | AdminPart;

export function isAdminPart(part: Part): part is AdminPart {
  return "unitPrice" in part;
}

export interface StockMovement {
  id: string;
  operationId: string | null;
  partId: string;
  partNumber: string;
  partName: string;
  movementType: MovementType;
  quantityChange: number;
  countedQuantity: number | null;
  bookQuantityBeforeCount: number | null;
  reason: string | null;
  reference: string | null;
  performedByUserId: string;
  performedByUserName: string;
  occurredAtUtc: string;
}

export interface StockOperationStatus {
  status: "Committed";
  movement: StockMovement;
}

export interface StockChangedNotification {
  partId: string;
  partNumber: string;
  partName: string;
  quantityOnHand: number;
  lowStockThreshold: number;
  isLowStock: boolean;
  stockConfidence: StockConfidence;
  movementType: MovementType;
  quantityChange: number;
  performedByUserName: string;
  occurredAtUtc: string;
}

export interface CounterDashboardSummary {
  totalParts: number;
  activeParts: number;
  lowStockCount: number;
  totalCategories: number;
  totalStockUnits: number;
  countedParts: number;
  notCountedParts: number;
  recentMovements: StockMovement[];
  lowStockParts: Part[];
}

export interface AdminDashboardSummary extends CounterDashboardSummary {
  totalSuppliers: number;
  totalStockValue: number;
}

export type DashboardSummary = CounterDashboardSummary | AdminDashboardSummary;

export type PartSearchMode = "Auto" | "Exact" | "Prefix" | "Suffix" | "Contains";

export type PartSortField =
  | "PartNumber"
  | "Name"
  | "UnitPrice"
  | "QuantityOnHand"
  | "CreatedAtUtc";

export interface PartQuery {
  search?: string;
  searchMode?: PartSearchMode;
  categoryId?: string;
  supplierId?: string;
  lowStockOnly?: boolean;
  includeInactive?: boolean;
  sortBy?: PartSortField;
  sortDescending?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SavePartRequest {
  partNumber: string;
  brandId?: string | null;
  variant?: string;
  name: string;
  description?: string | null;
  categoryId: string;
  supplierId?: string | null;
  unitPrice: number;
  lowStockThreshold: number;
  barcode?: string | null;
  stockUnit: StockUnit;
}

export interface SaveBrandRequest {
  name: string;
  confirmPossibleDuplicate?: boolean;
}

export interface SaveCategoryRequest {
  name: string;
  description?: string | null;
}

export interface SaveSupplierRequest {
  name: string;
  contactName?: string | null;
  email?: string | null;
  phone?: string | null;
  address?: string | null;
}

export interface RegisterUserRequest {
  email: string;
  fullName: string;
  password: string;
  role: Role;
}

export interface PostPurchaseReceiptLineRequest {
  partId: string;
  packageCount: number;
  baseUnitsPerPackage: number;
  costPerPackage: number | null;
  currency: MoneyCurrency | null;
  taxTreatment: TaxTreatment;
  supplierCode?: string | null;
  notes?: string | null;
}

export interface PostPurchaseReceiptRequest {
  operationId: string;
  supplierId: string;
  supplierReference?: string | null;
  notes?: string | null;
  receivedAtUtc: string;
  lines: PostPurchaseReceiptLineRequest[];
}

export interface PurchaseReceiptLine {
  id: string;
  partId: string;
  partNumber: string;
  partName: string;
  stockUnit: StockUnit;
  packageCount: number;
  baseUnitsPerPackage: number;
  baseQuantity: number;
  costPerPackage: number | null;
  currency: MoneyCurrency | null;
  taxTreatment: TaxTreatment;
  supplierCode: string | null;
  notes: string | null;
  stockMovementId: string;
  supplierCostObservationId: string | null;
}

export interface PurchaseReceipt {
  id: string;
  operationId: string;
  supplierId: string;
  supplierName: string;
  supplierReference: string | null;
  notes: string | null;
  receivedAtUtc: string;
  receivedByUserId: string;
  recordedAtUtc: string;
  lines: PurchaseReceiptLine[];
}

export type SupplierImportStatus = "Staged" | "Committed";
export type SupplierImportDecision = "Pending" | "CreateNew" | "LinkExisting" | "Skip";

export interface StageSupplierImportRowRequest {
  sourceRowNumber: number;
  partNumber: string;
  name: string;
  brand?: string | null;
  variant?: string | null;
  supplierCode?: string | null;
  catalogueCost: number | null;
  currency: MoneyCurrency | null;
  taxTreatment: TaxTreatment;
}

export interface StageSupplierImportRequest {
  supplierId: string;
  sourceFileName: string;
  sourceSha256: string;
  rows: StageSupplierImportRowRequest[];
}

export interface SupplierImportRow extends StageSupplierImportRowRequest {
  id: string;
  decision: SupplierImportDecision;
  targetPartId: string | null;
  categoryId: string | null;
  brandId: string | null;
  stockUnit: StockUnit;
  reviewedAtUtc: string | null;
}

export interface SupplierImportBatch {
  id: string;
  supplierId: string;
  supplierName: string;
  sourceFileName: string;
  sourceSha256: string;
  status: SupplierImportStatus;
  createdAtUtc: string;
  committedAtUtc: string | null;
  rows: SupplierImportRow[];
}

export interface ReviewSupplierImportRowRequest {
  decision: Exclude<SupplierImportDecision, "Pending">;
  targetPartId?: string | null;
  categoryId?: string | null;
  brandId?: string | null;
  stockUnit: StockUnit;
}

export interface CommitSupplierImportResult {
  batchId: string;
  createdParts: number;
  linkedParts: number;
  skippedRows: number;
  costObservations: number;
  resultPartIds: string[];
}

export interface CashOpeningBalance {
  id: string;
  cashAccountId: string;
  countedAmount: number;
  countedAtUtc: string;
  countedByUserId: string;
  recordedAtUtc: string;
  operationId: string;
}

export interface CashAccount {
  id: string;
  code: string;
  name: string;
  currency: MoneyCurrency;
  kind: CashAccountKind;
  isActive: boolean;
  openingBalance: CashOpeningBalance | null;
}

export interface ExpenseCategory {
  id: string;
  name: string;
  isActive: boolean;
}

export interface PostOutgoingPaymentRequest {
  operationId: string;
  expenseCategoryId: string;
  fundingSource: PaymentFundingSource;
  cashAccountId: string | null;
  originalAmount: number;
  currency: MoneyCurrency;
  agreedUsdValue: number;
  paidOn: string;
  payee?: string | null;
  description?: string | null;
  forPeriod?: string | null;
  receiptReference?: string | null;
}

export interface OutgoingPayment {
  id: string;
  operationId: string;
  expenseCategoryId: string;
  categoryName: string;
  fundingSource: PaymentFundingSource;
  cashAccountId: string | null;
  cashAccountName: string | null;
  originalAmount: number;
  currency: MoneyCurrency;
  agreedUsdValue: number;
  paidOn: string;
  recordedAtUtc: string;
  recordedByUserId: string;
  payee: string | null;
  description: string | null;
  forPeriod: string | null;
  receiptReference: string | null;
  isReversed: boolean;
  reversalId: string | null;
  reversalReason: string | null;
  reversedAtUtc: string | null;
}

export interface MonthlyOutgoingTotal {
  expenseCategoryId: string;
  categoryName: string;
  agreedUsdTotal: number;
  paymentCount: number;
}

export interface OutgoingPaymentList {
  year: number;
  month: number;
  payments: OutgoingPayment[];
  totals: MonthlyOutgoingTotal[];
}
