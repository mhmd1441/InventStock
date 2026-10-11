"use client";

import { useState } from "react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useAddBrandAlias, useBrands, useCreateBrand, useSetBrandActive } from "@/lib/queries";
import type { ProductBrand } from "@/lib/types";
import { Alert, Badge, Button, Card, EmptyState, Field, Input, Modal, Spinner, Table, Td, Th } from "@/components/ui";

export default function BrandsPage() {
  const { isAdmin } = useAuth();
  const [includeInactive, setIncludeInactive] = useState(false);
  const { data, isLoading, error } = useBrands(includeInactive);
  const create = useCreateBrand();
  const addAlias = useAddBrandAlias();
  const setActive = useSetBrandActive();
  const [name, setName] = useState("");
  const [confirm, setConfirm] = useState(false);
  const [aliasBrand, setAliasBrand] = useState<ProductBrand | null>(null);
  const [alias, setAlias] = useState("");
  const [message, setMessage] = useState<string | null>(null);

  const createBrand = async () => {
    setMessage(null);
    try {
      await create.mutateAsync({ name, confirmPossibleDuplicate: confirm });
      setName(""); setConfirm(false);
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : "Could not create the brand.");
    }
  };

  const saveAlias = async () => {
    if (!aliasBrand) return;
    setMessage(null);
    try {
      await addAlias.mutateAsync({ id: aliasBrand.id, value: alias });
      setAlias(""); setAliasBrand(null);
    } catch (e) {
      setMessage(e instanceof ApiError ? e.message : "Could not add the alias.");
    }
  };

  return <div className="space-y-6">
    <div>
      <h1 className="text-xl font-semibold tracking-tight">Brands</h1>
      <p className="text-sm text-muted">One canonical name per manufacturer, with known spellings stored as aliases.</p>
    </div>
    {message && <Alert>{message}</Alert>}
    {isAdmin && <Card className="p-4"><div className="flex flex-wrap items-end gap-3">
      <Field label="New canonical brand" hint="Possible duplicates require explicit confirmation">
        <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="FEBI Bilstein" />
      </Field>
      <label className="flex items-center gap-2 pb-2 text-sm"><input type="checkbox" checked={confirm} onChange={(e) => setConfirm(e.target.checked)} />Confirmed genuinely different</label>
      <Button disabled={!name.trim() || create.isPending} onClick={() => void createBrand()}>Add brand</Button>
      <label className="ml-auto flex items-center gap-2 pb-2 text-sm"><input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} />Show inactive</label>
    </div></Card>}
    <Card>{error ? <div className="p-4"><Alert>{(error as Error).message}</Alert></div> : isLoading ? <Spinner label="Loading brands…" /> : !data?.length ? <EmptyState message="No brands yet." /> :
      <Table><thead><tr><Th>Brand</Th><Th>Aliases</Th><Th>Status</Th>{isAdmin && <Th />}</tr></thead><tbody>{data.map((brand) => <tr key={brand.id}>
        <Td className="font-medium">{brand.name}</Td><Td className="text-muted">{brand.aliases.join(", ") || "—"}</Td><Td><Badge tone={brand.isActive ? "success" : "neutral"}>{brand.isActive ? "Active" : "Inactive"}</Badge></Td>
        {isAdmin && <Td className="text-right"><div className="flex justify-end gap-2"><Button variant="ghost" onClick={() => setAliasBrand(brand)}>Add alias</Button><Button variant="ghost" onClick={() => void setActive.mutateAsync({ id: brand.id, isActive: !brand.isActive })}>{brand.isActive ? "Deactivate" : "Reactivate"}</Button></div></Td>}
      </tr>)}</tbody></Table>}
    </Card>
    {aliasBrand && <Modal title={`Add alias for ${aliasBrand.name}`} onClose={() => setAliasBrand(null)}><div className="space-y-4"><Field label="Alias" hint="Example: FEBI for canonical FEBI Bilstein"><Input value={alias} onChange={(e) => setAlias(e.target.value)} /></Field><div className="flex justify-end gap-2"><Button variant="secondary" onClick={() => setAliasBrand(null)}>Cancel</Button><Button disabled={!alias.trim() || addAlias.isPending} onClick={() => void saveAlias()}>Save alias</Button></div></div></Modal>}
  </div>;
}
