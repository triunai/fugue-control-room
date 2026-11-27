export interface DecisionEntry {
  readonly kind: 'ADR' | 'D';
  readonly id: string;
  readonly title: string;
  readonly date: string;
  readonly summary: string;
  readonly status: string;
  readonly adr: string | null;
  /** Repository path, with an anchor for D-rows. */
  readonly path: string;
}

export interface DecisionGroup {
  readonly version: string;
  readonly date: string | null;
  readonly entries: readonly DecisionEntry[];
}

export type KindFilter = 'all' | 'adr' | 'd';

/** The groups left after the kind chip and the text filter; a group with no entries left is dropped. */
export function filterGroups(groups: readonly DecisionGroup[], kind: KindFilter, text: string): DecisionGroup[] {
  const needle = text.trim().toLowerCase();
  const keep = (e: DecisionEntry): boolean =>
    (kind === 'all' || (kind === 'adr' ? e.kind === 'ADR' : e.kind === 'D')) &&
    (!needle || `${e.id} ${e.title} ${e.summary} ${e.status} ${e.date}`.toLowerCase().includes(needle));
  return groups.map((g) => ({ ...g, entries: g.entries.filter(keep) })).filter((g) => g.entries.length > 0);
}

export function countEntries(groups: readonly DecisionGroup[]): number {
  return groups.reduce((n, g) => n + g.entries.length, 0);
}
