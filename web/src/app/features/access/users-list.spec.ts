import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { AccessApi } from '../../api/access.api';
import type { UserDto } from '../../api/generated/model';
import { RolesApi } from '../../api/roles.api';
import { AccessService } from '../../core/auth/access.service';
import { AccessPage } from './access-page';

const user = (n: number): UserDto => ({
  id: `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`,
  email: `user${n}@example.test`,
  displayName: `User ${n}`,
  isActive: true,
  roles: ['STAFF'],
  groups: ['SUPPORT'],
  createdAt: '2026-11-01T00:00:00Z',
});

class FakeAccess {
  readonly me = signal({ userId: 'me' } as never);
  readonly permissions = computed(() => new Set(['USERS_VIEW', 'OVERRIDES_VIEW']));
  has(code: string | readonly string[]): boolean {
    const set = this.permissions();
    return typeof code === 'string' ? set.has(code) : code.some((c) => set.has(c));
  }
}

function setup() {
  const users = vi.fn((q: string, cursor: string | null) =>
    Promise.resolve(
      cursor === null ? { items: [user(1), user(2)], nextCursor: 'c2' } : { items: [user(3)], nextCursor: null },
    ),
  );
  const overrides = vi.fn(() => Promise.resolve([]));
  TestBed.configureTestingModule({
    providers: [
      provideTanStackQuery(() => new QueryClient()),
      { provide: AccessService, useClass: FakeAccess },
      { provide: AccessApi, useValue: { users, overrides, groups: () => Promise.resolve([]) } },
      { provide: RolesApi, useValue: { list: () => Promise.resolve([]) } },
    ],
  });
  const fixture = TestBed.createComponent(AccessPage);
  return { fixture, users, overrides, root: fixture.nativeElement as HTMLElement };
}

describe('Users list', () => {
  it("lists users, loads more by cursor, and a row click opens that user's overrides", async () => {
    const { fixture, users, overrides, root } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(root.querySelectorAll('[data-testid="user-row"]').length).toBe(2);
    expect(root.textContent).toContain('user1@example.test');

    const more = [...root.querySelectorAll('button')].find((b) => b.textContent?.includes('Load more'))!;
    more.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(users).toHaveBeenCalledWith('', 'c2');
    expect(root.querySelectorAll('[data-testid="user-row"]').length).toBe(3);
    expect([...root.querySelectorAll('button')].some((b) => b.textContent?.includes('Load more'))).toBe(false);

    (root.querySelectorAll('[data-testid="user-row"]')[1] as HTMLElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(overrides).toHaveBeenCalledWith('00000000-0000-0000-0000-000000000002');
    expect(root.textContent).toContain('User 2');
  });

  it('debounces the search box into the q parameter', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      const { fixture, users, root } = setup();
      fixture.detectChanges();
      const input = root.querySelector('#u-search') as HTMLInputElement;
      input.value = 'ann';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      await vi.advanceTimersByTimeAsync(100);
      expect(users).not.toHaveBeenCalledWith('ann', null);
      await vi.advanceTimersByTimeAsync(400);
      fixture.detectChanges();
      await vi.waitFor(() => expect(users).toHaveBeenCalledWith('ann', null));
    } finally {
      vi.useRealTimers();
    }
  });
});
