import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AccessService } from './core/auth/access.service';
import { authGuard, guestGuard, permissionGuard } from './core/auth/guards';
import { ACCESS_HOOD } from './features/access/access-page.hood';
import { AUDIT_HOOD } from './features/audit/audit-page.hood';
import { CLAIM_DETAIL_HOOD } from './features/claims/claim-detail-page.hood';
import { CLAIM_ASSESSMENT_HOOD } from './features/claims/claim-assessment-page.hood';
import { CLAIMS_CALENDAR_HOOD } from './features/claims/claims-calendar-page.hood';
import { CLAIMS_QUEUE_HOOD } from './features/claims/claims-queue-page.hood';
import { MY_CLAIMS_HOOD } from './features/claims/my-claims-page.hood';
import { RESERVE_SHEET_HOOD } from './features/claims/reserve-sheet-page.hood';
import { REPORT_HOOD } from './features/claims/report-page.hood';
import { TEAM_HOOD } from './features/claims/team-page.hood';
import { HOME_HOOD } from './features/home/home-page.hood';
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
        data: { hood: HOME_HOOD },
        title: 'Home · Fugue Console',
        loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage),
      },
      {
        path: 'claims/mine',
        canActivate: [permissionGuard],
        data: { hood: MY_CLAIMS_HOOD, permission: 'CLAIMS_SUBMIT', section: 'Claims', breadcrumb: 'My claims' },
        title: 'My claims · Fugue Console',
        loadComponent: () => import('./features/claims/my-claims-page').then((m) => m.MyClaimsPage),
      },
      {
        path: 'claims/new',
        canActivate: [permissionGuard],
        data: { hood: REPORT_HOOD, permission: 'CLAIMS_SUBMIT', section: 'Claims', breadcrumb: 'Report an incident' },
        title: 'Report an incident · Fugue Console',
        loadComponent: () => import('./features/claims/report-page').then((m) => m.ReportPage),
      },
      {
        path: 'claims/queue',
        canActivate: [permissionGuard],
        data: { hood: CLAIMS_QUEUE_HOOD, permission: 'CLAIMS_ALL', section: 'Claims', breadcrumb: 'Claims queue' },
        title: 'Claims queue · Fugue Console',
        loadComponent: () => import('./features/claims/claims-queue-page').then((m) => m.ClaimsQueuePage),
      },
      {
        path: 'claims/team',
        canActivate: [permissionGuard],
        data: {
          hood: TEAM_HOOD,
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
        path: 'claims/sheet',
        canActivate: [permissionGuard],
        data: { hood: RESERVE_SHEET_HOOD, permission: 'CLAIMS_HANDLE', section: 'Claims', breadcrumb: 'Reserve sheet' },
        title: 'Reserve sheet · Fugue Console',
        loadComponent: () => import('./features/claims/reserve-sheet-page').then((m) => m.ReserveSheetPage),
      },
      {
        path: 'claims/calendar',
        canActivate: [permissionGuard],
        data: { hood: CLAIMS_CALENDAR_HOOD, permission: 'CLAIMS_READ', section: 'Claims', breadcrumb: 'SLA calendar' },
        title: 'SLA calendar · Fugue Console',
        loadComponent: () => import('./features/claims/claims-calendar-page').then((m) => m.ClaimsCalendarPage),
      },
      {
        path: 'claims/:id/assessment',
        canActivate: [permissionGuard],
        data: { hood: CLAIM_ASSESSMENT_HOOD, permission: 'CLAIMS_HANDLE', section: 'Claims', breadcrumb: 'Assessment' },
        title: 'Assessment · Fugue Console',
        loadComponent: () => import('./features/claims/claim-assessment-page').then((m) => m.ClaimAssessmentPage),
      },
      {
        path: 'claims/:id',
        canActivate: [permissionGuard],
        data: { hood: CLAIM_DETAIL_HOOD, permission: 'CLAIMS_READ', section: 'Claims', breadcrumb: 'Claim' },
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
        data: { hood: AUDIT_HOOD, permission: 'AUDIT_READ', section: 'Data', breadcrumb: 'Audit log' },
        title: 'Audit log · Fugue Console',
        loadComponent: () => import('./features/audit/audit-page').then((m) => m.AuditPage),
      },
      {
        path: 'audit/trace/:correlationId',
        canActivate: [permissionGuard],
        data: { permission: 'AUDIT_READ', section: 'Data', breadcrumb: 'The Score' },
        title: 'The Score · Fugue Console',
        loadComponent: () => import('./features/audit/score-page').then((m) => m.ScorePage),
      },
      {
        path: 'access',
        canActivate: [permissionGuard],
        data: {
          hood: ACCESS_HOOD,
          permission: ['GROUPS_VIEW', 'OVERRIDES_VIEW'],
          section: 'Access',
          breadcrumb: 'Users & access',
        },
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
