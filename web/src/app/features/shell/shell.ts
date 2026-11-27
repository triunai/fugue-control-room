import { ChangeDetectionStrategy, Component, computed, effect, inject, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { AccessService } from '../../core/auth/access.service';
import { CorrelationService } from '../../core/http/correlation.service';
import { PrefsStore } from '../../core/ui/prefs.store';
import { ToastStore } from '../../core/ui/toast.store';
import { HoodSidebar } from '../hood/hood-sidebar';
import { deriveCrumbs } from './breadcrumbs';
import { GlobalSearch } from './global-search';
import { searchVisible } from './global-search-logic';
import { HelpButton } from './help-button';
import { tourKeyFor } from './tour-steps';
import { NavProgress } from './nav-progress';
import { Sidebar } from './sidebar';
import { TourService } from './tour.service';

@Component({
  selector: 'fg-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, Sidebar, NavProgress, HelpButton, GlobalSearch, HoodSidebar],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  protected readonly access = inject(AccessService);
  protected readonly router = inject(Router);
  private readonly prefs = inject(PrefsStore);
  private readonly toasts = inject(ToastStore);
  private readonly correlation = inject(CorrelationService);
  private readonly tour = inject(TourService);

  protected readonly canSearch = computed(() => searchVisible((c) => this.access.has(c)));
  protected readonly me = this.access.me;
  protected readonly collapsed = this.prefs.sidebarCollapsed;
  protected readonly crumbs = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      startWith(null),
      map(() => deriveCrumbs(this.router.routerState.snapshot.root)),
    ),
    { initialValue: [] },
  );

  constructor() {
    // First sign-in per persona: offer the reviewer tour once (dismissible; remembered in localStorage).
    effect(() => {
      if (this.me()) untracked(() => this.tour.maybeAutoStart());
    });

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

  /** Help: the tour of the page on screen. */
  protected help(): void {
    void this.tour.start(tourKeyFor(this.router.url));
  }

  /** The route-change entrance: the page content fades in and rises a few pixels. The first page does not animate. */
  private entered = false;
  protected enter(page: HTMLElement): void {
    // The page card scrolls, not the window, so the router's scroll-to-top does not reach it: a new page starts at the top.
    page.closest('main')?.scrollTo?.({ top: 0 });
    if (!this.entered) {
      this.entered = true;
      return;
    }
    if (typeof page.animate !== 'function' || matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    page.animate(
      [
        { opacity: 0, transform: 'translateY(6px)' },
        { opacity: 1, transform: 'none' },
      ],
      { duration: 200, easing: 'ease-out' },
    );
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
