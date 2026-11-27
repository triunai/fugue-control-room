import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { MoreHorizontal, Search, AlertTriangle, Loader2, Plus } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { PageHeader, LoadingRows, EmptyState, ErrorState, StatusDot } from "@/components/console/states";
import { StaffOnly } from "@/components/console/staff-only";
import { useMock } from "@/lib/console";
import { errorToast } from "@/lib/notify";
import { customers as seed, correlationId, sleep, type Customer } from "@/lib/mock";

export const Route = createFileRoute("/_console/customers")({
  head: () => ({ meta: [
    { title: "Customers · Fugue Console" },
    { name: "description", content: "Search, edit, suspend and remove customer records." },
    { property: "og:title", content: "Customers · Fugue Console" },
    { property: "og:description", content: "Manage customer records on the Fugue API platform." },
  ] }),
  component: () => <StaffOnly><Customers /></StaffOnly>,
});

const PAGE = 12;
const tone = { active: "ok", suspended: "bad", pending: "warn" } as const;

function Customers() {
  const [db, setDb] = useState<Customer[]>(() => seed.map((c) => ({ ...c })));
  const [q, setQ] = useState("");
  const [status, setStatus] = useState("all");
  const [cursor, setCursor] = useState<string | null>(null);
  const [more, setMore] = useState(false);
  const filtered = useMemo(() => db.filter((c) =>
    (status === "all" || c.status === status) &&
    (!q || `${c.name} ${c.email} ${c.id}`.toLowerCase().includes(q.toLowerCase()))), [db, q, status]);
  const res = useMock(() => filtered.slice(0, PAGE), [] as Customer[], [q, status]);
  const [rows, setRows] = useState<Customer[]>([]);
  useEffect(() => { if (res.data) { setRows(res.data); setCursor(res.data.at(-1)?.id ?? null); } }, [res.data]);
  // keep visible rows in sync with edits
  useEffect(() => { setRows((r) => r.map((x) => db.find((d) => d.id === x.id)).filter((x): x is Customer => !!x)); }, [db]);
  const hasMore = cursor !== null && filtered.findIndex((c) => c.id === cursor) < filtered.length - 1;

  async function loadMore() {
    setMore(true); await sleep(500);
    const i = filtered.findIndex((c) => c.id === cursor);
    const next = filtered.slice(i + 1, i + 1 + PAGE);
    setRows((r) => [...r, ...next]); setCursor(next.at(-1)?.id ?? null); setMore(false);
  }

  const [editing, setEditing] = useState<Customer | null>(null);
  const [deleting, setDeleting] = useState<Customer | null>(null);

  function toggleSuspend(c: Customer) {
    const next = c.status === "suspended" ? "active" : "suspended";
    setDb((d) => d.map((x) => x.id === c.id ? { ...x, status: next, version: x.version + 1 } : x));
    toast.success(next === "suspended" ? `${c.name} suspended` : `${c.name} reactivated`);
  }
  async function confirmDelete() {
    const c = deleting!; setDeleting(null);
    await sleep(300);
    if (c.plan === "enterprise") return errorToast("Customer has an active enterprise contract", { status: 409, correlationId: correlationId(), description: "End the contract in Billing before deleting." });
    setDb((d) => d.filter((x) => x.id !== c.id));
    toast.success(`${c.name} deleted`);
  }

  return (
    <>
      <PageHeader eyebrow="Commerce" title="Customers" desc="Accounts that consume the Fugue API." actions={<Button size="sm"><Plus className="h-4 w-4" />New customer</Button>} />
      <div className="mb-4 flex items-center gap-3">
        <div className="relative w-80">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input placeholder="Search name, email or ID" value={q} onChange={(e) => setQ(e.target.value)} className="pl-8" />
        </div>
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="active">Active</SelectItem>
            <SelectItem value="suspended">Suspended</SelectItem>
            <SelectItem value="pending">Pending</SelectItem>
          </SelectContent>
        </Select>
        <span className="ml-auto font-mono text-xs text-muted-foreground">{rows.length} shown · keyset after {cursor ?? "—"}</span>
      </div>

      {res.status === "loading" && <LoadingRows />}
      {res.status === "error" && <ErrorState onRetry={res.reload} />}
      {res.status === "ok" && rows.length === 0 && (
        <EmptyState title={q || status !== "all" ? "No matches" : "No customers yet"}>
          {q || status !== "all" ? "Nothing matches these filters. Clear them to see everyone." : "Customers appear here once they sign up through the API."}
        </EmptyState>
      )}
      {res.status === "ok" && rows.length > 0 && (
        <div className="rounded-md border border-border">
          <Table>
            <TableHeader><TableRow>
              <TableHead>Customer</TableHead><TableHead>ID</TableHead><TableHead>Plan</TableHead><TableHead>Status</TableHead><TableHead>Created</TableHead><TableHead className="w-10" />
            </TableRow></TableHeader>
            <TableBody>
              {rows.map((c) => (
                <TableRow key={c.id}>
                  <TableCell><p className="font-medium">{c.name}</p><p className="text-xs text-muted-foreground">{c.email}</p></TableCell>
                  <TableCell className="font-mono text-xs text-muted-foreground">{c.id}</TableCell>
                  <TableCell className="capitalize">{c.plan}</TableCell>
                  <TableCell><span className="inline-flex items-center gap-2 text-sm capitalize"><StatusDot tone={tone[c.status]} />{c.status}</span></TableCell>
                  <TableCell className="text-sm text-muted-foreground">{c.createdAt.slice(0, 10)}</TableCell>
                  <TableCell>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild><Button variant="ghost" size="icon" aria-label={`Actions for ${c.name}`}><MoreHorizontal className="h-4 w-4" /></Button></DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => setEditing(c)}>Edit</DropdownMenuItem>
                        <DropdownMenuItem onClick={() => toggleSuspend(c)}>{c.status === "suspended" ? "Reactivate" : "Suspend"}</DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem className="text-destructive focus:text-destructive" onClick={() => setDeleting(c)}>Delete…</DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <div className="flex justify-center border-t border-border p-3">
            {hasMore ? <Button variant="outline" size="sm" onClick={loadMore} disabled={more}>{more && <Loader2 className="h-4 w-4 animate-spin" />}Load more</Button>
              : <span className="eyebrow">End of results</span>}
          </div>
        </div>
      )}

      <EditDrawer customer={editing} onClose={() => setEditing(null)} db={db} setDb={setDb} />

      <AlertDialog open={!!deleting} onOpenChange={(o) => !o && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="font-display text-2xl">Delete {deleting?.name}?</AlertDialogTitle>
            <AlertDialogDescription>This removes the customer and revokes their API keys. It can't be undone. Enterprise customers can't be deleted while a contract is active.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90" onClick={confirmDelete}>Delete customer</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

function EditDrawer({ customer, onClose, db, setDb }: { customer: Customer | null; onClose: () => void; db: Customer[]; setDb: (f: (d: Customer[]) => Customer[]) => void }) {
  const [form, setForm] = useState({ name: "", email: "", plan: "free" as Customer["plan"] });
  const [version, setVersion] = useState(0);
  const [errors, setErrors] = useState<{ name?: string; email?: string }>({});
  const [conflict, setConflict] = useState(false);
  const [saving, setSaving] = useState(false);
  const [simulate, setSimulate] = useState(true);

  useEffect(() => {
    if (customer) { setForm({ name: customer.name, email: customer.email, plan: customer.plan }); setVersion(customer.version); setErrors({}); setConflict(false); setSimulate(true); }
  }, [customer]);

  async function save(force = false) {
    if (!customer) return;
    const fe: { name?: string; email?: string } = {};
    if (!form.name.trim()) fe.name = "Name is required.";
    if (!/^\S+@\S+\.\S+$/.test(form.email)) fe.email = "Enter a valid email address.";
    setErrors(fe); if (Object.keys(fe).length) return;
    setSaving(true); await sleep(500); setSaving(false);
    if (form.email.endsWith("@taken.com")) return setErrors({ email: "Another customer already uses this email (422)." });
    // Simulate a concurrent edit the first time: server version moved on → 412 Precondition Failed
    if (simulate && !force) {
      setDb((d) => d.map((x) => x.id === customer.id ? { ...x, plan: x.plan === "team" ? "enterprise" : "team", version: x.version + 1 } : x));
      setSimulate(false); setConflict(true); return;
    }
    const current = db.find((x) => x.id === customer.id);
    if (!force && current && current.version !== version) return setConflict(true);
    setDb((d) => d.map((x) => x.id === customer.id ? { ...x, ...form, version: x.version + 1 } : x));
    toast.success("Customer saved"); onClose();
  }
  function reload() {
    const current = db.find((x) => x.id === customer?.id);
    if (current) { setForm({ name: current.name, email: current.email, plan: current.plan }); setVersion(current.version); }
    setConflict(false); toast("Loaded latest version");
  }

  return (
    <Sheet open={!!customer} onOpenChange={(o) => !o && onClose()}>
      <SheetContent className="w-[480px] sm:max-w-[480px]">
        <SheetHeader>
          <p className="eyebrow">{customer?.id} · v{version}</p>
          <SheetTitle className="font-display text-3xl font-medium">Edit customer</SheetTitle>
          <SheetDescription>Changes are written with If-Match: v{version}.</SheetDescription>
        </SheetHeader>
        {conflict && (
          <div role="alert" className="mt-6 rounded-sm border border-warning/60 bg-warning/10 p-4 text-sm">
            <div className="flex gap-2"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-warning" />
              <div><p className="font-medium">This record changed since you opened it <span className="font-mono text-xs text-muted-foreground">(412)</span></p>
                <p className="mt-1 text-muted-foreground">Someone else saved a newer version. Reload to see their changes, or overwrite them with yours.</p></div>
            </div>
            <div className="mt-3 flex gap-2 pl-6">
              <Button size="sm" variant="outline" onClick={reload}>Reload</Button>
              <Button size="sm" variant="destructive" onClick={() => save(true)}>Overwrite</Button>
            </div>
          </div>
        )}
        <div className="mt-6 space-y-5">
          <Field label="Name" id="name" error={errors.name}><Input id="name" value={form.name} aria-invalid={!!errors.name} className="aria-[invalid=true]:border-destructive" onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
          <Field label="Email" id="email" error={errors.email} hint="Tip: an address ending @taken.com triggers a server-side error."><Input id="email" value={form.email} aria-invalid={!!errors.email} className="aria-[invalid=true]:border-destructive" onChange={(e) => setForm({ ...form, email: e.target.value })} /></Field>
          <Field label="Plan" id="plan">
            <Select value={form.plan} onValueChange={(v) => setForm({ ...form, plan: v as Customer["plan"] })}>
              <SelectTrigger id="plan"><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="free">Free</SelectItem><SelectItem value="team">Team</SelectItem><SelectItem value="enterprise">Enterprise</SelectItem></SelectContent>
            </Select>
          </Field>
        </div>
        <div className="mt-8 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={() => save()} disabled={saving || conflict}>{saving && <Loader2 className="h-4 w-4 animate-spin" />}Save changes</Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}

function Field({ label, id, error, hint, children }: { label: string; id: string; error?: string | undefined; hint?: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>{children}
      {error ? <p className="text-xs text-destructive">{error}</p> : hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
    </div>
  );
}
