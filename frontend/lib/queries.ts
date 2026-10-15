"use client";

import {
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import { api } from "./api";
import { useAuth } from "./auth";
import type {
  Category,
  DashboardSummary,
  PagedResult,
  Part,
  PartQuery,
  ProductBrand,
  PostPurchaseReceiptRequest,
  PurchaseReceipt,
  RegisterUserRequest,
  SaveCategoryRequest,
  SavePartRequest,
  SaveBrandRequest,
  SaveSupplierRequest,
  StageSupplierImportRequest,
  SupplierImportBatch,
  ReviewSupplierImportRowRequest,
  CommitSupplierImportResult,
  StockMovement,
  Supplier,
  User,
  CashAccount,
  ExpenseCategory,
  PostOutgoingPaymentRequest,
  OutgoingPayment,
  OutgoingPaymentList,
  CustomerSummary,
  CustomerProfile,
  CustomerStatement,
  CustomerOpeningStatus,
  CustomerPayment,
  CustomerPaymentRequest,
  PostSaleRequest,
  Sale,
  SalePaymentAccount,
  SaleSummary,
  SaleLookup,
  SaleSearchQuery,
  ReturnableSale,
  PostSaleReturnRequest,
  SaleReturn,
  SellingPriceSuggestion,
  PricingPolicy,
  AdminPriceCalculation,
  CutHoseSellingRate,
} from "./types";

export function useCutHoseSellingRate(partId: string | null) {
  const { canViewPrivate } = useAuth();
  return useQuery({ queryKey: ["cut-hose-rate", partId, canViewPrivate], enabled: canViewPrivate && Boolean(partId),
    queryFn: ({ signal }) => api.get<CutHoseSellingRate>(`/api/admin/pricing/cut-hose/${partId}`, undefined, signal) });
}

export function useSetCutHoseSellingRate() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ partId, expectedCurrentRateId, usdPerMeter }: {
    partId: string; expectedCurrentRateId: string | null; usdPerMeter: number | null }) =>
    api.put<CutHoseSellingRate>(`/api/admin/pricing/cut-hose/${partId}`, { expectedCurrentRateId, usdPerMeter }),
    onSuccess: (_, request) => { void client.invalidateQueries({ queryKey: ["cut-hose-rate", request.partId] });
      void client.invalidateQueries({ queryKey: ["price-suggestions"] });
      void client.invalidateQueries({ queryKey: ["admin-price-calculations"] }); },
  });
}

const pricingPath = (ids: string[], admin: boolean) =>
  `${admin ? "/api/admin/pricing/calculations" : "/api/pricing/suggestions"}?${ids.map((id) => `partIds=${encodeURIComponent(id)}`).join("&")}`;

export function useSellingPriceSuggestions(partIds: string[]) {
  const ids = [...new Set(partIds)].sort();
  return useQuery({ queryKey: ["price-suggestions", ids], enabled: ids.length > 0,
    queryFn: ({ signal }) => api.get<SellingPriceSuggestion[]>(pricingPath(ids, false), undefined, signal) });
}

export function useAdminPriceCalculations(partIds: string[]) {
  const { canViewPrivate } = useAuth();
  const ids = [...new Set(partIds)].sort();
  return useQuery({ queryKey: ["admin-price-calculations", ids, canViewPrivate], enabled: canViewPrivate && ids.length > 0,
    queryFn: ({ signal }) => api.get<AdminPriceCalculation[]>(pricingPath(ids, true), undefined, signal) });
}

export function usePricingPolicy() {
  const { canViewPrivate } = useAuth();
  return useQuery({ queryKey: ["pricing-policy", canViewPrivate], enabled: canViewPrivate,
    queryFn: ({ signal }) => api.get<PricingPolicy>("/api/admin/pricing/policy", undefined, signal) });
}

export function useUpdatePricingPolicy() {
  const client = useQueryClient();
  return useMutation({ mutationFn: (request: Omit<PricingPolicy, "updatedById" | "updatedAtUtc" | "revision"> & { expectedRevision: number }) =>
    api.put<PricingPolicy>("/api/admin/pricing/policy", request),
    onSuccess: () => { void client.invalidateQueries({ queryKey: ["pricing-policy"] });
      void client.invalidateQueries({ queryKey: ["price-suggestions"] });
      void client.invalidateQueries({ queryKey: ["admin-price-calculations"] }); },
  });
}

