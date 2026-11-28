export interface NavItem {
  label: string;
  path: string;
  /** Permission CODE(S) (any of). Absent: always visible. */
  permission?: string | string[];
  icon: string;
  /** Opens a static page outside the app (new tab); `path` is then a plain URL, never a route. */
  external?: boolean;
}

export interface NavGroup {
  label: string;
  items: NavItem[];
}

/** The starter's sample aggregate lives in its own group at the bottom, folded by default (see PrefsStore). */
export const STARTER_GROUP = 'Starter example';

/** Grouped Data / Access / Platform. Items are filtered by permission code; a switched-off module drops its codes, so its items vanish. */
export const NAV: NavGroup[] = [
  {
    label: 'Claims',
    items: [
      {
        label: 'My claims',
        path: '/claims/mine',
        permission: 'CLAIMS_SUBMIT',
        icon: 'M4 2.5h8v11H4zM6 5.5h4M6 8h4M6 10.5h2.5',
      },
      {
        label: 'Report an incident',
        path: '/claims/new',
        permission: 'CLAIMS_SUBMIT',
        icon: 'M8 3v10M3 8h10',
      },
      {
        label: 'Claims queue',
        path: '/claims/queue',
        permission: 'CLAIMS_ALL',
        icon: 'M2.5 3.5h11M2.5 8h11M2.5 12.5h7',
      },
      {
        label: 'Team and exposure',
        path: '/claims/team',
        permission: ['CLAIMS_WORKLOAD', 'CLAIMS_EXPOSURE'],
        icon: 'M3 13V8M8 13V3M13 13V6',
      },
      {
        label: 'Reserve sheet',
        path: '/claims/sheet',
        permission: 'CLAIMS_HANDLE',
        icon: 'M2.5 2.5h11v11h-11zM2.5 6.5h11M2.5 10h11M7 2.5v11',
      },
      {
        label: 'Import',
        path: '/claims/import',
        permission: 'CLAIMS_HANDLE',
        icon: 'M8 2.5v7M5 6.5l3 3 3-3M3 10.5v3h10v-3',
      },
      {
        label: 'SLA calendar',
        path: '/claims/calendar',
        permission: 'CLAIMS_READ',
        icon: 'M2.5 3.5h11v10h-11zM2.5 6.5h11M5.5 2v3M10.5 2v3',
      },
    ],
  },
  {
    label: 'Data',
    items: [
      { label: 'Audit log', path: '/audit', permission: 'AUDIT_READ', icon: 'M4 2.5h8v11H4zM6 5.5h4M6 8h4M6 10.5h2.5' },
    ],
  },
  {
    label: 'Access',
    items: [
      {
        label: 'Users & access',
        path: '/access',
        permission: ['GROUPS_VIEW', 'OVERRIDES_VIEW'],
        icon: 'M8 2l5 2v4c0 3-2.2 5-5 6-2.8-1-5-3-5-6V4z',
      },
      {
        label: 'My profile',
        path: '/me',
        icon: 'M8 8a2.75 2.75 0 1 0 0-5.5A2.75 2.75 0 0 0 8 8M2.5 14c.6-2.6 2.8-4 5.5-4s4.9 1.4 5.5 4',
      },
    ],
  },
  {
    label: 'Platform',
    items: [
      {
        label: 'Modules',
        path: '/modules',
        permission: 'MODULES_VIEW',
        icon: 'M2.5 2.5h4.5v4.5H2.5zM9 2.5h4.5v4.5H9zM2.5 9h4.5v4.5H2.5zM9 9h4.5v4.5H9z',
      },
      {
        label: 'Changelog',
        path: '/changelog',
        permission: ['CLAIMS_ALL', 'MODULES_VIEW'],
        icon: 'M8 2.5v11M8 5.5H3.5M8 10.5h4.5M2.5 5.5h2M11.5 10.5h2',
      },
      {
        label: 'Fugue, explained',
        path: '/explained/index.html',
        external: true,
        icon: 'M4 2.5h8v11H4zM6 5.5h4M6 8h4M6 10.5h2.5',
      },
      {
        label: 'Webhooks',
        path: '/webhooks',
        permission: 'WEBHOOKS_MANAGE',
        icon: 'M5.5 10.5a2.5 2.5 0 1 1-2.2-3.7L6 2.5M10.5 5.5a2.5 2.5 0 1 1 1.3 4.6H6.5M8 13.5a2.5 2.5 0 1 1 3.6-2.2L9 7',
      },
    ],
  },
  {
    label: STARTER_GROUP,
    items: [
      {
        label: 'Customers',
        path: '/customers',
        permission: 'CUSTOMERS_VIEW',
        icon: 'M3 13c0-2.2 2.2-3.5 5-3.5s5 1.3 5 3.5M8 7.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5',
      },
    ],
  },
];

/** The items a user may see, per group, dropping empty groups: the one rule the shell and its tests share. */
export function visibleGroups(has: (code: string | string[]) => boolean): NavGroup[] {
  return NAV.map((g) => ({ ...g, items: g.items.filter((i) => !i.permission || has(i.permission)) })).filter(
    (g) => g.items.length > 0,
  );
}
