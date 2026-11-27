import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AccessService } from './core/auth/access.service';
import { authGuard, guestGuard, permissionGuard } from './core/auth/guards';
import { landingKind } from './features/home/home-logic';
import { NAV } from './features/shell/nav';

/** "/" is the persona landing (claimant, officer, manager, admin); anyone else goes to the first area they may open. */
const landing: CanActivateFn = async () => {
  const access = inject(AccessService);
  const router = inject(Router);
  try {
    const me = await access.ensure();
    if (landingKind((c) => access.hasIn(me, c)) !== 'other') return true;
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
      {
        path: '',
        pathMatch: 'full',
        canActivate: [landing],
        title: 'Home · Fugue Console',
        loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage),
      },
      {
        path: 'claims/mine',
        canActivate: [permissionGuard],
        data: { permission: 'CLAIMS_SUBMIT', section: 'Claims', breadcrumb: 'My claims' },
        title: 'My claims · Fugue Console',
        loadComponent: () => import('./features/claims/my-claims-page').then((m) => m.MyClaimsPage),
      },
      {
        path: 'claims/new',
        canActivate: [permissionGuard],
        data: { permission: 'CLAIMS_SUBMIT', section: 'Claims', breadcrumb: 'Report an incident' },
        title: 'Report an incident · Fugue Console',
        loadComponent: () => import('./features/claims/report-page').then((m) => m.ReportPage),
      },
      {
        path: 'claims/queue',
        canActivate: [permissionGuard],
        data: { permission: 'CLAIMS_ALL', section: 'Claims', breadcrumb: 'Claims queue' },
        title: 'Claims queue · Fugue Console',
        loadComponent: () => import('./features/claims/claims-queue-page').then((m) => m.ClaimsQueuePage),
      },
      {
        path: 'claims/team',
        canActivate: [permissionGuard],
        data: {
          permission: ['CLAIMS_WORKLOAD', 'CLAIMS_EXPOSURE'],
          section: 'Claims',
          breadcrumb: 'Team and exposure',
        },
        title: 'Team and exposure · Fugue Console',
        loadComponent: () => import('./features/claims/team-page').then((m) => m.TeamPage),
      },
      { path: 'claims/workload', redirectTo: 'claims/team', pathMatch: 'full' },
      { path: 'claims/exposure', redirectTo: 'claims/team', pathMatch: 'full' },
      {
        path: 'claims/:id',
        canActivate: [permissionGuard],
        data: { permission: 'CLAIMS_READ', section: 'Claims', breadcrumb: 'Claim' },
        title: 'Claim · Fugue Console',
        loadComponent: () => import('./features/claims/claim-detail-page').then((m) => m.ClaimDetailPage),
      },
      {
        path: 'customers',
        canActivate: [permissionGuard],
        data: { permission: 'CUSTOMERS_VIEW', section: 'Data', breadcrumb: 'Customers' },
        title: 'Customers · Fugue Console',
        loadComponent: () => import('./features/customers/customers-page').then((m) => m.CustomersPage),
      },
      {
        path: 'audit',
        canActivate: [permissionGuard],
        data: { permission: 'AUDIT_READ', section: 'Data', breadcrumb: 'Audit log' },
        title: 'Audit log · Fugue Console',
        loadComponent: () => import('./features/audit/audit-page').then((m) => m.AuditPage),
      },
      {
        path: 'access',
        canActivate: [permissionGuard],
        data: { permission: ['GROUPS_VIEW', 'OVERRIDES_VIEW'], section: 'Access', breadcrumb: 'Users & access' },
        title: 'Access · Fugue Console',
        loadComponent: () => import('./features/access/access-page').then((m) => m.AccessPage),
      },
      {
        path: 'modules',
        canActivate: [permissionGuard],
        data: { permission: 'MODULES_VIEW', section: 'Platform', breadcrumb: 'Modules' },
        title: 'Modules · Fugue Console',
        loadComponent: () => import('./features/modules/modules-page').then((m) => m.ModulesPage),
      },
      {
        path: 'webhooks',
        canActivate: [permissionGuard],
        data: { permission: 'WEBHOOKS_MANAGE', section: 'Platform', breadcrumb: 'Webhooks' },
        title: 'Webhooks · Fugue Console',
        loadComponent: () => import('./features/webhooks/webhooks-page').then((m) => m.WebhooksPage),
      },
      {
        path: 'me',
        data: { section: 'Access', breadcrumb: 'My profile' },
        title: 'My profile · Fugue Console',
        loadComponent: () => import('./features/profile/profile-page').then((m) => m.ProfilePage),
      },
      {
        path: 'unavailable',
        data: { breadcrumb: 'Not available' },
        title: 'Not available · Fugue Console',
        loadComponent: () => import('./features/shell/status-pages').then((m) => m.UnavailablePage),
      },
      {
        path: '**',
        data: { breadcrumb: 'Not found' },
        title: 'Not found · Fugue Console',
        loadComponent: () => import('./features/shell/status-pages').then((m) => m.NotFoundPage),
      },
    ],
  },
];