export const queryKeys = {
  dashboard: ["dashboard"] as const,
  parts: (query: PartQuery) => ["parts", query] as const,
  part: (id: string) => ["part", id] as const,
  partMovements: (id: string, page: number) =>
    ["part", id, "movements", page] as const,
  movements: (page: number) => ["movements", page] as const,
  categories: ["categories"] as const,
  brands: (includeInactive: boolean) => ["brands", includeInactive] as const,
  suppliers: (includeInactive: boolean) =>
    ["suppliers", includeInactive] as const,
  users: ["users"] as const,
  supplierImport: (id: string) => ["supplier-import", id] as const,
  cashAccounts: ["cash-accounts"] as const,
  expenseCategories: ["expense-categories"] as const,
  outgoings: (year: number, month: number) => ["outgoings", year, month] as const,
  customers: (search: string) => ["customers", search] as const,
  customerStatement: (id: string, page: number) => ["customer-statement", id, page] as const,
  customerOpening: (id: string) => ["customer-opening", id] as const,
  salePaymentAccounts: ["sale-payment-accounts"] as const,
  recentSales: ["recent-sales"] as const,
  saleSearch: (query: SaleSearchQuery) => ["sale-search", query] as const,
  returnableSale: (id: string) => ["returnable-sale", id] as const,
};

export function useCustomers(search: string) {
  return useQuery({
    queryKey: queryKeys.customers(search),
    enabled: search.trim().length > 0,
    queryFn: ({ signal }) => api.get<PagedResult<CustomerSummary>>("/api/customers", { search, page: 1, pageSize: 12 }, signal),
  });
}

export function useCustomerStatement(id: string | null, page = 1) {
  return useQuery({
    enabled: Boolean(id), queryKey: queryKeys.customerStatement(id ?? "", page),
    queryFn: ({ signal }) => api.get<CustomerStatement>(`/api/customers/${id}/statement`, { page, pageSize: 20 }, signal),
  });
}

export function useCustomerOpeningStatus(id: string | null, enabled: boolean) {
  return useQuery({
    enabled: Boolean(id) && enabled, queryKey: queryKeys.customerOpening(id ?? ""),
    queryFn: ({ signal }) => api.get<CustomerOpeningStatus | null>(`/api/customers/${id}/opening-balance`, undefined, signal),
  });
}

export function useCorrectCustomerOpening() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ openingEntryId, operationId, expectedCurrentOpeningBalanceUsd, correctedOpeningBalanceUsd,
      confirmedBalanceAfterUsd, reason }: { openingEntryId: string; operationId: string;
      expectedCurrentOpeningBalanceUsd: number; correctedOpeningBalanceUsd: number;
      confirmedBalanceAfterUsd: number; reason: string }) =>
      api.post(`/api/customers/opening-balances/${openingEntryId}/corrections`,
        { operationId, expectedCurrentOpeningBalanceUsd, correctedOpeningBalanceUsd, confirmedBalanceAfterUsd, reason }),
    onSuccess: () => { void client.invalidateQueries({ queryKey: ["customer-opening"] });
      void client.invalidateQueries({ queryKey: ["customer-statement"] }); void client.invalidateQueries({ queryKey: ["customers"] }); },
  });
}

export function useCreateCustomer() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (request: { operationId: string; name: string; distinguishingLabel: string | null; phone: string | null; address: string | null; expectedRevision: number }) =>
      api.post<CustomerProfile>("/api/customers", request),
    onSuccess: () => client.invalidateQueries({ queryKey: ["customers"] }),
  });
}

export function useSalePaymentAccounts() {
  return useQuery({ queryKey: queryKeys.salePaymentAccounts,
    queryFn: ({ signal }) => api.get<SalePaymentAccount[]>("/api/sales/payment-accounts", undefined, signal) });
}

export function usePostSale() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (request: PostSaleRequest) => api.post<Sale>("/api/sales", request),
    onSuccess: async () => { await invalidateStockViews(client); await client.invalidateQueries({ queryKey: ["customer-statement"] });
      await client.invalidateQueries({ queryKey: ["sale-search"] }); await client.invalidateQueries({ queryKey: queryKeys.recentSales }); },
  });
}

export function useRecentSales() {
  return useQuery({ queryKey: queryKeys.recentSales,
    queryFn: ({ signal }) => api.get<SaleSummary[]>("/api/sales/recent", undefined, signal) });
}

export function useSaleSearch(query: SaleSearchQuery) {
  return useQuery({ queryKey: queryKeys.saleSearch(query),
    queryFn: ({ signal }) => api.get<PagedResult<SaleLookup>>("/api/sales/search", query, signal) });
}

