import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import { sleep } from "./mock";

export type Session = { email: string; name: string; staff: boolean };
export type PreviewState = "live" | "loading" | "empty" | "error";

type Ctx = {
  session: Session | null; ready: boolean;
  signIn: (s: Session) => void; signOut: () => void;
  preview: PreviewState; setPreview: (p: PreviewState) => void;
};
const ConsoleCtx = createContext<Ctx | null>(null);
const KEY = "fugue.session";

export function ConsoleProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [ready, setReady] = useState(false);
  const [preview, setPreview] = useState<PreviewState>("live");
  useEffect(() => {
    const raw = localStorage.getItem(KEY);
    if (raw) setSession(JSON.parse(raw) as Session);
    setReady(true);
  }, []);
  const signIn = (s: Session) => { localStorage.setItem(KEY, JSON.stringify(s)); setSession(s); };
  const signOut = () => { localStorage.removeItem(KEY); setSession(null); setPreview("live"); };
  return <ConsoleCtx.Provider value={{ session, ready, signIn, signOut, preview, setPreview }}>{children}</ConsoleCtx.Provider>;
}
export function useConsole() {
  const c = useContext(ConsoleCtx);
  if (!c) throw new Error("ConsoleProvider missing");
  return c;
}

/** Fake fetch that honours the top-bar "preview state" switch. */
export function useMock<T>(load: () => T, empty: T, deps: unknown[] = []) {
  const { preview } = useConsole();
  const [state, setState] = useState<{ status: "loading" | "ok" | "error"; data: T | null }>({ status: "loading", data: null });
  const [n, setN] = useState(0);
  const reload = useCallback(() => setN((x) => x + 1), []);
  useEffect(() => {
    let alive = true;
    setState({ status: "loading", data: null });
    if (preview === "loading") return;
    sleep(450).then(() => {
      if (!alive) return;
      if (preview === "error") setState({ status: "error", data: null });
      else setState({ status: "ok", data: preview === "empty" ? empty : load() });
    });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [preview, n, ...deps]);
  return { ...state, reload, setData: (d: T) => setState({ status: "ok", data: d }) };
}
