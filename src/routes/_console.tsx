import { createFileRoute, Outlet, useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";
import { SidebarProvider } from "@/components/ui/sidebar";
import { AppSidebar } from "@/components/console/app-sidebar";
import { Topbar } from "@/components/console/topbar";
import { useConsole } from "@/lib/console";

export const Route = createFileRoute("/_console")({
  ssr: false,
  component: ConsoleLayout,
});

function ConsoleLayout() {
  const { session, ready } = useConsole();
  const navigate = useNavigate();
  useEffect(() => { if (ready && !session) navigate({ to: "/", replace: true }); }, [ready, session, navigate]);
  if (!ready || !session) return null;
  return (
    <SidebarProvider>
      <div className="flex min-h-screen w-full">
        <AppSidebar />
        <div className="flex min-w-0 flex-1 flex-col">
          <Topbar />
          <main className="mx-auto w-full max-w-[1400px] flex-1 px-10 py-10"><Outlet /></main>
        </div>
      </div>
    </SidebarProvider>
  );
}