export function useReturnableSale(id: string | null) {
  return useQuery({ enabled: Boolean(id), queryKey: queryKeys.returnableSale(id ?? ""),
    queryFn: ({ signal }) => api.get<ReturnableSale>(`/api/sale-returns/sales/${id}/availability`, undefined, signal) });
}

export function usePostSaleReturn() {
  const client = useQueryClient();
  return useMutation({ mutationFn: (request: PostSaleReturnRequest) => api.post<SaleReturn>("/api/sale-returns", request),
    onSuccess: async (_, request) => {
      await invalidateStockViews(client);
      await client.invalidateQueries({ queryKey: queryKeys.returnableSale(request.saleId) });
      await client.invalidateQueries({ queryKey: ["customer-statement"] });
      await client.invalidateQueries({ queryKey: ["customers"] });
    } });
}

export function usePostCustomerPayment() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ customerId, request }: { customerId: string; request: CustomerPaymentRequest }) =>
      api.post<CustomerPayment>(`/api/customers/${customerId}/payments`, request),
    onSuccess: () => { void client.invalidateQueries({ queryKey: ["customer-statement"] }); void client.invalidateQueries({ queryKey: ["customers"] }); },
  });
}

export function useCorrectCustomerPayment() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ paymentId, operationId, reason, confirmedBalanceAfterUsd }: { paymentId: string; operationId: string; reason: string; confirmedBalanceAfterUsd: number }) =>
      api.post(`/api/customers/payments/${paymentId}/corrections`, { operationId, reason, confirmedBalanceAfterUsd }),
    onSuccess: () => { void client.invalidateQueries({ queryKey: ["customer-statement"] }); void client.invalidateQueries({ queryKey: ["customers"] }); },
  });
}

export function useCashAccounts() {
  const { canViewPrivate } = useAuth();
  return useQuery({
    enabled: canViewPrivate,
    queryKey: queryKeys.cashAccounts,
    queryFn: ({ signal }) => api.get<CashAccount[]>("/api/admin/cash/accounts", undefined, signal),
  });
}

export function useSetCashOpeningBalance() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ accountId, countedAmount }: { accountId: string; countedAmount: number }) =>
      api.post(`/api/admin/cash/accounts/${accountId}/opening-balance`, {
        operationId: crypto.randomUUID(), countedAmount, countedAtUtc: new Date().toISOString(),
      }),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.cashAccounts }),
  });
}

export function useExpenseCategories(includeInactive = false) {
  const { canViewPrivate } = useAuth();
  return useQuery({
    enabled: canViewPrivate,
    queryKey: [...queryKeys.expenseCategories, includeInactive],
    queryFn: ({ signal }) => api.get<ExpenseCategory[]>("/api/admin/cash/categories", { includeInactive }, signal),
  });
}

export function useCreateExpenseCategory() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => api.post<ExpenseCategory>("/api/admin/cash/categories", { name }),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.expenseCategories }),
  });
}

export function useRenameExpenseCategory() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => api.put<ExpenseCategory>(`/api/admin/cash/categories/${id}`, { name }),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.expenseCategories }),
  });
}

export function useSetExpenseCategoryActive() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => api.patch<void>(`/api/admin/cash/categories/${id}/active`, { isActive }),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.expenseCategories }),
  });
}

export function useOutgoings(year: number, month: number) {
  const { canViewPrivate } = useAuth();
  return useQuery({
    enabled: canViewPrivate,
    queryKey: queryKeys.outgoings(year, month),
    queryFn: ({ signal }) => api.get<OutgoingPaymentList>("/api/admin/cash/outgoings", { year, month }, signal),
  });
}

export function usePostOutgoing() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (request: PostOutgoingPaymentRequest) => api.post<OutgoingPayment>("/api/admin/cash/outgoings", request),
    onSuccess: () => client.invalidateQueries({ queryKey: ["outgoings"] }),
  });
}

export function useReverseOutgoing() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => api.post<OutgoingPayment>(`/api/admin/cash/outgoings/${id}/reverse`, {
      operationId: crypto.randomUUID(), reason, reversedAtUtc: new Date().toISOString(),
    }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["outgoings"] }),
  });
}

/** Everything that can change when stock moves. */
export function invalidateStockViews(client: QueryClient) {
  return Promise.all([
    client.invalidateQueries({ queryKey: ["dashboard"] }),
    client.invalidateQueries({ queryKey: ["parts"] }),
    client.invalidateQueries({ queryKey: ["part"] }),
    client.invalidateQueries({ queryKey: ["movements"] }),
  ]);
}

