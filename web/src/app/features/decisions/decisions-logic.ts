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

/** One line of a release section in CHANGELOG.md; `label` is empty when the line has no `Label:` lead. */
export interface ReleaseNote {
  readonly label: string;
  readonly text: string;
}

/** A `Closed W-NNN` close-out from CHANGELOG.md. */
export interface ClosedItem {
  readonly id: string;
  readonly title: string;
  readonly date: string;
  readonly outcome: string;
  readonly commits: number;
}

export interface DecisionGroup {
  readonly version: string;
  readonly date: string | null;
  readonly notes: readonly ReleaseNote[];
  readonly closed: readonly ClosedItem[];
  readonly entries: readonly DecisionEntry[];
}

export type KindFilter = 'all' | 'notes' | 'adr' | 'd';

/** The groups left after the kind chip and the text filter; a group with no entries left is dropped. */
export function filterGroups(groups: readonly DecisionGroup[], kind: KindFilter, text: string): DecisionGroup[] {
  const needle = text.trim().toLowerCase();
  const match = (text: string): boolean => !needle || text.toLowerCase().includes(needle);
  const keep = (e: DecisionEntry): boolean =>
    (kind === 'all' || (kind === 'adr' ? e.kind === 'ADR' : kind === 'd' && e.kind === 'D')) &&
    match(`${e.id} ${e.title} ${e.summary} ${e.status} ${e.date}`);
  const withNotes = kind === 'all' || kind === 'notes';
  return groups
    .map((g) => ({
      ...g,
      notes: withNotes ? g.notes.filter((n) => match(`${n.label} ${n.text}`)) : [],
      closed: withNotes ? g.closed.filter((c) => match(`${c.id} ${c.title} ${c.outcome} ${c.date}`)) : [],
      entries: g.entries.filter(keep),
    }))
    .filter((g) => g.notes.length + g.closed.length + g.entries.length > 0);
}

/** Everything the filter counts: release notes, closed work items, ADRs and decisions. */
export function countEntries(groups: readonly DecisionGroup[]): number {
  return groups.reduce((n, g) => n + g.notes.length + g.closed.length + g.entries.length, 0);
}
