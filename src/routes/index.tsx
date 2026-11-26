import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useState, type FormEvent } from "react";
import { AlertTriangle, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useConsole } from "@/lib/console";
import { correlationId, sleep, users } from "@/lib/mock";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "Sign in · Fugue Console" },
      { name: "description", content: "Sign in to Fugue Console to manage customers, access, modules and webhooks." },
      { property: "og:title", content: "Sign in · Fugue Console" },
      { property: "og:description", content: "Sign in to manage your Fugue API platform." },
    ],
  }),
  component: SignIn,
});

function SignIn() {
  const { signIn, session, ready } = useConsole();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});
  const [formError, setFormError] = useState<{ msg: string; ref: string } | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (ready && session) navigate({ to: session.staff ? "/customers" : "/profile", replace: true });
  }, [ready, session, navigate]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    const fe: typeof errors = {};
    if (!/^\S+@\S+\.\S+$/.test(email)) fe.email = "Enter a valid email address.";
    if (password.length < 1) fe.password = "Password is required.";
    setErrors(fe); setFormError(null);
    if (Object.keys(fe).length) return;
    setBusy(true); await sleep(600); setBusy(false);
    if (email.startsWith("locked")) return setFormError({ msg: "This account is locked after too many attempts. Try again in 15 minutes.", ref: correlationId() });
    const u = users.find((x) => x.email === email.toLowerCase());
    if (!u || password !== "fugue") return setFormError({ msg: "Email or password is incorrect.", ref: correlationId() });
    signIn({ email: u.email, name: u.name, staff: u.staff });
  }

  return (
    <div className="grid min-h-screen grid-cols-[1fr_520px]">
      <section className="relative flex flex-col justify-between border-r border-border p-14">
        <span className="font-display text-2xl tracking-wide"><span className="text-primary">𝔉</span> Fugue Console</span>
        <div>
          <p className="eyebrow">Control plane · v3</p>
          <h1 className="mt-4 max-w-xl text-7xl leading-[0.95]">Every request,<br />in its place.</h1>
          <p className="mt-6 max-w-md text-muted-foreground">Customers, access, modules and webhooks for the Fugue API — audited to the field.</p>
        </div>
        <p className="font-mono text-xs text-muted-foreground">status: all systems nominal</p>
      </section>
      <section className="flex items-center justify-center p-14">
        <form onSubmit={submit} noValidate className="w-full max-w-sm space-y-6">
          <div>
            <h2 className="text-3xl">Sign in</h2>
            <p className="mt-1 text-sm text-muted-foreground">Use your Fugue account.</p>
          </div>
          {formError && (
            <div role="alert" className="rounded-sm border border-primary/60 bg-primary/10 p-3 text-sm">
              <div className="flex gap-2"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />{formError.msg}</div>
              <p className="mt-2 font-mono text-[11px] text-muted-foreground">ref {formError.ref}</p>
            </div>
          )}
          <div className="space-y-2">
            <Label htmlFor="email">Email</Label>
            <Input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} aria-invalid={!!errors.email} className="aria-[invalid=true]:border-destructive" />
            {errors.email && <p className="text-xs text-destructive">{errors.email}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="password">Password</Label>
            <Input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} aria-invalid={!!errors.password} className="aria-[invalid=true]:border-destructive" />
            {errors.password && <p className="text-xs text-destructive">{errors.password}</p>}
          </div>
          <Button type="submit" className="w-full" disabled={busy}>{busy && <Loader2 className="h-4 w-4 animate-spin" />}Sign in</Button>
          <div className="rounded-sm border border-dashed border-border p-3 text-xs text-muted-foreground">
            <p className="eyebrow mb-1">Demo accounts · password "fugue"</p>
            <p>admin@fugue.dev — staff · member@fugue.dev — self-service</p>
            <p>locked@fugue.dev — locked account error</p>
          </div>
        </form>
      </section>
    </div>
  );
}
