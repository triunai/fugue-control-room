import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { MeDto } from '../../api/models';
import { AccessService } from '../../core/auth/access.service';
import { AuthService } from '../../core/auth/auth.service';
import { PrefsStore } from '../../core/ui/prefs.store';
import { Shell } from './shell';

const ADMIN = ['CLAIMS_ALL', 'CUSTOMERS_VIEW', 'AUDIT_READ', 'GROUPS_VIEW', 'MODULES_VIEW', 'WEBHOOKS_MANAGE'];

class FakeAccess {
  readonly granted = signal(ADMIN);
  readonly me = signal({ displayName: 'Dev Admin', roles: ['ADMIN'] } as unknown as MeDto);
  readonly permissions = computed(() => new Set(this.granted()));
  has(code: string | readonly string[]): boolean {
    const set = this.permissions();
    return typeof code === 'string' ? set.has(code) : code.some((c) => set.has(c));
  }
}

function render() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideTanStackQuery(() => new QueryClient()),
      { provide: AccessService, useClass: FakeAccess },
      { provide: AuthService, useValue: { signOut: () => Promise.resolve() } },
    ],
  });
  const fixture = TestBed.createComponent(Shell);
  fixture.detectChanges();
  return fixture;
}

describe('Shell sidebar', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('fugue.tour.seen.admin', '1'); // no auto-started tour inside these tests
  });

  it('scrolls the nav list in its own container between the header and the pinned footer', () => {
    const fixture = render();
    const root = fixture.nativeElement as HTMLElement;
    const side = root.querySelector('.side') as HTMLElement;
    const kids = [...side.children].map((c) => c.className.split(' ')[0]);
    expect(kids).toEqual(['side-head', 'nav-scroll', 'side-foot']);
    expect(root.querySelector('.side-foot .group, .side-foot .item')).toBeNull();
  });

  it('lists the last group (Platform) items for an admin inside the scroll container', () => {
    const fixture = render();
    const root = fixture.nativeElement as HTMLElement;
    const scroll = root.querySelector('.nav-scroll') as HTMLElement;
    const labels = [...scroll.querySelectorAll('.item .label')].map((e) => e.textContent?.trim());
    expect(labels).toContain('Modules');
    expect(labels).toContain('Webhooks');
  });

  it('keeps the account actions in a menu that opens on demand, so the footer stays short', () => {
    const fixture = render();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('.side-foot [role="menu"]')).toBeNull();
    (root.querySelector('.acct') as HTMLButtonElement).click();
    fixture.detectChanges();
    const items = [...root.querySelectorAll('[role="menuitem"]')].map((e) => (e as HTMLElement).textContent?.trim());
    expect(items).toEqual(['My profile', 'Audit log', 'Take the tour', 'Sign out']);
  });

  it('opens the group that holds the open route even if the user had folded it', () => {
    const prefs = TestBed.inject(PrefsStore);
    prefs.toggleGroup('Claims');
    expect(prefs.collapsedGroups()).toContain('Claims');
    prefs.openGroup('Claims');
    expect(prefs.collapsedGroups()).not.toContain('Claims');
  });

  it('wraps the outlet in a padding-free host so each page owns the only .page padding', () => {
    const root = render().nativeElement as HTMLElement;
    expect(root.querySelector('main .page-host')).not.toBeNull();
    expect(root.querySelector('main .page')).toBeNull();
  });

  it('folds the starter example group on first run', () => {
    render();
    expect(TestBed.inject(PrefsStore).collapsedGroups()).toContain('Starter example');
  });
});
