import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AccessService } from './core/auth/access.service';
import { authGuard, guestGuard, permissionGuard } from './core/auth/guards';
import { NAV } from './features/shell/nav';

/** "/" goes to the first area the user may open (a self-service user lands on Customers or their profile). */
const landing: CanActivateFn = async () => {
  const access = inject(AccessService);
  const router = inject(Router);
  try {
    const me = await access.ensure();
    const first = NAV.flatMap((g) => g.items).find((i) => !i.permission || access.hasIn(me, i.permission));
    return router.createUrlTree([first?.path ?? '/me']);
  } catch {
    return router.createUrlTree(['/me']);
  }
};

export const routes: Routes = [
  {
    path: 'sign-in',
    canActivate: [guestGuard],
    title: 'Sign in · Fugue Console',
    loadComponent: () => import('./features/sign-in/sign-in').then((m) => m.SignIn),
  },
  {
    path: '',
    canMatch: [authGuard],
    loadComponent: () => import('./features/shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', canActivate: [landing], children: [] },
      {
        path: 'customers',
        canActivate: [permissionGuard],
        data: { permission: 'CUSTOMERS_VIEW' },
        title: 'Customers · Fugue Console',
        loadComponent: () => import('./features/customers/customers-page').then((m) => m.CustomersPage),
      },
      {
        path: 'audit',
        canActivate: [permissionGuard],
        data: { permission: 'AUDIT_READ' },
        title: 'Audit log · Fugue Console',
        loadComponent: () => import('./features/audit/audit-page').then((m) => m.AuditPage),
      },
      {
        path: 'access',
        canActivate: [permissionGuard],
        data: { permission: ['GROUPS_VIEW', 'OVERRIDES_VIEW'] },
        title: 'Access · Fugue Console',
        loadComponent: () => import('./features/access/access-page').then((m) => m.AccessPage),
      },
      {
        path: 'modules',
        canActivate: [permissionGuard],
        data: { permission: 'MODULES_VIEW' },
        title: 'Modules · Fugue Console',
        loadComponent: () => import('./features/modules/modules-page').then((m) => m.ModulesPage),
      },
      {
        path: 'webhooks',
        canActivate: [permissionGuard],
        data: { permission: 'WEBHOOKS_MANAGE' },
        title: 'Webhooks · Fugue Console',
        loadComponent: () => import('./features/webhooks/webhooks-page').then((m) => m.WebhooksPage),
      },
      {
        path: 'me',
        title: 'My profile · Fugue Console',
        loadComponent: () => import('./features/profile/profile-page').then((m) => m.ProfilePage),
      },
      {
        path: 'unavailable',
        title: 'Not available · Fugue Console',
        loadComponent: () => import('./features/shell/status-pages').then((m) => m.UnavailablePage),
      },
      {
        path: '**',
        title: 'Not found · Fugue Console',
        loadComponent: () => import('./features/shell/status-pages').then((m) => m.NotFoundPage),
      },
    ],
  },
];
