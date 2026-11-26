import { useNavigate } from "@tanstack/react-router";
import { LogOut, UserRound } from "lucide-react";
import { SidebarTrigger } from "@/components/ui/sidebar";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useConsole, type PreviewState } from "@/lib/console";

export function Topbar() {
  const { session, signOut, preview, setPreview } = useConsole();
  const navigate = useNavigate();
  const initials = session?.name.split(" ").map((p) => p[0]).join("") ?? "?";
  return (
    <header className="sticky top-0 z-20 flex h-14 items-center gap-3 border-b border-border bg-background/95 px-4 backdrop-blur">
      <SidebarTrigger />
      <span className="inline-flex items-center gap-1.5 rounded-sm border border-primary/60 px-2 py-0.5 font-mono text-[11px] uppercase tracking-wider text-primary">
        <span className="h-1.5 w-1.5 rounded-full bg-primary" /> production
      </span>
      <div className="ml-auto flex items-center gap-3">
        <div className="flex items-center gap-2">
          <span className="eyebrow">Preview state</span>
          <Select value={preview} onValueChange={(v) => setPreview(v as PreviewState)}>
            <SelectTrigger className="h-8 w-[110px] text-xs"><SelectValue /></SelectTrigger>
            <SelectContent>
              {(["live", "loading", "empty", "error"] as const).map((s) => <SelectItem key={s} value={s} className="text-xs capitalize">{s}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger className="flex items-center gap-2 rounded-sm px-1.5 py-1 hover:bg-accent" aria-label="User menu">
            <span className="flex h-7 w-7 items-center justify-center rounded-full border border-border font-display text-sm">{initials}</span>
            <span className="text-sm">{session?.name}</span>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel>
              <p className="text-sm">{session?.name}</p>
              <p className="text-xs font-normal text-muted-foreground">{session?.email} · {session?.staff ? "staff" : "member"}</p>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => navigate({ to: "/profile" })}><UserRound className="mr-2 h-4 w-4" />My profile</DropdownMenuItem>
            <DropdownMenuItem onClick={() => { signOut(); navigate({ to: "/", replace: true }); }}><LogOut className="mr-2 h-4 w-4" />Sign out</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
