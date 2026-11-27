import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';
import { guestGuard } from './guards';
import { SessionStore } from './session.store';

describe('guestGuard', () => {
  it('redirects a restorable session to / (Router is resolved before the await, NG0203)', async () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStore, useValue: { isAuthenticated: () => false } },
        { provide: AuthService, useValue: { restore: () => Promise.resolve(true) } },
      ],
    });
    const result = await TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never));
    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/');
  });
});
