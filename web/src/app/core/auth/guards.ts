import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { AccessService } from './access.service';
import { AuthService } from './auth.service';
import { SessionStore } from './session.store';

/** Signed in (restoring from the tab's refresh token if needed), else to /sign-in with a return url. */
export const authGuard: CanMatchFn = async (_route, segments) => {
  const session = inject(SessionStore);
  const router = inject(Router);
  if (session.isAuthenticated()) return true;
  if (await inject(AuthService).restore()) return true;
  const returnUrl = '/' + segments.map((s) => s.path).join('/');
  return router.createUrlTree(['/sign-in'], { queryParams: returnUrl !== '/' ? { returnUrl } : {} });
};

/** Already signed in: skip the sign-in page. */
export const guestGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  if (session.isAuthenticated() || (await inject(AuthService).restore())) {
    return inject(Router).createUrlTree(['/']);
  }
  return true;
};

/**
 * Route data `{ permission: 'CODE' | ['A','B'] }`: checks the PERMISSION CODE from /me (a disabled module removes its
 * permissions, so this also hides switched-off areas). A miss renders the calm "not available" page, never a 403 page.
 */
export const permissionGuard: CanActivateFn = async (route) => {
  const required = route.data['permission'] as string | string[] | undefined;
  if (!required) return true;
  const access = inject(AccessService);
  const router = inject(Router);
  try {
    const me = await access.ensure();
    if (access.hasIn(me, required)) return true;
  } catch {
    /* /me failed: treat as not available; the error is visible on the page */
  }
  return router.createUrlTree(['/unavailable'], { queryParams: { from: route.routeConfig?.path ?? '' } });
};
