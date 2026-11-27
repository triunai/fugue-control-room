import data from './decisions.data.json';
import { countEntries, DecisionGroup, filterGroups } from './decisions-logic';

const GROUPS: DecisionGroup[] = [
  {
    version: 'Unreleased',
    date: null,
    entries: [
      {
        kind: 'D',
        id: 'D-065',
        title: 'Parallel lanes',
        date: '2026-11-28',
        summary: 'ADR-014.',
        status: 'accepted',
        adr: 'ADR-014',
        path: 'docs/decisions.md#d-065',
      },
    ],
  },
  {
    version: 'v0.0.2',
    date: '2026-11-27',
    entries: [
      {
        kind: 'ADR',
        id: 'ADR-010',
        title: 'Audit log',
        date: '2026-11-26',
        summary: 'Append-only.',
        status: 'accepted',
        adr: null,
        path: 'docs/adr/ADR-010-audit-log.md',
      },
      {
        kind: 'D',
        id: 'D-027',
        title: 'audit_log table shape',
        date: '2026-11-26',
        summary: 'Trigger.',
        status: 'accepted',
        adr: 'ADR-010',
        path: 'docs/decisions.md#d-027',
      },
    ],
  },
];

describe('filterGroups', () => {
  it('all keeps everything', () => expect(countEntries(filterGroups(GROUPS, 'all', ''))).toBe(3));

  it('ADRs and Decisions chips split by kind and drop emptied groups', () => {
    const adrs = filterGroups(GROUPS, 'adr', '');
    expect(adrs.map((g) => g.version)).toEqual(['v0.0.2']);
    expect(adrs[0].entries.map((e) => e.id)).toEqual(['ADR-010']);
    expect(filterGroups(GROUPS, 'd', '').flatMap((g) => g.entries.map((e) => e.id))).toEqual(['D-065', 'D-027']);
  });

  it('the text filter is case-insensitive over id, title and summary, and combines with the chip', () => {
    expect(filterGroups(GROUPS, 'all', 'AUDIT').flatMap((g) => g.entries.map((e) => e.id))).toEqual([
      'ADR-010',
      'D-027',
    ]);
    expect(filterGroups(GROUPS, 'd', 'audit').flatMap((g) => g.entries.map((e) => e.id))).toEqual(['D-027']);
    expect(filterGroups(GROUPS, 'all', 'd-065')).toHaveLength(1);
    expect(filterGroups(GROUPS, 'all', 'nothing like this')).toEqual([]);
  });
});

describe('the generated data', () => {
  it('lists every ADR and keeps D-rows as real, linked, dated entries', () => {
    const entries = (data.groups as DecisionGroup[]).flatMap((g) => g.entries);
    expect(entries.filter((e) => e.kind === 'ADR')).toHaveLength(data.counts.adrs);
    expect(entries.filter((e) => e.kind === 'D')).toHaveLength(data.counts.decisions);
    for (const e of entries) {
      expect(e.date).toMatch(/^\d{4}-\d{2}-\d{2}$/);
      expect(e.path).toMatch(e.kind === 'ADR' ? /^docs\/adr\/ADR-\d+.*\.md$/ : /^docs\/decisions\.md#d-\d+/);
    }
  });
});
