export type Role = "Admin" | "Staff";

export type MovementType = "In" | "Out" | "Adjustment" | "PhysicalCount";
export type StockUnit = "Piece" | "Centimeter";
export type StockConfidence = "NotCounted" | "Counted" | "NeedsRecount";
export type MoneyCurrency = "USD" | "EUR" | "LBP";
export type TaxTreatment = "Unknown" | "Included" | "Excluded";
export type CashAccountKind = "PhysicalSafe" | "Whish";
export type PaymentFundingSource = "ShopAccount" | "PersonalPocket";

export interface SellingPriceSuggestion { partId: string; suggestedUsd: number | null }
export interface PricingPolicy { markupPercent: number; tvaPercent: number; eurUsdRate: number | null;
  lbpPerUsdRate: number | null; revision: number; updatedById: string | null; updatedAtUtc: string | null }
export interface AdminPriceCalculation extends SellingPriceSuggestion { latestActualPackageCost: number | null;
  costCurrency: MoneyCurrency | null; taxTreatment: TaxTreatment | null; baseUnitsPerPackage: number | null;
  purchasedAtUtc: string | null; baseUnitCostUsd: number | null; unavailableReason: string | null;
  directSellingRateUsdPerMeter: number | null }
export interface CutHoseSellingRate { partId: string; currentRateId: string | null; revision: number;
  usdPerMeter: number | null; updatedAtUtc: string | null; updatedById: string | null }

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

export interface CustomerProfile {
  id: string;
  name: string;
  distinguishingLabel: string | null;
  phone: string | null;
  address: string | null;
  revision: number;
}

export interface CustomerSummary {
  customer: CustomerProfile;
  balanceUsd: number;
}

export interface CustomerStatementEntry {
  id: string;
  kind: string;
  amountUsd: number;
  saleId: string | null;
  customerPaymentId: string | null;
  customerPaymentCorrectionId: string | null;
  operationId: string;
  actorId: string;
  recordedAtUtc: string;
  effectiveAtUtc: string | null;
  reason: string | null;
  originalOpeningEntryId: string | null;
  correctedOpeningBalanceUsd: number | null;
  saleReturnId: string | null;
}

export interface CustomerOpeningStatus {
  originalOpeningEntryId: string;
  originalBalanceUsd: number;
  currentOpeningBalanceUsd: number;
  asOfUtc: string;
}

export interface CustomerStatement {
  summary: CustomerSummary;
  entries: PagedResult<CustomerStatementEntry>;
}

export interface SalePaymentAccount {
  id: string;
  name: string;
  currency: MoneyCurrency;
  kind: CashAccountKind;
}

export interface SaleLineRequest {
  partId: string;
  quantity: number;
  unitSellingPriceUsd: number;
  physicalGoodsConfirmed: boolean;
  stockExceptionReason: string | null;
}

export interface SaleTenderRequest {
  cashAccountId: string;
  currency: MoneyCurrency;
  nativeAmount: number;
  confirmedUsdAmount: number;
}

export interface PostSaleRequest {
  operationId: string;
  customerId: string | null;
  confirmedTotalUsd: number;
  customerCreditUsedUsd: number;
  unpaidDebtUsd: number;
  lines: SaleLineRequest[];
  payments: SaleTenderRequest[];
  change: SaleTenderRequest[];
}

export interface SaleLine {
  id: string;
  partId: string;
  position: number;
  partNumber: string;
  name: string;
  brand: string;
  variant: string;
  stockUnit: StockUnit;
  quantity: number;
  unitSellingPriceUsd: number;
  lineTotalUsd: number;
  stockException: boolean;
  stockShortage: boolean;
  stockExceptionReason: string | null;
  suggestedUnitPriceUsd: number | null;
  priceOverridden: boolean;
}

export interface SaleTender {
  id: string;
  cashAccountId: string;
  accountName: string;
  currency: MoneyCurrency;
  isChange: boolean;
  nativeAmount: number;
  confirmedUsdAmount: number;
}

export interface Sale {
  id: string;
  operationId: string;
  actorId: string;
  postedAtUtc: string;
  customerId: string | null;
  customerName: string | null;
  totalUsd: number;
  paymentsUsd: number;
  changeUsd: number;
  customerCreditUsedUsd: number;
  unpaidDebtUsd: number;
  lines: SaleLine[];
  tenders: SaleTender[];
}

export interface SaleOperation { status: "Committed"; sale: Sale }
export interface SaleSummary { id: string; postedAtUtc: string; customerName: string | null; totalUsd: number; lineCount: number }
export interface SaleLookupLine { partId: string; partNumber: string; name: string; brand: string; variant: string;
  stockUnit: StockUnit; quantity: number }
export interface SaleLookup { id: string; postedAtUtc: string; customerId: string | null; customerName: string | null;
  totalUsd: number; lines: SaleLookupLine[] }
export interface SaleSearchQuery { fromUtc?: string; toUtc?: string; customer?: string; part?: string;
  amountUsd?: string; page: number; pageSize: number; [key: string]: string | number | undefined }
export interface ReturnableSaleLine { saleLineId: string; returnedQuantity: number; remainingQuantity: number; refundedUsd: number; remainingRefundUsd: number }
export interface ReturnableSale { sale: Sale; lines: ReturnableSaleLine[] }
export type ReturnCondition = "Sellable" | "Quarantined";
export interface PostSaleReturnRequest {
  operationId: string; saleId: string; reason: string; confirmedTotalUsd: number; accountReductionUsd: number;
  lines: { saleLineId: string; quantity: number; condition: ReturnCondition; physicalSeparationConfirmed: boolean; issueReason: string | null }[];
  refunds: SaleTenderRequest[];
}
export interface SaleReturn {
  id: string; operationId: string; actorId: string; saleId: string; customerId: string | null;
  postedAtUtc: string; reason: string; totalUsd: number; accountReductionUsd: number;
  lines: { id: string; saleLineId: string; partId: string; quantity: number; refundUsd: number; condition: ReturnCondition; issueReason: string | null }[];
  refunds: { id: string; cashAccountId: string; accountName: string; currency: MoneyCurrency; nativeAmount: number; confirmedUsdAmount: number }[];
}

export interface CustomerPaymentRequest {
  operationId: string;
  confirmedUsdCredit: number;
  confirmedExcessCreditUsd: number;
  payments: SaleTenderRequest[];
  change: SaleTenderRequest[];
  note: string | null;
}

export interface CustomerPayment {
  id: string;
  operationId: string;
  customerId: string;
  customerName: string;
  actorId: string;
  postedAtUtc: string;
  usdCredit: number;
  excessCreditUsd: number;
  balanceBeforeUsd: number;
  balanceAfterUsd: number;
  note: string | null;
  tenders: SaleTender[];
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
