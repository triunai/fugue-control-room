/**
 * The one place the claim pages ask for the guided tour. `TourService` (features/shell/tour.service.ts) registers
 * `window.startTour(key)` when the shell starts; before that, or in a unit test, this is a no-op.
 */
export function startTour(key: string): void {
  void (globalThis as { startTour?: (key: string) => Promise<void> }).startTour?.(key);
}
