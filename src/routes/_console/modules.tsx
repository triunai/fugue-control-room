import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import { Switch } from "@/components/ui/switch";
import { PageHeader, LoadingRows, EmptyState, ErrorState, StatusDot } from "@/components/console/states";
import { StaffOnly } from "@/components/console/staff-only";
import { useMock } from "@/lib/console";
import { modules as seed, type Module } from "@/lib/mock";

export const Route = createFileRoute("/_console/modules")({
  head: () => ({ meta: [
    { title: "Modules · Fugue Console" },
    { name: "description", content: "Enable or disable platform modules and see why they're off." },
    { property: "og:title", content: "Modules · Fugue Console" },
    { property: "og:description", content: "Config, database and effective module state." },
  ] }),
  component: () => <StaffOnly><Modules /></StaffOnly>,
});

function evaluate(m: Module, all: Module[]): { on: boolean; why?: string } {
  if (!m.config) return { on: false, why: `Disabled by deployment config — ${m.configNote ?? "config flag is off"}. Can't be changed here.` };
  if (!m.db) return { on: false, why: "Turned off by an administrator in the console." };
  if (m.requires) {
    const dep = all.find((x) => x.key === m.requires);
    if (dep && !evaluate(dep, all).on) return { on: false, why: `Requires the "${dep.name}" module, which is off.` };
  }
  return { on: true };
}

function Modules() {
  const [mods, setMods] = useState(seed);
  const res = useMock(() => mods, [] as Module[], [mods]);
  return (
    <>
      <PageHeader eyebrow="Platform" title="Modules" desc="A module runs only when its deployment config AND its console setting are both on, and its dependencies are running." />
      {res.status === "loading" && <LoadingRows rows={4} />}
      {res.status === "error" && <ErrorState onRetry={res.reload} />}
      {res.status === "ok" && !res.data?.length && <EmptyState title="No modules installed">Modules appear here when they're deployed.</EmptyState>}
      {res.status === "ok" && !!res.data?.length && (
        <div className="grid grid-cols-2 gap-4 xl:grid-cols-3">
          {res.data.map((m) => {
            const eff = evaluate(m, res.data!);
            return (
              <div key={m.key} className={`flex flex-col rounded-md border bg-card p-5 ${eff.on ? "border-border" : "border-border/60"}`}>
                <div className="flex items-start justify-between gap-3">
                  <div><h3 className="text-2xl">{m.name}</h3><p className="mt-1 text-sm text-muted-foreground">{m.desc}</p></div>
                  <Switch checked={m.db} disabled={!m.config} aria-label={`Toggle ${m.name}`}
                    onCheckedChange={(v) => { setMods((all) => all.map((x) => x.key === m.key ? { ...x, db: v } : x)); toast.success(`${m.name} ${v ? "enabled" : "disabled"}`); }} />
                </div>
                <dl className="mt-5 grid grid-cols-3 gap-2 border-t border-border pt-4 text-xs">
                  <State label="Config" on={m.config} />
                  <State label="DB" on={m.db} />
                  <State label="Effective" on={eff.on} strong />
                </dl>
                {eff.why && <p className="mt-4 border-l-2 border-primary pl-3 text-xs text-muted-foreground">{eff.why}</p>}
              </div>
            );
          })}
        </div>
      )}
    </>
  );
}

function State({ label, on, strong }: { label: string; on: boolean; strong?: boolean }) {
  return (
    <div>
      <dt className="eyebrow">{label}</dt>
      <dd className={`mt-1 inline-flex items-center gap-1.5 ${strong ? "font-medium" : ""}`}><StatusDot tone={on ? "ok" : "bad"} />{on ? "On" : "Off"}</dd>
    </div>
  );
}
