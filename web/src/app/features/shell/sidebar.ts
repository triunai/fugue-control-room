import { HttpClient, HttpContext } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { injectQuery } from '@tanstack/angular-query';
import { filter, firstValueFrom, map, startWith } from 'rxjs';
import { AccessService } from '../../core/auth/access.service';
import { CONSOLE_CONFIG } from '../../core/config';
import { CORRELATION_ID } from '../../core/http/context';
import { PrefsStore } from '../../core/ui/prefs.store';
import { AccountMenu } from './account-menu';
import { activeItem } from './active-nav';
import { BrandMark } from './brand-mark';
import { healthOf } from './health';
import { visibleGroups } from './nav';

/**
 * The console sidebar: brand and rail toggle on top, the permission-filtered nav in its own scroll container, the
 * account card pinned below. Groups fold (remembered), the group holding the open route opens by itself, the rail
 * mode shows icons with tooltips, and `[` toggles the rail.
 */
@Component({
  selector: 'fg-sidebar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, BrandMark, AccountMenu],
  host: { '[class.rail]': 'collapsed()', '(document:keydown)': 'onKey($event)' },
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
})
export class Sidebar {
  protected readonly access = inject(AccessService);
  protected readonly prefs = inject(PrefsStore);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly apiBase = inject(CONSOLE_CONFIG).apiBase;

  protected readonly collapsed = this.prefs.sidebarCollapsed;
  protected readonly folded = computed(() => new Set(this.prefs.collapsedGroups()));
  /** Count badges per nav path. The slot is wired; nothing fills it yet (no API call just for a badge). */
  protected readonly badges = computed<Record<string, number>>(() => ({}));
  protected readonly groups = computed(() => {
    this.access.permissions();
    const badges = this.badges();
    return visibleGroups((code) => this.access.has(code)).map((g) => ({
      ...g,
      badge: g.items.reduce((sum, i) => sum + (badges[i.path] ?? 0), 0),
    }));
  });
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
      startWith(this.router.url),
    ),
    { initialValue: '/' },
  );
  protected readonly active = computed(() => activeItem(this.groups(), this.url()));

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

  constructor() {
    // The group that holds the page on screen is never left folded.
    effect(() => {
      const a = this.active();
      if (a) this.prefs.openGroup(a.group);
    });
  }

  protected onKey(event: KeyboardEvent): void {
    if (event.key !== '[' || event.metaKey || event.ctrlKey || event.altKey) return;
    const t = event.target as HTMLElement | null;
    if (t && (t.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(t.tagName))) return;
    event.preventDefault();
    this.prefs.toggleSidebar();
  }
}
