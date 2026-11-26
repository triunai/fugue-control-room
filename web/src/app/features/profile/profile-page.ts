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
      <header class="page-head">
        <div>
          <h1 class="page-title">{{ me()?.displayName ?? 'My profile' }}</h1>
          <p class="page-lede">
            What the API knows about you right now. Permissions are read on every request, so a change by an
            administrator shows here on your next visit.
          </p>
        </div>
      </header>

      @if (access.query.isError()) {
        <div class="panel"><fg-error-state [error]="access.query.error()" (retry)="access.query.refetch()" /></div>
      } @else if (me(); as me) {
        <div class="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
          <section class="panel panel-pad flex flex-col gap-4" aria-labelledby="who">
            <h2 id="who" class="section-title">Who you are</h2>
            <dl class="facts">
              <div>
                <dt>Email</dt>
                <dd class="mono">{{ me.email }}</dd>
              </div>
              <div>
                <dt>User id</dt>
                <dd><fg-ref-chip [value]="me.userId" maxWidth="38ch" /></dd>
              </div>
              <div>
                <dt>Tenant</dt>
                <dd>{{ me.tenantId ?? 'none (single-tenant)' }}</dd>
              </div>
              <div>
                <dt>Roles</dt>
                <dd class="flex flex-wrap gap-1">
                  @for (r of me.roles; track r) {
                    <span class="pill pill-acc pill-plain">{{ r }}</span>
                  } @empty {
                    <span class="text-muted">none directly</span>
                  }
                </dd>
              </div>
              <div>
                <dt>Groups</dt>
                <dd class="flex flex-wrap gap-1">
                  @for (g of me.groups; track g) {
                    <span class="pill pill-muted pill-plain">{{ g }}</span>
                  } @empty {
                    <span class="text-muted">none</span>
                  }
                </dd>
              </div>
              <div>
                <dt>Modules on</dt>
                <dd class="flex flex-wrap gap-1">
                  @for (m of me.modules; track m.code) {
                    <span class="pill pill-ok pill-plain">{{ m.name }}</span>
                  }
                </dd>
              </div>
            </dl>
          </section>

          <section class="panel panel-pad" aria-labelledby="perms">
            <h2 id="perms" class="section-title">What you can do</h2>
            <p class="mt-1 mb-4 text-sm text-muted">
              {{ me.permissions.length }} permission codes in effect: roles, plus allow overrides, minus deny overrides,
              minus switched-off modules.
            </p>
            <div class="flex flex-col gap-3">
              @for (g of grouped(); track g.resource) {
                <div class="perm-row">
                  <span class="perm-res">{{ g.resource }}</span>
                  <div class="flex flex-wrap gap-1">
                    @for (a of g.actions; track a) {
                      <span class="pill pill-plain pill-acc mono">{{ a }}</span>
                    }
                  </div>
                </div>
              } @empty {
                <p class="m-0 text-muted">No permissions. An administrator can grant a role or an override.</p>
              }
            </div>
          </section>
        </div>

        @if (access.has('CUSTOMERS_VIEW')) {
          <section class="table-wrap" aria-labelledby="mine">
            <div class="flex items-center justify-between px-4 pt-4 pb-2">
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
    .facts {
      display: grid;
      gap: 0.7rem;
      margin: 0;
    }
    .facts div {
      display: grid;
      grid-template-columns: 7rem minmax(0, 1fr);
      align-items: center;
    }
    .facts dt {
      color: var(--muted);
      font-size: 0.9333rem;
    }
    .facts dd {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .perm-row {
      display: grid;
      grid-template-columns: 8rem minmax(0, 1fr);
      gap: 0.75rem;
      align-items: center;
      padding-bottom: 0.6rem;
      border-bottom: 1px dashed var(--line);
    }
    .perm-row:last-child {
      border-bottom: 0;
    }
    .perm-res {
      font-weight: 600;
      font-size: 0.9333rem;
    }
  `,
})
export class ProfilePage {
  protected readonly access = inject(AccessService);
  private readonly api = inject(CustomersApi);
  protected readonly me = this.access.me;

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
