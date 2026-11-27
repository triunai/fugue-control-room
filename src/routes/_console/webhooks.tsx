import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Plus, Copy, AlertTriangle, RotateCw } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { PageHeader, LoadingRows, EmptyState, ErrorState, StatusDot } from "@/components/console/states";
import { StaffOnly } from "@/components/console/staff-only";
import { useMock } from "@/lib/console";
import { errorToast } from "@/lib/notify";
import { webhooks as seedW, deliveries as seedD, correlationId, type Webhook, type Delivery } from "@/lib/mock";

export const Route = createFileRoute("/_console/webhooks")({
  head: () => ({ meta: [
    { title: "Webhooks · Fugue Console" },
    { name: "description", content: "Webhook subscriptions, signing secrets and delivery attempts." },
    { property: "og:title", content: "Webhooks · Fugue Console" },
    { property: "og:description", content: "Manage outbound webhooks and redeliver events." },
  ] }),
  component: () => <StaffOnly><Webhooks /></StaffOnly>,
});

const EVENTS = ["customer.created", "customer.updated", "customer.suspended", "customer.deleted"];
const dTone = { delivered: "ok", failed: "bad", retrying: "warn", pending: "idle" } as const;

function Webhooks() {
  const [hooks, setHooks] = useState(seedW);
  const [dels, setDels] = useState(seedD);
  const res = useMock(() => hooks, [] as Webhook[], [hooks]);
  const dres = useMock(() => dels, [] as Delivery[], [dels]);
  const [open, setOpen] = useState(false);
  const [url, setUrl] = useState(""); const [ev, setEv] = useState<string[]>([]);
  const [err, setErr] = useState<{ url?: string; ev?: string }>({});
  const [secret, setSecret] = useState<string | null>(null);

  function create() {
    const e: typeof err = {};
    if (!/^https:\/\/\S+\.\S+/.test(url)) e.url = "Must be an https:// URL.";
    if (!ev.length) e.ev = "Pick at least one event.";
    setErr(e); if (Object.keys(e).length) return;
    const s = "whsec_" + Array.from({ length: 32 }, () => Math.floor(Math.random() * 16).toString(16)).join("");
    setHooks((h) => [{ id: `wh_0${h.length + 1}`, url, events: ev, active: true, secretHint: `whsec_••••${s.slice(-4)}`, createdAt: "2026-11-27" }, ...h]);
    setSecret(s);
  }
  function close() { setOpen(false); setSecret(null); setUrl(""); setEv([]); setErr({}); }
  function redeliver(d: Delivery) {
    if (d.code === 410) return errorToast("Endpoint is gone — subscription must be updated first", { status: 422, correlationId: correlationId() });
    setDels((x) => x.map((y) => y.id === d.id ? { ...y, status: "pending", nextRetry: "now" } : y));
    toast.success(`Redelivery queued for ${d.id}`);
  }

  return (
    <>
      <PageHeader eyebrow="Platform" title="Webhooks" desc="Signed outbound events. Secrets are shown once, at creation." actions={<Button size="sm" onClick={() => setOpen(true)}><Plus className="h-4 w-4" />New subscription</Button>} />
      <h2 className="mb-3 text-2xl">Subscriptions</h2>
      {res.status === "loading" && <LoadingRows rows={3} />}
      {res.status === "error" && <ErrorState onRetry={res.reload} />}
      {res.status === "ok" && !res.data?.length && <EmptyState title="No subscriptions" action={<Button size="sm" onClick={() => setOpen(true)}>Create one</Button>}>Subscribe an endpoint to receive events.</EmptyState>}
      {res.status === "ok" && !!res.data?.length && (
        <div className="rounded-md border border-border"><Table>
          <TableHeader><TableRow><TableHead>Endpoint</TableHead><TableHead>Events</TableHead><TableHead>Secret</TableHead><TableHead>Status</TableHead><TableHead>Created</TableHead></TableRow></TableHeader>
          <TableBody>{res.data.map((w) => (
            <TableRow key={w.id}>
              <TableCell><p className="font-mono text-xs">{w.url}</p><p className="text-[11px] text-muted-foreground">{w.id}</p></TableCell>
              <TableCell className="text-xs text-muted-foreground">{w.events.join(", ")}</TableCell>
              <TableCell className="font-mono text-xs">{w.secretHint}</TableCell>
              <TableCell><span className="inline-flex items-center gap-2 text-sm"><StatusDot tone={w.active ? "ok" : "idle"} />{w.active ? "Active" : "Paused"}</span></TableCell>
              <TableCell className="text-sm text-muted-foreground">{w.createdAt}</TableCell>
            </TableRow>))}</TableBody>
        </Table></div>
      )}

      <h2 className="mb-3 mt-10 text-2xl">Recent deliveries</h2>
      {dres.status === "loading" && <LoadingRows rows={4} />}
      {dres.status === "error" && <ErrorState onRetry={dres.reload} />}
      {dres.status === "ok" && !dres.data?.length && <EmptyState title="No deliveries yet">Deliveries appear once an event fires.</EmptyState>}
      {dres.status === "ok" && !!dres.data?.length && (
        <div className="rounded-md border border-border"><Table>
          <TableHeader><TableRow><TableHead>Delivery</TableHead><TableHead>Event</TableHead><TableHead>Status</TableHead><TableHead>Attempts</TableHead><TableHead>Next retry</TableHead><TableHead>Last code</TableHead><TableHead /></TableRow></TableHeader>
          <TableBody>{dres.data.map((d) => (
            <TableRow key={d.id}>
              <TableCell><p className="font-mono text-xs">{d.id}</p><p className="text-[11px] text-muted-foreground">{d.webhookId} · {d.at}</p></TableCell>
              <TableCell className="font-mono text-xs">{d.event}</TableCell>
              <TableCell><span className="inline-flex items-center gap-2 text-sm capitalize"><StatusDot tone={dTone[d.status]} />{d.status}</span></TableCell>
              <TableCell className="font-mono text-xs">{d.attempts}/8</TableCell>
              <TableCell className="text-sm text-muted-foreground">{d.nextRetry ?? "—"}</TableCell>
              <TableCell className={`font-mono text-xs ${d.code && d.code < 300 ? "text-success" : "text-destructive"}`}>{d.code ?? "timeout"}</TableCell>
              <TableCell className="text-right">{d.status !== "delivered" && <Button size="sm" variant="outline" onClick={() => redeliver(d)}><RotateCw className="h-3.5 w-3.5" />Redeliver</Button>}</TableCell>
            </TableRow>))}</TableBody>
        </Table></div>
      )}

      <Dialog open={open} onOpenChange={(o) => !o && close()}>
        <DialogContent className="max-w-lg">
          {secret ? (<>
            <DialogHeader><DialogTitle className="font-display text-3xl font-medium">Copy your signing secret</DialogTitle>
              <DialogDescription>This is the only time it will be shown.</DialogDescription></DialogHeader>
            <div className="flex items-center gap-2 rounded-sm border border-border bg-muted p-3">
              <code className="flex-1 break-all font-mono text-xs">{secret}</code>
              <Button size="icon" variant="ghost" aria-label="Copy secret" onClick={() => { void navigator.clipboard?.writeText(secret); toast.success("Secret copied"); }}><Copy className="h-4 w-4" /></Button>
            </div>
            <p className="flex gap-2 text-xs text-warning"><AlertTriangle className="h-4 w-4 shrink-0" />Store it in your secrets manager now. You'll need to rotate it if lost.</p>
            <DialogFooter><Button onClick={close}>I've stored it</Button></DialogFooter>
          </>) : (<>
            <DialogHeader><DialogTitle className="font-display text-3xl font-medium">New subscription</DialogTitle>
              <DialogDescription>Events are signed with an HMAC secret.</DialogDescription></DialogHeader>
            <div className="space-y-1.5"><Label htmlFor="url">Endpoint URL</Label>
              <Input id="url" placeholder="https://" value={url} onChange={(e) => setUrl(e.target.value)} aria-invalid={!!err.url} className="aria-[invalid=true]:border-destructive" />
              {err.url && <p className="text-xs text-destructive">{err.url}</p>}</div>
            <div className="space-y-2"><Label>Events</Label>
              {EVENTS.map((e) => (
                <label key={e} className="flex items-center gap-2 font-mono text-xs"><Checkbox checked={ev.includes(e)} onCheckedChange={(c) => setEv((x) => c ? [...x, e] : x.filter((y) => y !== e))} />{e}</label>))}
              {err.ev && <p className="text-xs text-destructive">{err.ev}</p>}</div>
            <DialogFooter><Button variant="ghost" onClick={close}>Cancel</Button><Button onClick={create}>Create</Button></DialogFooter>
          </>)}
        </DialogContent>
      </Dialog>
    </>
  );
}
