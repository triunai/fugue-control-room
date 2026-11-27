/**
 * The one place the claim record asks for the guided tour. The tour service is built by another lane, so there is no
 * import here: it is looked up on `globalThis.fugueTour` at call time, and this is a no-op until something registers it.
 */
export function startTour(key: string): void {
  (globalThis as { fugueTour?: { start?: (key: string) => void } }).fugueTour?.start?.(key);
}
