"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ApiError } from "@/lib/api";
import { useBrands, useCategories, useSavePart, useSuppliers } from "@/lib/queries";
import type { AdminPart } from "@/lib/types";
import {
  Alert,
  Button,
  Field,
  Input,
  Modal,
  Select,
  Textarea,
} from "./ui";

const schema = z.object({
  brandId: z.string().optional(),
  variant: z.string().max(120),
  partNumber: z
    .string()
    .min(1, "Part number is required")
    .max(64)
    .regex(/^[A-Za-z0-9._/-]+$/, "Use letters, digits and . _ / - only"),
  name: z.string().min(1, "Name is required").max(200),
  description: z.string().max(1000).optional(),
  categoryId: z.string().uuid("Choose a category"),
  supplierId: z.string().optional(),
  unitPrice: z.coerce.number().min(0, "Cannot be negative"),
  lowStockThreshold: z.coerce
    .number()
    .int("Whole units only")
    .min(0, "Cannot be negative"),
  barcode: z.string().max(64).optional(),
});

type FormValues = z.input<typeof schema>;

export function PartFormModal({
  part,
  onClose,
}: {
  part: AdminPart | null;
  onClose: () => void;
}) {
  const { data: categories } = useCategories();
  const { data: brands } = useBrands();
  const { data: suppliers } = useSuppliers();
  const save = useSavePart(part?.id);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      partNumber: part?.partNumber ?? "",
      brandId: part?.brandId ?? "",
      variant: part?.variant ?? "",
      name: part?.name ?? "",
      description: part?.description ?? "",
      categoryId: part?.categoryId ?? "",
      supplierId: part?.supplierId ?? "",
      unitPrice: part?.unitPrice ?? 0,
      lowStockThreshold: part?.lowStockThreshold ?? 0,
      barcode: part?.barcode ?? "",
    },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await save.mutateAsync({
        partNumber: values.partNumber,
        brandId: values.brandId || null,
        variant: values.variant,
        name: values.name,
        description: values.description || null,
        categoryId: values.categoryId,
        supplierId: values.supplierId || null,
        unitPrice: Number(values.unitPrice),
        lowStockThreshold: Number(values.lowStockThreshold),
        barcode: values.barcode || null,
      });

      onClose();
    } catch (error) {
      setFormError(
        error instanceof ApiError
          ? [error.message, ...error.fieldMessages].join(" ")
          : "Could not save the part.",
      );
    }
  });

  return (
    <Modal title={part ? `Edit ${part.partNumber}` : "New part"} onClose={onClose}>
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {formError && <Alert>{formError}</Alert>}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Part number" error={errors.partNumber?.message}>
            <Input {...register("partNumber")} placeholder="BRK-1001" />
          </Field>

          <Field label="Barcode" error={errors.barcode?.message} hint="Optional">
            <Input {...register("barcode")} />
          </Field>
        </div>

        <Field label="Name" error={errors.name?.message}>
          <Input {...register("name")} placeholder="Front brake pad set" />
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Brand" error={errors.brandId?.message} hint="Choose a canonical brand; manage spellings on the Brands page">
            <Select {...register("brandId")}>
              <option value="">No brand</option>
              {(brands ?? []).map((brand) => (
                <option key={brand.id} value={brand.id}>{brand.name}</option>
              ))}
            </Select>
          </Field>
          <Field label="Variant" error={errors.variant?.message} hint="Example: includes seal; keep different products separate">
            <Input {...register("variant")} />
          </Field>
        </div>

        <Field label="Description" error={errors.description?.message}>
          <Textarea rows={2} {...register("description")} />
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Category" error={errors.categoryId?.message}>
            <Select {...register("categoryId")}>
              <option value="">Select a category</option>
              {(categories ?? []).map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Supplier" error={errors.supplierId?.message} hint="Optional">
            <Select {...register("supplierId")}>
              <option value="">No supplier</option>
              {(suppliers ?? []).map((supplier) => (
                <option key={supplier.id} value={supplier.id}>
                  {supplier.name}
                </option>
              ))}
            </Select>
          </Field>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Purchase cost (USD)" error={errors.unitPrice?.message}>
            <Input type="number" step="0.01" min="0" {...register("unitPrice")} />
          </Field>

          <Field
            label="Reorder threshold"
            error={errors.lowStockThreshold?.message}
            hint="Flagged when stock falls to this level"
          >
            <Input type="number" min="0" {...register("lowStockThreshold")} />
          </Field>
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Saving…" : "Save part"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