export function useDashboard() {
  const { canViewPrivate } = useAuth();
  return useQuery({
    queryKey: [...queryKeys.dashboard, canViewPrivate],
    refetchInterval: 30_000,
    queryFn: ({ signal }) => api.get<DashboardSummary>(canViewPrivate ? "/api/admin/dashboard" : "/api/dashboard", undefined, signal),
  });
}

export function useParts(query: PartQuery) {
  const { canViewPrivate } = useAuth();
  return useQuery({
    queryKey: [...queryKeys.parts(query), canViewPrivate],
    refetchInterval: 30_000,
    queryFn: ({ signal }) =>
      api.get<PagedResult<Part>>(canViewPrivate ? "/api/admin/parts" : "/api/parts", {
        search: query.search,
        searchMode: query.searchMode,
        categoryId: query.categoryId,
        supplierId: query.supplierId,
        lowStockOnly: query.lowStockOnly,
        includeInactive: query.includeInactive,
        sortBy: query.sortBy,
        sortDescending: query.sortDescending,
        page: query.page,
        pageSize: query.pageSize,
      }, signal),
    placeholderData: (previous) => previous,
  });
}

export function usePart(id: string) {
  const { canViewPrivate } = useAuth();
  return useQuery({
    queryKey: [...queryKeys.part(id), canViewPrivate],
    refetchInterval: 30_000,
    queryFn: ({ signal }) => api.get<Part>(`${canViewPrivate ? "/api/admin/parts" : "/api/parts"}/${id}`, undefined, signal),
  });
}

export function usePartMovements(id: string, page: number) {
  return useQuery({
    queryKey: queryKeys.partMovements(id, page),
    queryFn: () =>
      api.get<PagedResult<StockMovement>>(`/api/parts/${id}/movements`, {
        page,
        pageSize: 10,
      }),
    placeholderData: (previous) => previous,
  });
}

export function useMovements(page: number) {
  return useQuery({
    queryKey: queryKeys.movements(page),
    queryFn: () =>
      api.get<PagedResult<StockMovement>>("/api/stock-movements", {
        page,
        pageSize: 20,
      }),
    placeholderData: (previous) => previous,
  });
}

export function useCategories() {
  return useQuery({
    queryKey: queryKeys.categories,
    queryFn: () => api.get<Category[]>("/api/categories"),
  });
}

export function useBrands(includeInactive = false) {
  return useQuery({
    queryKey: queryKeys.brands(includeInactive),
    queryFn: ({ signal }) => api.get<ProductBrand[]>("/api/brands", { includeInactive }, signal),
  });
}

export function useCreateBrand() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveBrandRequest) => api.post<ProductBrand>("/api/brands", request),
    onSuccess: () => client.invalidateQueries({ queryKey: ["brands"] }),
  });
}

export function useAddBrandAlias() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, value }: { id: string; value: string }) =>
      api.post<ProductBrand>(`/api/brands/${id}/aliases`, { value }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["brands"] }),
  });
}

export function useSetBrandActive() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      api.patch<void>(`/api/brands/${id}/active`, { isActive }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["brands"] }),
  });
}

export function useSuppliers(includeInactive = false) {
  const { canViewPrivate } = useAuth();
  return useQuery({
    enabled: canViewPrivate,
    queryKey: queryKeys.suppliers(includeInactive),
    queryFn: ({ signal }) =>
      api.get<Supplier[]>("/api/suppliers", { includeInactive }, signal),
  });
}

export function useUsers() {
  const { canViewPrivate } = useAuth();
  return useQuery({
    enabled: canViewPrivate,
    queryKey: queryKeys.users,
    queryFn: () => api.get<User[]>("/api/users"),
  });
}

export function usePostPurchaseReceipt() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (request: PostPurchaseReceiptRequest) =>
      api.post<PurchaseReceipt>("/api/admin/receipts", request),
    onSuccess: () => invalidateStockViews(client),
  });
}

export function useSupplierImport(id: string | null) {
  const { canViewPrivate } = useAuth();
  return useQuery({
    enabled: canViewPrivate && Boolean(id),
    queryKey: queryKeys.supplierImport(id ?? ""),
    queryFn: ({ signal }) => api.get<SupplierImportBatch>(`/api/admin/supplier-imports/${id}`, undefined, signal),
  });
}

export function useStageSupplierImport() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (request: StageSupplierImportRequest) =>
      api.post<SupplierImportBatch>("/api/admin/supplier-imports", request),
    onSuccess: (batch) => client.setQueryData(queryKeys.supplierImport(batch.id), batch),
  });
}

