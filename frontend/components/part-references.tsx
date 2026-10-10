"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { Alert, Button, Card, Field, Input, Select } from "./ui";

const kinds = ["OE", "MercedesOE", "WVA", "Manufacturer", "Other"] as const;
type Kind = (typeof kinds)[number];
interface Reference { id: string; kind: Kind; issuer: string; value: string; isActive: boolean; revision: number }
interface History { revision: number; isActive: boolean; reason: string; actorId: string; occurredAtUtc: string }

export function PartReferences({ partId }: { partId: string }) {
  const { isAdmin } = useAuth();
  const client = useQueryClient();
  const [kind, setKind] = useState<Kind>("OE");
  const [issuer, setIssuer] = useState("");
  const [value, setValue] = useState("");
  const [selected, setSelected] = useState<Reference | null>(null);
  const [reason, setReason] = useState("");
  const path = `/api/parts/${partId}/references`;
  const references = useQuery({
    queryKey: ["part", partId, "references", isAdmin],
    queryFn: ({ signal }) => api.get<Reference[]>(isAdmin ? `${path}/all` : path, undefined, signal),
  });
  const history = useQuery({
    queryKey: ["part", partId, "reference-history", selected?.id],
    enabled: isAdmin && !!selected,
    queryFn: ({ signal }) => api.get<History[]>(`${path}/${selected!.id}/history`, undefined, signal),
  });
  const change = useMutation({
    mutationFn: () => api.patch<void>(`${path}/${selected!.id}/state`, {
      isActive: !selected!.isActive, reason, expectedRevision: selected!.revision,
    }),
    onSuccess: async () => {
      setSelected(null); setReason("");
      await Promise.all([client.invalidateQueries({ queryKey: ["part", partId] }), client.invalidateQueries({ queryKey: ["parts"] })]);
    },
  });
  const add = useMutation({
    mutationFn: () => api.post<Reference>(path, { kind, issuer, value }),
    onSuccess: async () => {
      setValue("");
      await Promise.all([
        client.invalidateQueries({ queryKey: ["part", partId] }),
        client.invalidateQueries({ queryKey: ["parts"] }),
      ]);
    },
  });
  return <Card title="Search references">
    <div className="space-y-4 p-5">
      <p className="text-sm text-muted">These numbers help find this part. A shared number is not proof of compatibility.</p>
      {references.error && <Alert>{references.error.message}</Alert>}
      {references.isLoading ? <p>Loading references…</p> : <ul className="space-y-2">
        {references.data?.map(reference => <li key={reference.id} className="text-sm">
          <span className="font-mono font-semibold">{reference.value}</span>{" — "}{reference.kind}{reference.issuer && ` · ${reference.issuer}`}
          {reference.kind === "MercedesOE" && <span className="text-muted"> (searchable with or without A)</span>}
          {!reference.isActive && <span className="ml-2 text-muted">Inactive link — excluded from search</span>}
          {isAdmin && <Button type="button" variant="secondary" onClick={() => { setSelected(reference); setReason(""); change.reset(); }}>
            {reference.isActive ? "Review / unlink" : "Review / restore"}
          </Button>}
        </li>)}
        {references.data?.length === 0 && <li className="text-sm text-muted">No additional references yet.</li>}
      </ul>}
      {isAdmin && selected && <form className="space-y-3 rounded border border-border p-3" onSubmit={event => { event.preventDefault(); change.mutate(); }}>
        <p>{selected.isActive ? "Deactivate" : "Restore"} the link to {selected.value}. Other products are not affected.</p>
        {history.error && <Alert>{history.error.message}</Alert>}
        {history.data?.length === 0 && <p className="text-xs text-muted">Legacy link: no correction events recorded yet.</p>}
        {history.data?.map(item => <p key={item.revision} className="text-xs text-muted">
          {new Date(item.occurredAtUtc).toLocaleString()} · {item.isActive ? "Active" : "Inactive"} · {item.reason} · User {item.actorId}
        </p>)}
        <Field label="Reason (required)"><Input required maxLength={500} value={reason} onChange={event => setReason(event.target.value)} /></Field>
        {change.error && <Alert>{change.error.message}</Alert>}
        <Button type="submit" disabled={change.isPending || !reason.trim()}>{change.isPending ? "Saving…" : "Confirm correction"}</Button>
        <Button type="button" variant="secondary" onClick={() => setSelected(null)} disabled={change.isPending}>Cancel</Button>
      </form>}
      {isAdmin && <form className="space-y-3" onSubmit={event => { event.preventDefault(); add.mutate(); }}>
        {add.error && <Alert>{add.error.message}</Alert>}
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="Number system"><Select value={kind} onChange={event => setKind(event.target.value as Kind)}>
            {kinds.map(item => <option key={item}>{item}</option>)}
          </Select></Field>
          <Field label="Issuing manufacturer (optional)"><Input maxLength={80} value={issuer} onChange={event => setIssuer(event.target.value)} /></Field>
          <Field label="Reference number"><Input required maxLength={64} value={value} onChange={event => setValue(event.target.value)} /></Field>
        </div>
        <p className="text-xs text-muted">Public catalogue numbers only. Do not enter confidential supplier codes here.</p>
        {kind === "OE" && /^A[\d -]+$/i.test(value.trim()) && <p className="text-sm text-muted">If this is a Mercedes number, choose MercedesOE to enable its validated optional-A rule. Other OE prefixes are preserved.</p>}
        <Button disabled={add.isPending || !value.trim()} type="submit">{add.isPending ? "Adding…" : "Add reference"}</Button>
      </form>}
    </div>
  </Card>;
}
