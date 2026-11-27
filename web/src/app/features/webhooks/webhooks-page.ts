import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { CONSOLE_CONFIG } from '../../core/config';
import { DeliveriesTab } from './deliveries-tab';
import { SinkTab } from './sink-tab';
import { SubscriptionsTab } from './subscriptions-tab';

type Tab = 'subscriptions' | 'deliveries' | 'sink';

@Component({
  selector: 'fg-webhooks-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SubscriptionsTab, DeliveriesTab, SinkTab],
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">Webhooks</h1>
          <p class="page-lede">
            Every claim.* and customer.* event is written to an outbox in the same transaction, then sent to each
            subscriber signed with its own secret. Delivery is at least once; receivers verify and dedupe.
          </p>
        </div>
      </header>
      <div class="tabs" role="tablist" aria-label="Webhooks">
        @for (t of tabs(); track t.id) {
          <button
            type="button"
            class="tab"
            role="tab"
            [id]="'tab-' + t.id"
            [attr.aria-selected]="current() === t.id"
            [attr.aria-controls]="'panel-' + t.id"
            (click)="go(t.id)"
          >
            {{ t.label }}
            @if (t.id === 'sink') {
              <span class="pill pill-warn pill-plain ml-1">dev</span>
            }
          </button>
        }
      </div>
      <div role="tabpanel" [id]="'panel-' + current()" [attr.aria-labelledby]="'tab-' + current()">
        @switch (current()) {
          @case ('deliveries') {
            <fg-deliveries-tab />
          }
          @case ('sink') {
            <fg-sink-tab />
          }
          @default {
            <fg-subscriptions-tab />
          }
        }
      </div>
    </div>
  `,
})
export class WebhooksPage {
  private readonly router = inject(Router);
  private readonly development = inject(CONSOLE_CONFIG).development;

  readonly tab = input<string>();

  protected readonly tabs = computed(() => [
    { id: 'subscriptions' as Tab, label: 'Subscriptions' },
    { id: 'deliveries' as Tab, label: 'Deliveries' },
    ...(this.development ? [{ id: 'sink' as Tab, label: 'Dev sink' }] : []),
  ]);
  protected readonly current = computed<Tab>(() => {
    const t = this.tab() as Tab;
    return this.tabs().some((x) => x.id === t) ? t : 'subscriptions';
  });

  protected go(tab: Tab): void {
    void this.router.navigate([], { queryParams: { tab }, replaceUrl: true });
  }
}
