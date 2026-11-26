import { Link, useRouterState } from "@tanstack/react-router";
import { Users2, ShieldCheck, Boxes, ScrollText, Webhook, UserRound } from "lucide-react";
import {
  Sidebar, SidebarContent, SidebarFooter, SidebarGroup, SidebarGroupContent, SidebarGroupLabel,
  SidebarHeader, SidebarMenu, SidebarMenuButton, SidebarMenuItem,
} from "@/components/ui/sidebar";
import { useConsole } from "@/lib/console";

const staffGroups = [
  { label: "Commerce", items: [{ title: "Customers", url: "/customers", icon: Users2 }] },
  { label: "Identity", items: [{ title: "Users & access", url: "/users", icon: ShieldCheck }] },
  { label: "Platform", items: [
    { title: "Modules", url: "/modules", icon: Boxes },
    { title: "Webhooks", url: "/webhooks", icon: Webhook },
  ] },
  { label: "Compliance", items: [{ title: "Audit log", url: "/audit", icon: ScrollText }] },
];
const selfGroup = { label: "Account", items: [{ title: "My profile", url: "/profile", icon: UserRound }] };

export function AppSidebar() {
  const { session } = useConsole();
  const path = useRouterState({ select: (r) => r.location.pathname });
  const groups = session?.staff ? [...staffGroups, selfGroup] : [selfGroup];
  return (
    <Sidebar collapsible="icon" className="border-r border-sidebar-border">
      <SidebarHeader className="px-4 py-5">
        <div className="flex items-baseline gap-2 overflow-hidden">
          <span className="font-display text-2xl text-primary">𝔉</span>
          <span className="font-display text-xl tracking-wide group-data-[collapsible=icon]:hidden">Fugue Console</span>
        </div>
      </SidebarHeader>
      <SidebarContent>
        {groups.map((g) => (
          <SidebarGroup key={g.label}>
            <SidebarGroupLabel className="eyebrow">{g.label}</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {g.items.map((it) => {
                  const active = path === it.url || path.startsWith(it.url + "/");
                  return (
                    <SidebarMenuItem key={it.url}>
                      <SidebarMenuButton asChild isActive={active} tooltip={it.title}
                        className="data-[active=true]:border-l-2 data-[active=true]:border-primary data-[active=true]:bg-sidebar-accent">
                        <Link to={it.url}><it.icon className="h-4 w-4" /><span>{it.title}</span></Link>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  );
                })}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>
      <SidebarFooter className="px-4 py-4 text-[11px] text-muted-foreground group-data-[collapsible=icon]:hidden">
        api v3.8.1 · eu-west
      </SidebarFooter>
    </Sidebar>
  );
}
