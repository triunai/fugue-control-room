import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { injectInfiniteQuery, injectQuery, keepPreviousData } from '@tanstack/angular-query';
import { AuditApi, auditKeys, AuditFilter } from '../../api/audit.api';
import type { AuditSearchHitDto } from '../../api/generated/model';
import { AuditLogDto } from '../../api/models';
import { isApiProblem } from '../../core/http/problem';
import { displayMasked, RelativeTimePipe, StampPipe } from '../../ui/format';
import { RefChip } from '../../ui/ref-chip';
import { DatePicker } from '../../ui/date-picker';
import { Select, SelectOption } from '../../ui/select';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { AuditFilterStore } from './audit-filter.store';
import { highlightSegments, HighlightSegment } from './highlight';

interface DiffRow {
  field: string;
  old: string | null;
  new: string | null;
  oldMasked: boolean;
  newMasked: boolean;
  kind: 'added' | 'removed' | 'changed';
}

interface DayGroup {
  label: string;
  entries: AuditLogDto[];
}

const ENTITY_TYPES = [
  'Customer',
  'User',
  'Module',
  'Group',
  'PermissionOverride',
  'WebhookSubscription',
  'WebhookDelivery',
];

@Component({
  selector: 'fg-audit-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePicker,
    ReactiveFormsModule,
    RouterLink,
    RefChip,
    RelativeTimePipe,
    StampPipe,
    DatePipe,
    SkeletonRows,
    EmptyState,
    ErrorState,
    Select,
  ],
  templateUrl: './audit-page.html',
  styleUrl: './audit-page.css',
})
export class AuditPage {
  private readonly api = inject(AuditApi);
  protected readonly store = inject(AuditFilterStore);
  private readonly fb = inject(NonNullableFormBuilder);

  /** Deep link: /audit?correlationId=... (from a toast's ref, or a runbook). */
  readonly correlationId = input<string>();
  readonly entityId = input<string>();

  protected readonly entityTypeOptions: SelectOption[] = [
    { value: '', label: 'Any' },
    ...ENTITY_TYPES.map((t) => ({ value: t, label: t })),
  ];
  protected readonly outcomeOptions: SelectOption[] = [
    { value: '', label: 'Any' },
    { value: 'success', label: 'success' },
    { value: 'failure', label: 'failure' },
  ];
  protected readonly snippetFields = ['errorCode', 'errorMessage', 'routeTemplate', 'entityId'] as const;
  protected readonly form = this.fb.group({ ...this.store.filter() });

  /** Full-text search runs when a query is typed; no retries so a 503 (engine off) falls back at once. */
  protected readonly searchQuery = injectQuery(() => ({
    queryKey: auditKeys.search(this.store.filter()),
    queryFn: () => this.api.search(this.store.filter()),
    enabled: this.store.filter().q.trim() !== '',
    retry: false,
    placeholderData: keepPreviousData,
  }));
  protected readonly searchUnavailable = computed(() => {
    const e = this.searchQuery.error();
    return isApiProblem(e) && e.status === 503;
  });
  /** Search mode: a query is set and the engine did not answer 503. The plain log is the fallback. */
  protected readonly searchMode = computed(() => this.store.filter().q.trim() !== '' && !this.searchUnavailable());
  protected readonly hits = computed(() => this.searchQuery.data()?.hits ?? []);
  protected readonly selectedHit = computed(() => {
    const list = this.hits();
    return list.find((h) => h.id === this.store.selectedId()) ?? list[0] ?? null;
  });

  protected readonly query = injectInfiniteQuery(() => ({
    enabled: !this.searchMode(),
    queryKey: auditKeys.list(this.store.filter()),
    queryFn: ({ pageParam }) => this.api.list(this.store.filter(), pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    placeholderData: keepPreviousData,
  }));

  protected readonly entries = computed(() => this.query.data()?.pages.flatMap((p) => p.items) ?? []);
  protected readonly selected = computed(() => {
    const list = this.entries();
    return list.find((e) => e.id === this.store.selectedId()) ?? list[0] ?? null;
  });
  protected readonly days = computed<DayGroup[]>(() => {
    const groups: DayGroup[] = [];
    for (const e of this.entries()) {
      const label = dayLabel(e.occurredAt);
      const last = groups.at(-1);
      if (last?.label === label) last.entries.push(e);
      else groups.push({ label, entries: [e] });
    }
    return groups;
  });
  protected readonly diff = computed(() => toDiff(this.selected()));
  protected readonly truncated = computed(() => !!this.selected()?.changes?.['_truncated']);

  constructor() {
    effect(() => {
      const cid = this.correlationId();
      const eid = this.entityId();
      untracked(() => {
        if (cid) this.store.byCorrelation(cid);
        else if (eid) this.store.byEntity('', eid);
      });
    });
    // Keep the form in step when the filter changes from elsewhere ("View all for this correlation id").
    effect(() => {
      const f = this.store.filter();
      untracked(() => this.form.setValue({ ...f }, { emitEvent: false }));
    });
  }

  protected apply(): void {
    this.store.apply(this.form.getRawValue() as AuditFilter);
    this.store.select(null);
  }

  protected clear(): void {
    this.store.clear();
    this.store.select(null);
  }

  /** A hit field as highlight segments: the engine's snippet when it matched there, else the plain value. */
  protected segs(h: AuditSearchHitDto, field: keyof AuditSearchHitDto & string): HighlightSegment[] {
    const snippet = h.highlights?.[field];
    if (snippet) return highlightSegments(snippet);
    const plain = h[field];
    return typeof plain === 'string' && plain ? [{ text: plain, em: false }] : [];
  }

  protected outcomeClass(e: { outcome: string }): string {
    return e.outcome === 'success' ? 'pill-ok' : 'pill-err';
  }

  protected metadataEntries(e: AuditLogDto): [string, string][] {
    return Object.entries(e.metadata ?? {}).map(([k, v]) => [k, typeof v === 'string' ? v : JSON.stringify(v)]);
  }
}

function dayLabel(iso: string): string {
  const d = new Date(iso);
  const same = (a: Date, b: Date) => a.toDateString() === b.toDateString();
  if (same(d, new Date())) return 'Today';
  if (same(d, new Date(Date.now() - 86_400_000))) return 'Yesterday';
  return d.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
}

function isMasked(text: string | null): boolean {
  return !!text && text.includes('•••');
}

export function toDiff(entry: AuditLogDto | null): DiffRow[] {
  if (!entry?.changes) return [];
  return Object.entries(entry.changes)
    .filter(([field]) => !field.startsWith('_'))
    .map(([field, change]) => {
      const hasOld = !!change && 'old' in change;
      const hasNew = !!change && 'new' in change;
      const oldText = hasOld ? displayMasked(change.old) : null;
      const newText = hasNew ? displayMasked(change.new) : null;
      return {
        field,
        old: oldText,
        new: newText,
        oldMasked: isMasked(oldText),
        newMasked: isMasked(newText),
        kind: hasOld && hasNew ? 'changed' : hasNew ? 'added' : 'removed',
      } satisfies DiffRow;
    });
}
