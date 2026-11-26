import type { ReactNode } from "react";
import { useConsole } from "@/lib/console";
import { NotFoundState } from "./states";

export function StaffOnly({ children }: { children: ReactNode }) {
  const { session } = useConsole();
  return session?.staff ? <>{children}</> : <NotFoundState />;
}
