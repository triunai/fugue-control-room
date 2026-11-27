import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { hoodUrl } from '../hood/hood-note';
import data from './decisions.data.json';
import { countEntries, DecisionEntry, DecisionGroup, filterGroups, KindFilter } from './decisions-logic';

const GROUPS = data.groups as DecisionGroup[];

@Component({
  selector: 'fg-decisions-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './decisions-page.html',
  styleUrl: './decisions-page.css',
})
export class DecisionsPage {
  protected readonly counts = data.counts;
  protected readonly total = countEntries(GROUPS);
  protected readonly kind = signal<KindFilter>('all');
  protected readonly text = signal('');
  protected readonly groups = computed(() => filterGroups(GROUPS, this.kind(), this.text()));
  protected readonly shown = computed(() => countEntries(this.groups()));

  protected readonly kinds: readonly { value: KindFilter; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'adr', label: 'ADRs' },
    { value: 'd', label: 'Decisions' },
  ];

  protected href(e: DecisionEntry): string {
    return hoodUrl(e.path);
  }

  protected onText(event: Event): void {
    this.text.set((event.target as HTMLInputElement).value);
  }
}
