import { createFileRoute } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader, LoadingRows, EmptyState, ErrorState } from "@/components/console/states";
import { StaffOnly } from "@/components/console/staff-only";
import { useMock } from "@/lib/console";
import { audit, type AuditEntry } from "@/lib/mock";

export const Route = createFileRoute("/_console/audit")({
  head: () => ({ meta: [
    { title: "Audit log · Fugue Console" },
    { name: "description", content: "Every change, who made it and the field-level diff." },
    { property: "og:title", content: "Audit log · Fugue Console" },
    { property: "og:description", content: "Field-level audit trail for the Fugue platform." },
  ] }),
  component: () => <StaffOnly><Audit /></StaffOnly>,
});

function Audit() {
  const [f, setF] = useState({ entity: "all", actor: "", action: "all", corr: "", from: "", to: "" });
  const [sel, setSel] = useState<string | null>(null);
  const list = useMemo(() => audit.filter((a) =>
    (f.entity === "all" || a.entity === f.entity) && (f.action === "all" || a.action === f.action) &&
    a.actor.includes(f.actor.toLowerCase()) && a.correlationId.includes(f.corr.trim()) &&
    (!f.from || a.at.slice(0, 10) >= f.from) && (!f.to || a.at.slice(0, 10) <= f.to)), [f]);
  const res = useMock(() => list, [] as AuditEntry[], [list]);
  const current = res.data?.find((a) => a.id === sel) ?? res.data?.[0];
  return (
    <>
      <PageHeader eyebrow="Compliance" title="Audit log" desc="Immutable record of changes. Secrets are masked." />
      <div className="mb-6 grid grid-cols-6 gap-2">
        <Select value={f.entity} onValueChange={(v) => setF({ ...f, entity: v })}><SelectTrigger aria-label="Entity"><SelectValue /></SelectTrigger>
          <SelectContent>{["all","customer","user","module","webhook","session"].map((e) => <SelectItem key={e} value={e}>{e === "all" ? "All entities" : e}</SelectItem>)}</SelectContent></Select>
        <Input placeholder="Actor" value={f.actor} onChange={(e) => setF({ ...f, actor: e.target.value })} />
        <Select value={f.action} onValueChange={(v) => setF({ ...f, action: v })}><SelectTrigger aria-label="Action"><SelectValue /></SelectTrigger>
          <SelectContent>{["all","create","update","delete","suspend","login"].map((e) => <SelectItem key={e} value={e}>{e === "all" ? "All actions" : e}</SelectItem>)}</SelectContent></Select>
        <Input placeholder="Correlation id" className="font-mono text-xs" value={f.corr} onChange={(e) => setF({ ...f, corr: e.target.value })} />
        <Input type="date" aria-label="From" value={f.from} onChange={(e) => setF({ ...f, from: e.target.value })} />
        <Input type="date" aria-label="To" value={f.to} onChange={(e) => setF({ ...f, to: e.target.value })} />
      </div>
      {res.status === "loading" && <LoadingRows />}
      {res.status === "error" && <ErrorState onRetry={res.reload} />}
      {res.status === "ok" && !res.data?.length && <EmptyState title="No events">No audit events match these filters.</EmptyState>}
      {res.status === "ok" && !!res.data?.length && current && (
        <div className="grid grid-cols-[1fr_1.1fr] gap-6">
          <ol className="relative border-l border-border pl-6">
            {res.data.map((a) => (
              <li key={a.id} className="mb-2">
                <span className={`absolute -left-[5px] mt-4 h-2.5 w-2.5 rounded-full border ${a.id === current.id ? "border-primary bg-primary" : "border-border bg-background"}`} />
                <button onClick={() => setSel(a.id)} className={`w-full rounded-sm border px-4 py-3 text-left transition-colors ${a.id === current.id ? "border-primary/60 bg-card" : "border-transparent hover:bg-card"}`}>
                  <p className="text-sm"><span className="text-muted-foreground">{a.actor}</span> <span className="font-medium">{a.action}</span> {a.entity} <span className="font-mono text-xs">{a.entityId}</span></p>
                  <p className="mt-1 font-mono text-[11px] text-muted-foreground">{a.at.replace("T", " ").slice(0, 19)} · {a.correlationId}</p>
                </button>
              </li>
            ))}
          </ol>
          <aside className="sticky top-20 h-fit rounded-md border border-border bg-card p-6">
            <p className="eyebrow">{current.id}</p>
            <h2 className="mt-1 text-3xl capitalize">{current.action} {current.entity}</h2>
            <dl className="mt-4 grid grid-cols-2 gap-3 text-sm">
              <div><dt className="eyebrow">Actor</dt><dd>{current.actor}</dd></div>
              <div><dt className="eyebrow">Correlation</dt><dd className="font-mono text-xs">{current.correlationId}</dd></div>
            </dl>
            <h3 className="mt-6 text-xl">Changes</h3>
            {current.diff.length === 0 ? <p className="mt-2 text-sm text-muted-foreground">No field changes recorded.</p> : (
              <table className="mt-3 w-full text-sm">
                <thead><tr className="eyebrow text-left"><th className="pb-2 font-normal">Field</th><th className="pb-2 font-normal">Before</th><th className="pb-2 font-normal">After</th></tr></thead>
                <tbody>{current.diff.map((d) => (
                  <tr key={d.field} className="border-t border-border align-top">
                    <td className="py-2 pr-3 font-mono text-xs">{d.field}</td>
                    <td className="py-2 pr-3"><span className="text-destructive line-through decoration-destructive/50">{d.masked ? "•••" : d.before ?? <span className="text-muted-foreground no-underline">∅</span>}</span></td>
                    <td className="py-2 text-success">{d.masked ? "•••" : d.after ?? <span className="text-muted-foreground">∅</span>}</td>
                  </tr>))}</tbody>
              </table>
            )}
          </aside>
        </div>
      )}
    </>
  );
}
