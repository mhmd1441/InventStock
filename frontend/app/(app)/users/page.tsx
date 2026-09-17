"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useCreateUser, useSetUserActive, useUsers } from "@/lib/queries";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Modal,
  Select,
  Spinner,
  Table,
  Td,
  Th,
} from "@/components/ui";

const schema = z.object({
  fullName: z.string().min(1, "Name is required").max(150),
  email: z.string().min(1, "Email is required").email("Enter a valid email"),
  password: z
    .string()
    .min(12, "Use at least 12 characters")
    .regex(/[A-Z]/, "Include an uppercase letter")
    .regex(/[a-z]/, "Include a lowercase letter")
    .regex(/\d/, "Include a digit")
    .regex(/[^A-Za-z0-9]/, "Include a symbol"),
  role: z.enum(["Admin", "Staff"]),
});

type FormValues = z.infer<typeof schema>;

function CreateUserModal({ onClose }: { onClose: () => void }) {
  const create = useCreateUser();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { role: "Staff" },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await create.mutateAsync(values);
      onClose();
    } catch (error) {
      setFormError(
        error instanceof ApiError
          ? [error.message, ...error.fieldMessages].join(" ")
          : "Could not create the user.",
      );
    }
  });

  return (
    <Modal title="New user" onClose={onClose}>
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {formError && <Alert>{formError}</Alert>}

        <Field label="Full name" error={errors.fullName?.message}>
          <Input {...register("fullName")} />
        </Field>

        <Field label="Email" error={errors.email?.message}>
          <Input type="email" autoComplete="off" {...register("email")} />
        </Field>

        <Field
          label="Temporary password"
          error={errors.password?.message}
          hint="At least 12 characters with upper, lower, digit and symbol"
        >
          <Input type="password" autoComplete="new-password" {...register("password")} />
        </Field>

        <Field label="Role" error={errors.role?.message}>
          <Select {...register("role")}>
            <option value="Staff">Staff — record stock movements</option>
            <option value="Admin">Admin — full access</option>
          </Select>
        </Field>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Creating…" : "Create user"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

export default function UsersPage() {
  const router = useRouter();
  const { isAdmin, user } = useAuth();
  const { data, isLoading, error } = useUsers();
  const setActive = useSetUserActive();
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    if (!isAdmin) router.replace("/dashboard");
  }, [isAdmin, router]);

  if (!isAdmin) return null;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Users</h1>
          <p className="text-sm text-muted">
            Staff can record stock movements; administrators can also manage the
            catalogue and post adjustments.
          </p>
        </div>
        <Button onClick={() => setCreating(true)}>New user</Button>
      </div>

      <Card>
        {error && (
          <div className="p-4">
            <Alert>{(error as Error).message}</Alert>
          </div>
        )}

        {isLoading ? (
          <Spinner label="Loading users…" />
        ) : !data || data.length === 0 ? (
          <EmptyState message="No users yet." />
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Name</Th>
                <Th>Email</Th>
                <Th>Roles</Th>
                <Th>Status</Th>
                <Th />
              </tr>
            </thead>
            <tbody>
              {data.map((row) => (
                <tr key={row.id}>
                  <Td className="font-medium">{row.fullName}</Td>
                  <Td className="text-muted">{row.email}</Td>
                  <Td>
                    <span className="flex flex-wrap gap-1">
                      {row.roles.map((role) => (
                        <Badge
                          key={role}
                          tone={role === "Admin" ? "accent" : "neutral"}
                        >
                          {role}
                        </Badge>
                      ))}
                    </span>
                  </Td>
                  <Td>
                    {row.isActive ? (
                      <Badge tone="success">Active</Badge>
                    ) : (
                      <Badge tone="danger">Disabled</Badge>
                    )}
                  </Td>
                  <Td className="text-right">
                    <Button
                      variant="ghost"
                      disabled={row.id === user?.id || setActive.isPending}
                      title={
                        row.id === user?.id
                          ? "You cannot disable your own account"
                          : undefined
                      }
                      onClick={() =>
                        setActive.mutate({
                          id: row.id,
                          isActive: !row.isActive,
                        })
                      }
                    >
                      {row.isActive ? "Disable" : "Enable"}
                    </Button>
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>

      {creating && <CreateUserModal onClose={() => setCreating(false)} />}
    </div>
  );
}
