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
  RegisterUserRequest,
  SaveCategoryRequest,
  SavePartRequest,
  SaveBrandRequest,
  SaveSupplierRequest,
  StockMovement,
  Supplier,
  User,
} from "./types";

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
};

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
