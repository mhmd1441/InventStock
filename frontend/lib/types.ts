export type Role = "Admin" | "Staff";

export type MovementType = "In" | "Out" | "Adjustment";

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

export interface Part {
  id: string;
  partNumber: string;
  name: string;
  description: string | null;
  categoryId: string;
  categoryName: string;
  supplierId: string | null;
  supplierName: string | null;
  unitPrice: number;
  lowStockThreshold: number;
  barcode: string | null;
  isActive: boolean;
  quantityOnHand: number;
  createdAtUtc: string;
  isLowStock: boolean;
  stockValue: number;
}

export interface StockMovement {
  id: string;
  partId: string;
  partNumber: string;
  partName: string;
  movementType: MovementType;
  quantityChange: number;
  reason: string | null;
  reference: string | null;
  performedByUserId: string;
  performedByUserName: string;
  occurredAtUtc: string;
}

export interface StockChangedNotification {
  partId: string;
  partNumber: string;
  partName: string;
  quantityOnHand: number;
  lowStockThreshold: number;
  isLowStock: boolean;
  movementType: MovementType;
  quantityChange: number;
  performedByUserName: string;
  occurredAtUtc: string;
}

export interface DashboardSummary {
  totalParts: number;
  activeParts: number;
  lowStockCount: number;
  totalCategories: number;
  totalSuppliers: number;
  totalStockUnits: number;
  totalStockValue: number;
  recentMovements: StockMovement[];
  lowStockParts: Part[];
}

export type PartSortField =
  | "PartNumber"
  | "Name"
  | "UnitPrice"
  | "QuantityOnHand"
  | "CreatedAtUtc";

export interface PartQuery {
  search?: string;
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
  name: string;
  description?: string | null;
  categoryId: string;
  supplierId?: string | null;
  unitPrice: number;
  lowStockThreshold: number;
  barcode?: string | null;
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
