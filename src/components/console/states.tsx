import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";

export function PageHeader({ eyebrow, title, desc, actions }: { eyebrow: string; title: string; desc?: string; actions?: ReactNode }) {
  return (
    <div className="mb-8 flex items-end justify-between gap-6 border-b border-border pb-6">
      <div>
        <p className="eyebrow">{eyebrow}</p>
        <h1 className="mt-1 text-4xl text-foreground">{title}</h1>
        {desc && <p className="mt-2 max-w-xl text-sm text-muted-foreground">{desc}</p>}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}

export function LoadingRows({ rows = 6 }: { rows?: number }) {
  return (
    <div className="space-y-2" aria-busy="true" aria-label="Loading">
      {Array.from({ length: rows }, (_, i) => <Skeleton key={i} className="h-11 w-full bg-muted" />)}
    </div>
  );
}

function Frame({ mark, title, children }: { mark: string; title: string; children: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center rounded-md border border-dashed border-border px-6 py-16 text-center">
      <span className="font-display text-5xl text-primary">{mark}</span>
      <h3 className="mt-3 text-2xl">{title}</h3>
      <div className="mt-2 max-w-sm text-sm text-muted-foreground">{children}</div>
    </div>
  );
}

export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return <Frame mark="∅" title={title}>{children}{action && <div className="mt-5">{action}</div>}</Frame>;
}

export function ErrorState({ onRetry, correlationId = "5e1d-0a77-c3f2" }: { onRetry: () => void; correlationId?: string }) {
  return (
    <Frame mark="†" title="This didn't load">
      The server returned 503. Try again, or quote the reference below to support.
      <p className="mt-3 font-mono text-xs">ref {correlationId}</p>
      <Button variant="outline" size="sm" className="mt-5" onClick={onRetry}>Try again</Button>
    </Frame>
  );
}

/** Permission-denied is rendered as "not found" so we never confirm a resource exists. */
export function NotFoundState() {
  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center text-center">
      <p className="font-display text-8xl text-primary">404</p>
      <h2 className="mt-2 text-3xl">Nothing here</h2>
      <p className="mt-2 max-w-sm text-sm text-muted-foreground">This page doesn't exist, or you don't have access to it.</p>
      <Button asChild variant="outline" className="mt-6"><Link to="/profile">Back to my profile</Link></Button>
    </div>
  );
}

export function StatusDot({ tone }: { tone: "ok" | "warn" | "bad" | "idle" }) {
  const c = { ok: "bg-success", warn: "bg-warning", bad: "bg-destructive", idle: "bg-muted-foreground" }[tone];
  return <span className={`inline-block h-1.5 w-1.5 rounded-full ${c}`} />;
}
