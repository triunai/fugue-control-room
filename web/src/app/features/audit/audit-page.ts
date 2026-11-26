import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { injectInfiniteQuery, keepPreviousData } from '@tanstack/angular-query';
import { AuditApi, auditKeys, AuditFilter } from '../../api/audit.api';
import { AuditLogDto } from '../../api/models';
import { displayMasked, RelativeTimePipe, StampPipe } from '../../ui/format';
import { RefChip } from '../../ui/ref-chip';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { AuditFilterStore } from './audit-filter.store';

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
  imports: [ReactiveFormsModule, RefChip, RelativeTimePipe, StampPipe, DatePipe, SkeletonRows, EmptyState, ErrorState],
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

  protected readonly entityTypes = ENTITY_TYPES;
  protected readonly form = this.fb.group({ ...this.store.filter() });

  protected readonly query = injectInfiniteQuery(() => ({
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

  protected outcomeClass(e: AuditLogDto): string {
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
