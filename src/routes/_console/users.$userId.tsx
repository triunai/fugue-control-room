import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowLeft } from "lucide-react";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { NotFoundState, EmptyState } from "@/components/console/states";
import { StaffOnly } from "@/components/console/staff-only";
import { users, roles, groups, type Override } from "@/lib/mock";

export const Route = createFileRoute("/_console/users/$userId")({
  head: () => ({ meta: [
    { title: "User detail · Fugue Console" },
    { name: "description", content: "Roles, groups and permission overrides for a user." },
    { property: "og:title", content: "User detail · Fugue Console" },
    { property: "og:description", content: "Inspect a user's effective access." },
  ] }),
  component: () => <StaffOnly><UserDetail /></StaffOnly>,
});

const TODAY = "2026-11-26";

function UserDetail() {
  const { userId } = Route.useParams();
  const u = users.find((x) => x.id === userId);
  if (!u) return <NotFoundState />;
  const perms = [...new Set(u.overrides.map((o) => o.perm))];
  return (
    <>
      <Link to="/users" className="mb-6 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"><ArrowLeft className="h-4 w-4" />Users & access</Link>
      <div className="mb-8 border-b border-border pb-6">
        <p className="eyebrow">{u.id} · {u.staff ? "staff" : "member"}</p>
        <h1 className="mt-1 text-4xl">{u.name}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{u.email}</p>
      </div>
      <Tabs defaultValue="overrides">
        <TabsList className="bg-muted">
          <TabsTrigger value="roles">Roles ({u.roles.length})</TabsTrigger>
          <TabsTrigger value="groups">Groups ({u.groups.length})</TabsTrigger>
          <TabsTrigger value="overrides">Overrides ({u.overrides.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="roles" className="mt-6">
          {u.roles.length === 0 ? <EmptyState title="No roles">This user has no role assignments.</EmptyState> : (
            <div className="grid grid-cols-2 gap-4">
              {u.roles.map((id) => { const r = roles.find((x) => x.id === id)!; return (
                <div key={id} className="rounded-md border border-border bg-card p-5">
                  <h3 className="text-2xl">{r.name}</h3>
                  <div className="mt-3 flex flex-wrap gap-1.5">{r.perms.map((p) => <span key={p} className="rounded-sm border border-border px-1.5 py-0.5 font-mono text-[11px] text-muted-foreground">{p}</span>)}</div>
                </div>); })}
            </div>
          )}
        </TabsContent>
        <TabsContent value="groups" className="mt-6">
          {u.groups.length === 0 ? <EmptyState title="No groups">This user isn't in any group.</EmptyState> : (
            <div className="divide-y divide-border rounded-md border border-border">
              {u.groups.map((id) => { const g = groups.find((x) => x.id === id)!; return (
                <div key={id} className="flex items-center justify-between px-5 py-4"><span>{g.name}</span><span className="text-sm text-muted-foreground">{g.members} members</span></div>); })}
            </div>
          )}
        </TabsContent>
        <TabsContent value="overrides" className="mt-6">
          {u.overrides.length === 0 ? <EmptyState title="No overrides">Access comes only from roles and groups.</EmptyState> : (
            <div className="space-y-3">
              <p className="text-sm text-muted-foreground">When an allow and a deny cover the same permission, <span className="text-foreground">deny wins</span>.</p>
              {perms.map((p) => {
                const os = u.overrides.filter((o) => o.perm === p);
                const live = os.filter((o) => !o.expires || o.expires >= TODAY);
                const effective = live.some((o) => o.effect === "deny") ? "deny" : live.length ? "allow" : "none";
                return (
                  <div key={p} className="rounded-md border border-border bg-card p-5">
                    <div className="flex items-center justify-between">
                      <span className="font-mono text-sm">{p}</span>
                      <span className="text-xs text-muted-foreground">Effective: <span className={effective === "deny" ? "font-medium text-destructive" : "font-medium text-success"}>{effective}</span></span>
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {os.map((o, i) => <OverrideChip key={i} o={o} loses={effective === "deny" && o.effect === "allow"} />)}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </>
  );
}

function OverrideChip({ o, loses }: { o: Override; loses: boolean }) {
  const deny = o.effect === "deny";
  return (
    <div title={o.reason} className={`inline-flex items-center gap-2 rounded-sm border px-2.5 py-1 text-xs ${deny ? "border-primary bg-primary/15 text-foreground" : "border-success/50 text-success"} ${loses ? "line-through opacity-50" : ""}`}>
      <span className="font-medium uppercase tracking-wider">{o.effect}</span>
      <span className="text-muted-foreground">{o.expires ? `until ${o.expires}` : "no expiry"}</span>
      {deny && !loses && <span className="rounded-sm bg-primary px-1 text-[10px] uppercase text-primary-foreground">wins</span>}
      <span className="text-muted-foreground">· {o.reason}</span>
    </div>
  );
}
