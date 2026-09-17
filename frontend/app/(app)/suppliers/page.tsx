"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useSaveSupplier, useSetSupplierActive, useSuppliers } from "@/lib/queries";
import type { Supplier } from "@/lib/types";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Modal,
  Spinner,
  Table,
  Td,
  Th,
} from "@/components/ui";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(150),
  contactName: z.string().max(150).optional(),
  email: z.union([z.string().email("Enter a valid email"), z.literal("")]),
  phone: z.string().max(40).optional(),
  address: z.string().max(300).optional(),
});

type FormValues = z.infer<typeof schema>;

function SupplierModal({
  supplier,
  onClose,
}: {
  supplier: Supplier | null;
  onClose: () => void;
}) {
  const save = useSaveSupplier(supplier?.id);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: supplier?.name ?? "",
      contactName: supplier?.contactName ?? "",
      email: supplier?.email ?? "",
      phone: supplier?.phone ?? "",
      address: supplier?.address ?? "",
    },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await save.mutateAsync({
        name: values.name,
        contactName: values.contactName || null,
        email: values.email || null,
        phone: values.phone || null,
        address: values.address || null,
      });
      onClose();
    } catch (error) {
      setFormError(
        error instanceof ApiError
          ? [error.message, ...error.fieldMessages].join(" ")
          : "Could not save the supplier.",
      );
    }
  });

  return (
    <Modal
      title={supplier ? `Edit ${supplier.name}` : "New supplier"}
      onClose={onClose}
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {formError && <Alert>{formError}</Alert>}

        <Field label="Name" error={errors.name?.message}>
          <Input {...register("name")} placeholder="Bosch Middle East" />
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Contact" error={errors.contactName?.message}>
            <Input {...register("contactName")} />
          </Field>

          <Field label="Email" error={errors.email?.message}>
            <Input type="email" {...register("email")} />
          </Field>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Phone" error={errors.phone?.message}>
            <Input {...register("phone")} />
          </Field>

          <Field label="Address" error={errors.address?.message}>
            <Input {...register("address")} />
          </Field>
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Saving…" : "Save supplier"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

export default function SuppliersPage() {
  const { isAdmin } = useAuth();
  const [includeInactive, setIncludeInactive] = useState(false);
  const { data, isLoading, error } = useSuppliers(includeInactive);
  const setActive = useSetSupplierActive();

  const [editing, setEditing] = useState<Supplier | null>(null);
  const [creating, setCreating] = useState(false);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Suppliers</h1>
          <p className="text-sm text-muted">Where parts are sourced from.</p>
        </div>
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(event) => setIncludeInactive(event.target.checked)}
            />
            Include inactive
          </label>
          {isAdmin && (
            <Button onClick={() => setCreating(true)}>New supplier</Button>
          )}
        </div>
      </div>

      <Card>
        {error && (
          <div className="p-4">
            <Alert>{(error as Error).message}</Alert>
          </div>
        )}

        {isLoading ? (
          <Spinner label="Loading suppliers…" />
        ) : !data || data.length === 0 ? (
          <EmptyState message="No suppliers yet." />
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Name</Th>
                <Th>Contact</Th>
                <Th>Email</Th>
                <Th>Phone</Th>
                <Th className="text-right">Parts</Th>
                {isAdmin && <Th />}
              </tr>
            </thead>
            <tbody>
              {data.map((supplier) => (
                <tr key={supplier.id}>
                  <Td>
                    <span className="font-medium">{supplier.name}</span>
                    {!supplier.isActive && (
                      <span className="ml-2">
                        <Badge>Inactive</Badge>
                      </span>
                    )}
                  </Td>
                  <Td className="text-muted">{supplier.contactName ?? "—"}</Td>
                  <Td className="text-muted">{supplier.email ?? "—"}</Td>
                  <Td className="text-muted">{supplier.phone ?? "—"}</Td>
                  <Td className="text-right tabular-nums">
                    {supplier.partCount}
                  </Td>
                  {isAdmin && (
                    <Td className="text-right">
                      <div className="flex justify-end gap-2">
                        <Button
                          variant="ghost"
                          onClick={() => setEditing(supplier)}
                        >
                          Edit
                        </Button>
                        <Button
                          variant="ghost"
                          disabled={setActive.isPending}
                          onClick={() =>
                            setActive.mutate({
                              id: supplier.id,
                              isActive: !supplier.isActive,
                            })
                          }
                        >
                          {supplier.isActive ? "Deactivate" : "Reactivate"}
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
        <SupplierModal
          supplier={editing}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
        />
      )}
    </div>
  );
}
