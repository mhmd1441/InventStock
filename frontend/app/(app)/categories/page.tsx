"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import {
  useCategories,
  useDeleteCategory,
  useSaveCategory,
} from "@/lib/queries";
import type { Category } from "@/lib/types";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Modal,
  Spinner,
  Table,
  Td,
  Textarea,
  Th,
} from "@/components/ui";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  description: z.string().max(500).optional(),
});

type FormValues = z.infer<typeof schema>;

function CategoryModal({
  category,
  onClose,
}: {
  category: Category | null;
  onClose: () => void;
}) {
  const save = useSaveCategory(category?.id);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: category?.name ?? "",
      description: category?.description ?? "",
    },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await save.mutateAsync({
        name: values.name,
        description: values.description || null,
      });
      onClose();
    } catch (error) {
      setFormError(
        error instanceof ApiError
          ? [error.message, ...error.fieldMessages].join(" ")
          : "Could not save the category.",
      );
    }
  });

  return (
    <Modal
      title={category ? `Edit ${category.name}` : "New category"}
      onClose={onClose}
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {formError && <Alert>{formError}</Alert>}

        <Field label="Name" error={errors.name?.message}>
          <Input {...register("name")} placeholder="Brakes" />
        </Field>

        <Field label="Description" error={errors.description?.message}>
          <Textarea rows={2} {...register("description")} />
        </Field>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Saving…" : "Save category"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

export default function CategoriesPage() {
  const { isAdmin } = useAuth();
  const { data, isLoading, error } = useCategories();
  const remove = useDeleteCategory();

  const [editing, setEditing] = useState<Category | null>(null);
  const [creating, setCreating] = useState(false);
  const [removeError, setRemoveError] = useState<string | null>(null);

  const onDelete = async (category: Category) => {
    setRemoveError(null);

    try {
      await remove.mutateAsync(category.id);
    } catch (err) {
      setRemoveError(
        err instanceof ApiError ? err.message : "Could not delete the category.",
      );
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Categories</h1>
          <p className="text-sm text-muted">
            Group parts so the catalogue stays searchable.
          </p>
        </div>
        {isAdmin && (
          <Button onClick={() => setCreating(true)}>New category</Button>
        )}
      </div>

      {removeError && <Alert>{removeError}</Alert>}

      <Card>
        {error && (
          <div className="p-4">
            <Alert>{(error as Error).message}</Alert>
          </div>
        )}

        {isLoading ? (
          <Spinner label="Loading categories…" />
        ) : !data || data.length === 0 ? (
          <EmptyState message="No categories yet." />
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Name</Th>
                <Th>Description</Th>
                <Th className="text-right">Parts</Th>
                {isAdmin && <Th />}
              </tr>
            </thead>
            <tbody>
              {data.map((category) => (
                <tr key={category.id}>
                  <Td className="font-medium">{category.name}</Td>
                  <Td className="text-muted">{category.description ?? "—"}</Td>
                  <Td className="text-right tabular-nums">
                    {category.partCount}
                  </Td>
                  {isAdmin && (
                    <Td className="text-right">
                      <div className="flex justify-end gap-2">
                        <Button
                          variant="ghost"
                          onClick={() => setEditing(category)}
                        >
                          Edit
                        </Button>
                        <Button
                          variant="ghost"
                          className="text-danger"
                          disabled={category.partCount > 0 || remove.isPending}
                          title={
                            category.partCount > 0
                              ? "Reassign its parts before deleting"
                              : undefined
                          }
                          onClick={() => void onDelete(category)}
                        >
                          Delete
                        </Button>
                      </div>
                    </Td>
                  )}
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>

      {(creating || editing) && (
        <CategoryModal
          category={editing}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
        />
      )}
    </div>
  );
}
