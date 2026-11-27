import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { injectQuery } from '@tanstack/angular-query';
import { CustomersApi } from '../../api/customers.api';
import { AccessService } from '../../core/auth/access.service';
import { RelativeTimePipe } from '../../ui/format';
import { RefChip } from '../../ui/ref-chip';
import { EmptyState, ErrorState } from '../../ui/states';

/** Self-service view: who am I, what may I do (straight from /me), and the customers I own. */
@Component({
  selector: 'fg-profile-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RefChip, RelativeTimePipe, EmptyState, ErrorState, RouterLink],
  template: `
    <div class="page">
      @if (access.query.isError()) {
        <fg-error-state [error]="access.query.error()" (retry)="access.query.refetch()" />
      } @else if (me(); as me) {
        <header class="page-head who">
          <span class="avatar" aria-hidden="true">{{ initials() }}</span>
          <div class="who-main">
            <h1 class="page-title">{{ me.displayName }}</h1>
            <p class="who-email mono">{{ me.email }}</p>
            <div class="flex flex-wrap items-center gap-1">
              @for (r of me.roles; track r) {
                <span class="pill pill-acc pill-plain">{{ r }}</span>
              }
              @for (g of me.groups; track g) {
                <span class="pill pill-muted pill-plain">{{ g }}</span>
              }
            </div>
          </div>
          <div class="who-id">
            <span class="text-muted text-sm">User id</span>
            <fg-ref-chip [value]="me.userId" maxWidth="26ch" />
          </div>
        </header>

        <section class="block" aria-labelledby="perms">
          <div class="block-head">
            <h2 id="perms" class="section-title">What you can do</h2>
            <p class="m-0 text-sm text-muted">
              {{ me.permissions.length }} permissions in effect: roles, plus allow overrides, minus deny overrides,
              minus switched-off modules. Read on every request.
            </p>
          </div>
          <div class="matrix">
            @for (g of grouped(); track g.resource) {
              <div class="perm-row">
                <span class="perm-res">{{ g.resource }} <span class="perm-count">{{ g.actions.length }}</span></span>
                <div class="flex flex-wrap gap-1">
                  @for (a of g.actions; track a) {
                    <span class="pill pill-plain pill-muted mono">{{ a }}</span>
                  }
                </div>
              </div>
            } @empty {
              <p class="m-0 text-muted">No permissions. An administrator can grant a role or an override.</p>
            }
          </div>
        </section>

        <section class="block" aria-labelledby="mods">
          <h2 id="mods" class="section-title">Modules on</h2>
          <div class="flex flex-wrap gap-1">
            @for (m of me.modules; track m.code) {
              <span class="pill pill-ok pill-plain">{{ m.name }}</span>
            } @empty {
              <span class="text-muted">none</span>
            }
          </div>
          <p class="m-0 text-sm text-muted">Tenant: {{ me.tenantId ?? 'none (single-tenant)' }}</p>
        </section>

        @if (access.has('CUSTOMERS_VIEW')) {
          <section class="block" aria-labelledby="mine">
            <div class="flex items-center justify-between">
              <h2 id="mine" class="section-title">Customers you own</h2>
              <a routerLink="/customers" class="btn btn-sm btn-quiet">Open Customers</a>
            </div>
            @if (mine().length) {
              <table class="table">
                <thead>
                  <tr>
                    <th scope="col">Name</th>
                    <th scope="col">Email</th>
                    <th scope="col">Status</th>
                    <th scope="col">Updated</th>
                  </tr>
                </thead>
                <tbody>
                  @for (c of mine(); track c.id) {
                    <tr>
                      <td class="font-semibold">{{ c.name }}</td>
                      <td class="mono">{{ c.email }}</td>
                      <td>
                        <span
                          class="pill"
                          [class.pill-ok]="c.status === 'Active'"
                          [class.pill-warn]="c.status === 'Suspended'"
                          >{{ c.status }}</span
                        >
                      </td>
                      <td class="text-muted">{{ c.updatedAt | relative }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            } @else if (!customers.isPending()) {
              <fg-empty-state title="You own no customers" body="Customers you create are bound to you." />
            }
          </section>
        }
      }
    </div>
  `,
  styles: `
    .who {
      display: flex;
      align-items: center;
      gap: 1.1rem;
    }
    .who-main {
      display: grid;
      gap: 0.35rem;
      flex: 1;
      min-width: 0;
    }
    .who-email {
      margin: 0;
      color: var(--muted);
      overflow-wrap: anywhere;
    }
    .who-id {
      display: grid;
      gap: 0.2rem;
      justify-items: start;
    }
    .avatar {
      display: grid;
      place-items: center;
      flex: none;
      width: 4rem;
      height: 4rem;
      border-radius: 50%;
      background: var(--accent);
      color: var(--on-accent);
      font-family: var(--font-display);
      font-size: 1.6rem;
      font-weight: 600;
    }
    .block {
      display: grid;
      gap: 0.8rem;
      padding: 1.25rem 0;
      border-bottom: 1px solid var(--line-soft);
    }
    .block:last-child {
      border-bottom: 0;
    }
    .block-head {
      display: grid;
      gap: 0.25rem;
    }
    .perm-row {
      display: grid;
      grid-template-columns: 9rem minmax(0, 1fr);
      gap: 0.75rem;
      align-items: baseline;
      padding: 0.45rem 0;
      border-top: 1px solid var(--line-soft);
    }
    .perm-row:first-child {
      border-top: 0;
    }
    .perm-res {
      font-weight: 600;
      font-size: 0.9333rem;
    }
    .perm-count {
      margin-left: 0.3rem;
      color: var(--muted);
      font-weight: 400;
      font-size: 0.8rem;
    }
    @media (max-width: 40rem) {
      .who {
        flex-wrap: wrap;
      }
      .perm-row {
        grid-template-columns: 1fr;
        gap: 0.3rem;
      }
    }
  `,
})
export class ProfilePage {
  protected readonly access = inject(AccessService);
  private readonly api = inject(CustomersApi);
  protected readonly me = this.access.me;
  protected readonly initials = computed(() =>
    (this.me()?.displayName ?? '?')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((w) => w[0].toUpperCase())
      .join(''),
  );

  protected readonly customers = injectQuery(() => ({
    queryKey: ['customers', 'mine'],
    queryFn: () => this.api.list({ search: '', status: '' }, null, 100),
    enabled: this.access.has('CUSTOMERS_VIEW'),
  }));
  protected readonly mine = computed(() =>
    (this.customers.data()?.items ?? []).filter((c) => c.ownerId && c.ownerId === this.me()?.userId),
  );

  /** CUSTOMERS_VIEW, CUSTOMERS_EDIT -> Customers: VIEW, EDIT. */
  protected readonly grouped = computed(() => {
    const groups = new Map<string, string[]>();
    for (const p of this.me()?.permissions ?? []) {
      const i = p.lastIndexOf('_');
      const res = p.slice(0, i);
      groups.set(res, [...(groups.get(res) ?? []), p.slice(i + 1)]);
    }
    return [...groups].map(([resource, actions]) => ({
      resource: resource.charAt(0) + resource.slice(1).toLowerCase(),
      actions,
    }));
  });
}
