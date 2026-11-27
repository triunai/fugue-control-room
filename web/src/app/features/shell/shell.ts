import { HttpClient, HttpContext } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  ActivatedRouteSnapshot,
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { injectQuery } from '@tanstack/angular-query';
import { filter, firstValueFrom, map, startWith } from 'rxjs';
import { AccessService } from '../../core/auth/access.service';
import { AuthService } from '../../core/auth/auth.service';
import { CONSOLE_CONFIG } from '../../core/config';
import { CORRELATION_ID } from '../../core/http/context';
import { CorrelationService } from '../../core/http/correlation.service';
import { PrefsStore } from '../../core/ui/prefs.store';
import { ToastStore } from '../../core/ui/toast.store';
import { BrandMark } from './brand-mark';
import { deriveCrumbs } from './breadcrumbs';
import { healthOf } from './health';
import { NAV } from './nav';

@Component({
  selector: 'fg-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, BrandMark],
  template: `
    <div class="shell" [class.rail]="collapsed()">
      <a class="skip" href="#main">Skip to content</a>
      <nav class="side" aria-label="Sections">
        <div class="side-head">
          <a routerLink="/" class="brand" aria-label="Fugue Console home">
            <fg-brand-mark />
            <span class="brand-name">Fugue <span class="brand-sub">Console</span></span>
          </a>
          <button
            type="button"
            class="rail-toggle"
            [attr.aria-label]="collapsed() ? 'Expand sidebar' : 'Collapse sidebar'"
            [attr.aria-pressed]="collapsed()"
            (click)="prefs.toggleSidebar()"
          >
            <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true">
              <path
                [attr.d]="collapsed() ? 'M6 3l5 5-5 5' : 'M10 3L5 8l5 5'"
                fill="none"
                stroke="currentColor"
                stroke-width="1.8"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
          </button>
        </div>
        <div class="nav-scroll">
          @for (group of groups(); track group.label) {
            @let open = collapsed() || !folded().has(group.label);
            <button
              type="button"
              class="group"
              [attr.aria-expanded]="open"
              [attr.aria-controls]="'grp-' + group.label"
              (click)="prefs.toggleGroup(group.label)"
            >
              <span>{{ group.label }}</span>
              @if (group.badge > 0) {
                <span class="badge">{{ group.badge }}</span>
              }
              <span class="fold" aria-hidden="true">{{ open ? '−' : '+' }}</span>
            </button>
            <ul role="list" [id]="'grp-' + group.label" [hidden]="!open">
              @for (item of group.items; track item.path) {
                <li>
                  <a
                    class="item"
                    [routerLink]="item.path"
                    routerLinkActive="on"
                    ariaCurrentWhenActive="page"
                    [attr.data-tip]="item.label"
                    [attr.aria-label]="collapsed() ? item.label : null"
                  >
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
                    <span class="label">{{ item.label }}</span>
                    @if (badges()[item.path]; as n) {
                      <span class="badge">{{ n }}</span>
                    }
                  </a>
                </li>
              }
            </ul>
          }
        </div>
        <div class="side-foot">
          @if (me(); as me) {
            <div class="card">
              <div class="card-id">
                <span class="avatar" aria-hidden="true">{{ initials() }}</span>
                <span class="user-text">
                  <span class="user-name">{{ me.displayName }}</span>
                  <span class="user-roles">{{ me.roles.join(', ') || 'no role' }}</span>
                </span>
              </div>
              <div class="card-actions">
                <a class="act" routerLink="/me" data-tip="My profile">
                  <span class="label">My profile</span>
                </a>
                @if (access.has('AUDIT_READ')) {
                  <a class="act" routerLink="/audit" data-tip="Audit log">
                    <span class="label">Audit log</span>
                  </a>
                }
                <button type="button" class="act" data-tip="Sign out" (click)="auth.signOut()">
                  <span class="label">Sign out</span>
                </button>
              </div>
            </div>
          }
          <p class="status" [attr.data-tip]="healthLabel()">
            <span
              class="dot"
              [class.up]="health() === 'up'"
              [class.down]="health() === 'down'"
              role="img"
              [attr.aria-label]="healthLabel()"
            ></span>
            <span class="label">Fugue Console &middot; single-tenant &middot; API v1</span>
          </p>
        </div>
      </nav>
      <div class="main-col">
        <header class="top">
          <nav class="crumbs" aria-label="Breadcrumb">
            <ol>
              @for (c of crumbs(); track $index; let last = $last) {
                <li>
                  @if (c.link && !last) {
                    <a [routerLink]="c.link">{{ c.label }}</a>
                  } @else {
                    <span [attr.aria-current]="last ? 'page' : null">{{ c.label }}</span>
                  }
                </li>
              }
            </ol>
          </nav>
          <div class="flex-1"></div>
          <span class="env" title="Environment, API runtime and version">dev &middot; net8 &middot; v1</span>
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
    .shell.rail {
      grid-template-columns: 68px minmax(0, 1fr);
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
      height: 100dvh;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      background: var(--surface);
      border-right: 1px solid var(--line);
      padding: 1.1rem 0.85rem 1rem;
    }
    .nav-scroll {
      flex: 1 1 auto;
      min-height: 0;
      overflow-y: auto;
    }
    .side-head {
      flex: none;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.4rem;
      padding-bottom: 0.6rem;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      min-width: 0;
      padding: 0.2rem 0.3rem;
      color: var(--text);
      text-decoration: none;
    }
    .brand-name {
      font-family: var(--font-display);
      font-weight: 600;
      font-size: 1.5rem;
      line-height: 1;
      letter-spacing: -0.01em;
    }
    .brand-sub {
      color: var(--muted);
      font-weight: 500;
    }
    .rail-toggle {
      flex: none;
      display: grid;
      place-items: center;
      width: 1.7rem;
      height: 1.7rem;
      border-radius: var(--radius-control);
      border: 1.5px solid var(--line);
      background: var(--surface);
      color: var(--muted);
      cursor: pointer;
    }
    .rail-toggle:hover {
      border-color: var(--control);
      color: var(--text);
    }
    .group {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      width: 100%;
      margin: 0.9rem 0 0.3rem;
      padding: 0.2rem 0.6rem;
      border: 0;
      background: none;
      font: inherit;
      font-size: 0.72rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.09em;
      color: var(--muted);
      cursor: pointer;
      text-align: left;
    }
    .group:hover {
      color: var(--text);
    }
    .group .fold {
      margin-left: auto;
      font-size: 1rem;
      line-height: 1;
    }
    .badge {
      margin-left: auto;
      min-width: 1.3rem;
      padding: 0 0.4rem;
      border-radius: 999px;
      background: var(--accent-soft);
      color: var(--accent-text);
      font-size: 0.72rem;
      font-weight: 600;
      text-align: center;
      letter-spacing: 0;
    }
    .group .badge {
      margin-left: 0.2rem;
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    ul[hidden] {
      display: none;
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
      flex: none;
      padding-top: 0.75rem;
    }
    .card {
      border: 1.5px solid var(--line);
      border-radius: var(--radius-control);
      background: var(--bg);
      padding: 0.6rem;
    }
    .card-id {
      display: flex;
      align-items: center;
      gap: 0.6rem;
    }
    .card-actions {
      display: flex;
      flex-direction: column;
      margin-top: 0.5rem;
      padding-top: 0.4rem;
      border-top: 1px solid var(--line);
    }
    .act {
      display: block;
      width: 100%;
      padding: 0.35rem 0.4rem;
      border: 0;
      border-radius: var(--radius-control);
      background: none;
      color: var(--text);
      font: inherit;
      font-size: 0.88rem;
      text-align: left;
      text-decoration: none;
      cursor: pointer;
    }
    .act:hover {
      background: color-mix(in oklab, var(--accent) 6%, var(--surface));
    }
    .status {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0.75rem 0.2rem 0;
      font-size: 0.72rem;
      color: var(--muted);
    }
    .dot {
      flex: none;
      width: 0.55rem;
      height: 0.55rem;
      border-radius: 50%;
      background: var(--muted);
    }
    .dot.up {
      background: var(--ok);
    }
    .dot.down {
      background: var(--err);
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
    .crumbs ol {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: 0.9rem;
      color: var(--muted);
    }
    .crumbs li + li::before {
      content: '\\203A';
      margin: 0 0.5rem;
    }
    .crumbs a {
      color: var(--muted);
      text-decoration: none;
    }
    .crumbs a:hover {
      color: var(--text);
      text-decoration: underline;
    }
    .crumbs li:last-child span {
      color: var(--text);
      font-weight: 600;
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
    .avatar {
      display: grid;
      flex: none;
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
      min-width: 0;
    }
    .user-name {
      font-weight: 600;
      font-size: 0.9rem;
    }
    .user-roles {
      font-size: 0.75rem;
      color: var(--muted);
    }
    /* Icon rail: labels hide, a tooltip names each control on hover or keyboard focus. */
    .rail .side {
      padding-inline: 0.5rem;
      overflow: visible;
    }
    .rail .nav-scroll {
      overflow: visible;
    }
    .rail .side-head {
      flex-direction: column;
      gap: 0.6rem;
    }
    .rail .brand-name,
    .rail .label,
    .rail .user-text,
    .rail .card-actions .act:not(:last-child),
    .rail .group span,
    .rail .item .badge {
      display: none;
    }
    .rail .group {
      padding: 0;
      margin: 0.4rem 0;
      border-top: 1px solid var(--line);
      pointer-events: none;
    }
    .rail .item {
      justify-content: center;
    }
    .rail .card {
      padding: 0.4rem 0.2rem;
    }
    .rail .card-id,
    .rail .status {
      justify-content: center;
    }
    .rail .act {
      text-align: center;
    }
    .rail .card-actions .act:last-child::before {
      content: '\\21E5';
    }
    [data-tip] {
      position: relative;
    }
    .rail [data-tip]:hover::after,
    .rail [data-tip]:focus-visible::after {
      content: attr(data-tip);
      position: absolute;
      left: calc(100% + 0.6rem);
      top: 50%;
      transform: translateY(-50%);
      z-index: 60;
      white-space: nowrap;
      padding: 0.25rem 0.55rem;
      border-radius: var(--radius-control);
      background: var(--text);
      color: var(--bg);
      font-size: 0.8rem;
      font-weight: 500;
    }
    @media (max-width: 860px) {
      .shell,
      .shell.rail {
        grid-template-columns: 1fr;
      }
      .side {
        position: static;
        height: auto;
        overflow: visible;
        flex-direction: row;
        flex-wrap: wrap;
        gap: 0.25rem;
      }
      .group,
      .rail-toggle {
        display: none;
      }
      .side-foot {
        width: 100%;
        padding-top: 0.5rem;
      }
      .nav-scroll {
        overflow: visible;
      }
      .side ul[hidden] {
        display: flex;
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
  protected readonly prefs = inject(PrefsStore);
  private readonly http = inject(HttpClient);
  private readonly apiBase = inject(CONSOLE_CONFIG).apiBase;
  private readonly toasts = inject(ToastStore);
  private readonly correlation = inject(CorrelationService);

  protected readonly me = this.access.me;
  protected readonly collapsed = this.prefs.sidebarCollapsed;
  protected readonly folded = computed(() => new Set(this.prefs.collapsedGroups()));
  /** Count badges per nav path. The slot is wired; nothing fills it yet (no API call just for a badge). */
  protected readonly badges = computed<Record<string, number>>(() => ({}));
  protected readonly groups = computed(() => {
    this.access.permissions();
    const badges = this.badges();
    return NAV.map((g) => {
      const items = g.items.filter((i) => !i.permission || this.access.has(i.permission));
      return { ...g, items, badge: items.reduce((sum, i) => sum + (badges[i.path] ?? 0), 0) };
    }).filter((g) => g.items.length > 0);
  });
  protected readonly crumbs = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      startWith(null),
      map(() => deriveCrumbs(this.router.routerState.snapshot.root)),
    ),
    { initialValue: [] },
  );
  /** Gentle liveness probe: the anonymous ping every 30 s, no retries (a failure is the signal). */
  private readonly ping = injectQuery(() => ({
    queryKey: ['health', 'ping'] as const,
    queryFn: () =>
      firstValueFrom(
        this.http.get(`${this.apiBase}/diagnostics/ping`, {
          context: new HttpContext().set(CORRELATION_ID, 'console-health'),
        }),
      ),
    refetchInterval: 30_000,
    retry: false,
    staleTime: 25_000,
  }));
  protected readonly health = computed(() => healthOf(this.ping.status()));
  protected readonly healthLabel = computed(
    () => ({ up: 'API reachable', down: 'API unreachable', checking: 'Checking API' })[this.health()],
  );
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
