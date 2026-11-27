import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs';
import { AccessService } from '../../core/auth/access.service';
import { AuthService } from '../../core/auth/auth.service';
import { TourService } from './tour.service';

/** The account card pinned under the nav: one row that opens an upward menu (profile, audit, tour, sign out). */
@Component({
  selector: 'fg-account-menu',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  host: {
    '(document:click)': 'closeOutside($event)',
    '(document:keydown.escape)': 'open.set(false)',
  },
  template: `
    @if (me(); as me) {
      <button
        type="button"
        class="acct"
        aria-haspopup="menu"
        [attr.aria-expanded]="open()"
        [attr.data-tip]="me.displayName"
        [attr.aria-label]="rail() ? me.displayName : null"
        (click)="open.set(!open())"
      >
        <span class="avatar" aria-hidden="true">{{ initials() }}</span>
        <span class="user-text">
          <span class="user-name">{{ me.displayName }}</span>
          <span class="user-roles">{{ me.roles.join(', ') || 'no role' }}</span>
        </span>
      </button>
      @if (open()) {
        <div class="menu" role="menu" aria-label="Account">
          <a role="menuitem" class="act" routerLink="/me">My profile</a>
          @if (access.has('AUDIT_READ')) {
            <a role="menuitem" class="act" routerLink="/audit">Audit log</a>
          }
          <button type="button" role="menuitem" class="act" (click)="startTour()">Take the tour</button>
          <button type="button" role="menuitem" class="act" (click)="auth.signOut()">Sign out</button>
        </div>
      }
    }
  `,
  styles: `
    :host {
      position: relative;
      display: block;
    }
    :host-context(.rail) .user-text {
      display: none;
    }
    .acct {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      width: 100%;
      padding: 0.4rem 0.45rem;
      border: 0;
      border-radius: var(--radius-control);
      background: none;
      color: var(--text);
      font: inherit;
      text-align: left;
      cursor: pointer;
    }
    :host-context(.rail) .acct {
      justify-content: center;
      padding: 0.3rem 0;
    }
    .acct:hover,
    .acct[aria-expanded='true'] {
      background: color-mix(in oklab, var(--accent) 6%, var(--surface));
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
    .menu {
      position: absolute;
      left: 0;
      right: 0;
      bottom: calc(100% + 0.2rem);
      z-index: 40;
      display: flex;
      flex-direction: column;
      padding: 0.3rem;
      background: var(--surface2);
      border: 1px solid var(--line);
      border-radius: var(--radius-panel);
      box-shadow: var(--shadow-overlay);
    }
    :host-context(.rail) .menu {
      right: auto;
      bottom: 0;
      left: calc(100% + 0.5rem);
      min-width: 10rem;
    }
    .act {
      display: block;
      width: 100%;
      padding: 0.4rem 0.5rem;
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
    .act:hover,
    .act:focus-visible {
      background: color-mix(in oklab, var(--accent) 6%, var(--surface));
    }
  `,
})
export class AccountMenu {
  protected readonly access = inject(AccessService);
  protected readonly auth = inject(AuthService);
  private readonly tour = inject(TourService);
  protected readonly me = this.access.me;
  protected readonly open = signal(false);
  /** True in the icon rail, where the avatar has no visible name. */
  readonly rail = input(false);
  protected readonly initials = computed(() =>
    (this.me()?.displayName ?? '?')
      .split(/\s+/)
      .map((p) => p[0])
      .slice(0, 2)
      .join('')
      .toUpperCase(),
  );

  constructor() {
    const router = inject(Router);
    router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.open.set(false));
  }

  protected startTour(): void {
    this.open.set(false);
    void this.tour.start();
  }

  protected closeOutside(event: Event): void {
    if (this.open() && !(event.target as Element).closest('fg-account-menu')) this.open.set(false);
  }
}
