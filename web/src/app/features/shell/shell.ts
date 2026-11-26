import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { ActivatedRouteSnapshot, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AccessService } from '../../core/auth/access.service';
import { AuthService } from '../../core/auth/auth.service';
import { CorrelationService } from '../../core/http/correlation.service';
import { ToastStore } from '../../core/ui/toast.store';
import { BrandMark } from './brand-mark';
import { NAV } from './nav';

@Component({
  selector: 'fg-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CdkMenu, CdkMenuItem, CdkMenuTrigger, BrandMark],
  template: `
    <div class="shell">
      <a class="skip" href="#main">Skip to content</a>
      <nav class="side" aria-label="Sections">
        <a routerLink="/" class="brand" aria-label="Fugue Console home">
          <fg-brand-mark />
          <span class="brand-name">Fugue <span class="brand-sub">Console</span></span>
        </a>
        @for (group of groups(); track group.label) {
          <p class="group">{{ group.label }}</p>
          <ul role="list">
            @for (item of group.items; track item.path) {
              <li>
                <a class="item" [routerLink]="item.path" routerLinkActive="on" ariaCurrentWhenActive="page">
                  <svg width="17" height="17" viewBox="0 0 16 16" aria-hidden="true">
                    <path
                      [attr.d]="item.icon"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="1.4"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                    />
                  </svg>
                  {{ item.label }}
                </a>
              </li>
            }
          </ul>
        }
        <div class="side-foot">
          @if (access.query.isFetching()) {
            <span class="sync" title="Checking your permissions">checking access…</span>
          } @else {
            <span class="sync">{{ access.permissions().size }} permissions in effect</span>
          }
        </div>
      </nav>

      <div class="main-col">
        <header class="top">
          <span class="env" title="Environment, API runtime and version">dev · net8 · v1</span>
          <div class="flex-1"></div>
          @if (me(); as me) {
            <button type="button" class="user" [cdkMenuTriggerFor]="userMenu" aria-label="Account menu">
              <span class="avatar" aria-hidden="true">{{ initials() }}</span>
              <span class="user-text">
                <span class="user-name">{{ me.displayName }}</span>
                <span class="user-roles">{{ me.roles.join(', ') || 'no role' }}</span>
              </span>
              <svg width="12" height="12" viewBox="0 0 16 16" aria-hidden="true">
                <path d="M4 6l4 4 4-4" fill="none" stroke="currentColor" stroke-width="1.8" />
              </svg>
            </button>
          }
          <ng-template #userMenu>
            <div class="menu" cdkMenu>
              <div class="px-3 pt-2 pb-3">
                <p class="m-0 font-semibold">{{ me()?.displayName }}</p>
                <p class="mono m-0 text-muted">{{ me()?.email }}</p>
                <div class="mt-2 flex flex-wrap gap-1">
                  @for (r of me()?.roles ?? []; track r) {
                    <span class="pill pill-acc pill-plain">{{ r }}</span>
                  }
                  @for (g of me()?.groups ?? []; track g) {
                    <span class="pill pill-muted pill-plain">group {{ g }}</span>
                  }
                </div>
              </div>
              <div class="border-t border-line pt-1">
                <button type="button" class="menu-item" cdkMenuItem (cdkMenuItemTriggered)="router.navigate(['/me'])">
                  My profile
                </button>
                <button type="button" class="menu-item" cdkMenuItem (cdkMenuItemTriggered)="auth.signOut()">
                  Sign out
                </button>
              </div>
            </div>
          </ng-template>
        </header>
        <main id="main" tabindex="-1" class="outline-none">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: `
    .shell {
      display: grid;
      grid-template-columns: 248px minmax(0, 1fr);
      min-height: 100vh;
    }
    .skip {
      position: absolute;
      left: -999px;
      top: 0.5rem;
      z-index: 50;
      padding: 0.5rem 0.75rem;
      background: var(--surface2);
      border: 1.5px solid var(--focus);
      border-radius: var(--radius-control);
    }
    .skip:focus {
      left: 0.5rem;
    }
    .side {
      position: sticky;
      top: 0;
      height: 100vh;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      background: var(--surface);
      border-right: 1px solid var(--line);
      padding: 1.1rem 0.85rem 1rem;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.2rem 0.5rem 1.2rem;
      color: var(--text);
      text-decoration: none;
    }
    .brand-name {
      font-family: var(--font-display);
      font-weight: 600;
      font-size: 1.7rem;
      line-height: 1;
      letter-spacing: -0.01em;
    }
    .brand-sub {
      color: var(--muted);
      font-weight: 500;
    }
    .group {
      margin: 1rem 0.6rem 0.35rem;
      font-size: 0.8rem;
      font-weight: 600;
      color: var(--muted);
      letter-spacing: 0.01em;
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .item {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0.52rem 0.65rem;
      border-radius: var(--radius-control);
      color: var(--text);
      text-decoration: none;
      font-weight: 500;
    }
    .item svg {
      color: var(--muted);
      flex: none;
    }
    .item:hover {
      background: color-mix(in oklab, var(--accent) 6%, var(--surface));
    }
    .item.on {
      background: var(--accent-soft);
      color: var(--accent-text);
      font-weight: 600;
      box-shadow: inset 3px 0 0 var(--accent);
    }
    .item.on svg {
      color: var(--accent);
    }
    .side-foot {
      margin-top: auto;
      padding: 1rem 0.6rem 0;
    }
    .sync {
      font-size: 0.8rem;
      color: var(--muted);
    }
    .main-col {
      min-width: 0;
      display: flex;
      flex-direction: column;
    }
    .top {
      position: sticky;
      top: 0;
      z-index: 20;
      display: flex;
      align-items: center;
      gap: 1rem;
      height: 3.6rem;
      padding: 0 2rem;
      background: color-mix(in oklab, var(--bg) 88%, transparent);
      backdrop-filter: blur(6px);
      border-bottom: 1px solid var(--line);
    }
    .env {
      font-family: var(--font-mono);
      font-size: 0.8rem;
      color: var(--muted);
      border: 1.5px solid var(--line);
      background: var(--surface);
      border-radius: 999px;
      padding: 0.2rem 0.7rem;
    }
    .user {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.25rem 0.6rem 0.25rem 0.3rem;
      border-radius: 999px;
      border: 1.5px solid var(--line);
      background: var(--surface);
      color: var(--text);
      cursor: pointer;
      font: inherit;
    }
    .user:hover {
      border-color: var(--control);
    }
    .avatar {
      display: grid;
      place-items: center;
      width: 2rem;
      height: 2rem;
      border-radius: 50%;
      background: var(--accent);
      color: var(--on-accent);
      font-family: var(--font-display);
      font-weight: 600;
      font-size: 1.05rem;
    }
    .user-text {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      line-height: 1.15;
    }
    .user-name {
      font-weight: 600;
      font-size: 0.9rem;
    }
    .user-roles {
      font-size: 0.75rem;
      color: var(--muted);
    }
    @media (max-width: 860px) {
      .shell {
        grid-template-columns: 1fr;
      }
      .side {
        position: static;
        height: auto;
        flex-direction: row;
        flex-wrap: wrap;
        gap: 0.25rem;
      }
      .group,
      .side-foot {
        display: none;
      }
      .top {
        padding: 0 1rem;
      }
    }
  `,
})
export class Shell {
  protected readonly access = inject(AccessService);
  protected readonly auth = inject(AuthService);
  protected readonly router = inject(Router);
  private readonly toasts = inject(ToastStore);
  private readonly correlation = inject(CorrelationService);

  protected readonly me = this.access.me;
  protected readonly groups = computed(() => {
    this.access.permissions();
    return NAV.map((g) => ({
      ...g,
      items: g.items.filter((i) => !i.permission || this.access.has(i.permission)),
    })).filter((g) => g.items.length > 0);
  });
  protected readonly initials = computed(() =>
    (this.me()?.displayName ?? '?')
      .split(/\s+/)
      .map((p) => p[0])
      .slice(0, 2)
      .join('')
      .toUpperCase(),
  );

  constructor() {
    // When /me changes under us (refetch on focus or after a token refresh) and the area on screen lost its
    // permission (a module switched off, an override revoked), leave calmly instead of showing a wall of 404s.
    effect(() => {
      const me = this.me();
      if (!me) return;
      const required = this.requiredPermission(this.router.routerState.snapshot.root);
      if (required && !this.access.has(required)) {
        this.toasts.show({
          tone: 'info',
          title: 'This area was just switched off for you',
          body: 'Your permissions changed. The navigation now shows what you can open.',
          ref: this.correlation.id(),
        });
        void this.router.navigate(['/unavailable'], { queryParams: { reason: 'changed' } });
      }
    });
  }

  private requiredPermission(route: ActivatedRouteSnapshot): string | string[] | undefined {
    let r: ActivatedRouteSnapshot | null = route;
    let required: string | string[] | undefined;
    while (r) {
      required = (r.data?.['permission'] as string | string[] | undefined) ?? required;
      r = r.firstChild;
    }
    return required;
  }
}
