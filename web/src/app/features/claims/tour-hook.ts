/**
 * The one place the claim record asks for the guided tour. The shell's tour service registers `window.startTour(key)`;
 * it is looked up at call time (no import), so this is a no-op when it is absent.
 */
export function startTour(key: string): void {
  (globalThis as { startTour?: (key: string) => void }).startTour?.(key);
}
