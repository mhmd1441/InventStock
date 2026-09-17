"use client";

import {
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import { api } from "./api";
import type {
  Category,
  DashboardSummary,
  PagedResult,
  Part,
  PartQuery,
  RegisterUserRequest,
  SaveCategoryRequest,
  SavePartRequest,
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
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: () => api.get<DashboardSummary>("/api/dashboard"),
  });
}

export function useParts(query: PartQuery) {
  return useQuery({
    queryKey: queryKeys.parts(query),
    queryFn: () =>
      api.get<PagedResult<Part>>("/api/parts", {
        search: query.search,
        categoryId: query.categoryId,
        supplierId: query.supplierId,
        lowStockOnly: query.lowStockOnly,
        includeInactive: query.includeInactive,
        sortBy: query.sortBy,
        sortDescending: query.sortDescending,
        page: query.page,
        pageSize: query.pageSize,
      }),
    placeholderData: (previous) => previous,
  });
}

export function usePart(id: string) {
  return useQuery({
    queryKey: queryKeys.part(id),
    queryFn: () => api.get<Part>(`/api/parts/${id}`),
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

export function useSuppliers(includeInactive = false) {
  return useQuery({
    queryKey: queryKeys.suppliers(includeInactive),
    queryFn: () =>
      api.get<Supplier[]>("/api/suppliers", { includeInactive }),
  });
}

export function useUsers() {
  return useQuery({
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
      quantityChange,
      reason,
      reference,
    }: {
      partId: string;
      quantityChange: number;
      reason: string;
      reference?: string;
    }) =>
      api.post<StockMovement>(`/api/parts/${partId}/stock/adjust`, {
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