export function useReviewSupplierImportRow() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ batchId, rowId, request }: { batchId: string; rowId: string; request: ReviewSupplierImportRowRequest }) =>
      api.put<SupplierImportBatch>(`/api/admin/supplier-imports/${batchId}/rows/${rowId}/review`, request),
    onSuccess: (batch) => client.setQueryData(queryKeys.supplierImport(batch.id), batch),
  });
}

export function useCommitSupplierImport() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (batchId: string) => api.post<CommitSupplierImportResult>(`/api/admin/supplier-imports/${batchId}/commit`),
    onSuccess: async (_, batchId) => {
      await client.invalidateQueries({ queryKey: queryKeys.supplierImport(batchId) });
      await client.invalidateQueries({ queryKey: ["parts"] });
      await client.invalidateQueries({ queryKey: ["dashboard"] });
    },
  });
}

export function useSavePart(id?: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (request: SavePartRequest) =>
      id
        ? api.put<Part>(`/api/parts/${id}`, request)
        : api.post<Part>("/api/parts", request),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["parts"] });
      await client.invalidateQueries({ queryKey: ["part"] });
      await client.invalidateQueries({ queryKey: ["dashboard"] });
    },
  });
}

export function useSetPartActive() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      api.patch<void>(`/api/parts/${id}/active`, { isActive }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["parts"] });
      await client.invalidateQueries({ queryKey: ["part"] });
    },
  });
}

interface StockMovementInput {
  partId: string;
  operationId: string;
  quantity: number;
  reason?: string;
  reference?: string;
}

export function useStockIn() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: ({ partId, ...body }: StockMovementInput) =>
      api.post<StockMovement>(`/api/parts/${partId}/stock/in`, body),
    onSuccess: () => invalidateStockViews(client),
  });
}

export function useStockOut() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: ({ partId, ...body }: StockMovementInput) =>
      api.post<StockMovement>(`/api/parts/${partId}/stock/out`, body),
    onSuccess: () => invalidateStockViews(client),
  });
}

export function useStockAdjust() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: ({
      partId,
      operationId,
      quantityChange,
      reason,
      reference,
    }: {
      partId: string;
      operationId: string;
      quantityChange: number;
      reason: string;
      reference?: string;
    }) =>
      api.post<StockMovement>(`/api/parts/${partId}/stock/adjust`, {
        operationId,
        quantityChange,
        reason,
        reference,
      }),
    onSuccess: () => invalidateStockViews(client),
  });
}

export function useStockCount() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: ({
      partId,
      operationId,
      countedQuantity,
      expectedBookQuantity,
      reason,
    }: {
      partId: string;
      operationId: string;
      countedQuantity: number;
      expectedBookQuantity: number;
      reason?: string;
    }) => api.post<StockMovement>(`/api/parts/${partId}/stock/count`, {
      operationId,
      countedQuantity,
      expectedBookQuantity,
      reason,
    }),
    onSuccess: () => invalidateStockViews(client),
  });
}

export function useStockHold() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ partId, ...body }: { partId: string; operationId: string; quantity: number; reason: string; physicalSeparationConfirmed: boolean }) =>
      api.post(`/api/parts/${partId}/stock/hold`, body),
    onSuccess: () => invalidateStockViews(client),
  });
}

export function useStockRelease() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ partId, ...body }: { partId: string; operationId: string; quantity: number; reason: string }) =>
      api.post(`/api/parts/${partId}/stock/release`, body),
    onSuccess: () => invalidateStockViews(client),
  });
}

export function useSaveCategory(id?: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (request: SaveCategoryRequest) =>
      id
        ? api.put<Category>(`/api/categories/${id}`, request)
        : api.post<Category>("/api/categories", request),
    onSuccess: () => client.invalidateQueries({ queryKey: ["categories"] }),
  });
}

export function useDeleteCategory() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/api/categories/${id}`),
    onSuccess: () => client.invalidateQueries({ queryKey: ["categories"] }),
  });
}

export function useSaveSupplier(id?: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (request: SaveSupplierRequest) =>
      id
        ? api.put<Supplier>(`/api/suppliers/${id}`, request)
        : api.post<Supplier>("/api/suppliers", request),
    onSuccess: () => client.invalidateQueries({ queryKey: ["suppliers"] }),
  });
}

export function useSetSupplierActive() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      api.patch<void>(`/api/suppliers/${id}/active`, { isActive }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["suppliers"] }),
  });
}

export function useCreateUser() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (request: RegisterUserRequest) =>
      api.post<User>("/api/users", request),
    onSuccess: () => client.invalidateQueries({ queryKey: ["users"] }),
  });
}

export function useSetUserActive() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      api.patch<void>(`/api/users/${id}/active`, { isActive }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["users"] }),
  });
}
