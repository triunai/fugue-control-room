import { toast } from "sonner";
import { Copy } from "lucide-react";

export function errorToast(title: string, opts: { status?: number; correlationId: string; description?: string }) {
  toast.custom((t) => (
    <div className="w-[380px] rounded-md border border-primary/50 bg-popover p-4 text-popover-foreground shadow-xl">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium">{opts.status ? <span className="mr-2 font-mono text-destructive">{opts.status}</span> : null}{title}</p>
          {opts.description && <p className="mt-1 text-xs text-muted-foreground">{opts.description}</p>}
        </div>
        <button onClick={() => toast.dismiss(t)} className="text-xs text-muted-foreground hover:text-foreground">✕</button>
      </div>
      <button
        onClick={() => { void navigator.clipboard?.writeText(opts.correlationId); toast.success("Reference copied", { description: opts.correlationId }); }}
        className="mt-3 inline-flex items-center gap-1.5 rounded-sm border border-border bg-muted px-2 py-1 font-mono text-[11px] text-muted-foreground transition-colors hover:border-primary hover:text-foreground"
        aria-label="Copy correlation reference"
      >
        <Copy className="h-3 w-3" /> ref {opts.correlationId}
      </button>
    </div>
  ), { duration: 9000 });
}
