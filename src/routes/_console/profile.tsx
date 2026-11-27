import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHeader } from "@/components/console/states";
import { useConsole } from "@/lib/console";
import { errorToast } from "@/lib/notify";
import { correlationId } from "@/lib/mock";

export const Route = createFileRoute("/_console/profile")({
  head: () => ({ meta: [
    { title: "My profile · Fugue Console" },
    { name: "description", content: "Your Fugue account details, password and sessions." },
    { property: "og:title", content: "My profile · Fugue Console" },
    { property: "og:description", content: "Manage your own Fugue account." },
  ] }),
  component: Profile,
});

function Profile() {
  const { session } = useConsole();
  const [name, setName] = useState(session?.name ?? "");
  const [cur, setCur] = useState(""); const [pw, setPw] = useState("");
  const [pwErr, setPwErr] = useState<{ cur?: string; pw?: string }>({});
  function changePw() {
    const e: typeof pwErr = {};
    if (!cur) e.cur = "Enter your current password.";
    if (pw.length < 12) e.pw = "Use at least 12 characters.";
    setPwErr(e); if (Object.keys(e).length) return;
    if (cur !== "fugue") return errorToast("Current password is incorrect", { status: 403, correlationId: correlationId() });
    setCur(""); setPw(""); toast.success("Password updated");
  }
  return (
    <>
      <PageHeader eyebrow="Account" title="My profile" desc={session?.staff ? "Your personal settings." : "You're signed in with a self-service account. Admin tools aren't available to you."} />
      <div className="grid max-w-4xl grid-cols-2 gap-6">
        <section className="rounded-md border border-border bg-card p-6">
          <h2 className="text-2xl">Details</h2>
          <div className="mt-4 space-y-4">
            <div className="space-y-1.5"><Label htmlFor="pname">Display name</Label><Input id="pname" value={name} onChange={(e) => setName(e.target.value)} /></div>
            <div className="space-y-1.5"><Label>Email</Label><Input value={session?.email ?? ""} disabled /><p className="text-xs text-muted-foreground">Contact an administrator to change your email.</p></div>
            <Button size="sm" onClick={() => toast.success("Profile saved")}>Save</Button>
          </div>
        </section>
        <section className="rounded-md border border-border bg-card p-6">
          <h2 className="text-2xl">Password</h2>
          <div className="mt-4 space-y-4">
            <div className="space-y-1.5"><Label htmlFor="cur">Current password</Label><Input id="cur" type="password" value={cur} onChange={(e) => setCur(e.target.value)} />{pwErr.cur && <p className="text-xs text-destructive">{pwErr.cur}</p>}</div>
            <div className="space-y-1.5"><Label htmlFor="npw">New password</Label><Input id="npw" type="password" value={pw} onChange={(e) => setPw(e.target.value)} />{pwErr.pw && <p className="text-xs text-destructive">{pwErr.pw}</p>}</div>
            <Button size="sm" variant="outline" onClick={changePw}>Update password</Button>
          </div>
        </section>
        <section className="col-span-2 rounded-md border border-border bg-card p-6">
          <h2 className="text-2xl">Active sessions</h2>
          <ul className="mt-3 divide-y divide-border text-sm">
            <li className="flex justify-between py-3"><span>Firefox · Berlin <span className="ml-2 text-xs text-primary">this device</span></span><span className="text-muted-foreground">now</span></li>
            <li className="flex justify-between py-3"><span>Safari iOS · Lisbon</span><Button size="sm" variant="ghost" onClick={() => toast.success("Session revoked")}>Revoke</Button></li>
          </ul>
        </section>
      </div>
    </>
  );
}
