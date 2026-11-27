export interface NavItem {
  label: string;
  path: string;
  /** Permission CODE(S) (any of). Absent: always visible. */
  permission?: string | string[];
  icon: string;
}

export interface NavGroup {
  label: string;
  items: NavItem[];
}

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
        label: 'Workload',
        path: '/claims/workload',
        permission: 'CLAIMS_WORKLOAD',
        icon: 'M3 13V8M8 13V3M13 13V6',
      },
      {
        label: 'Exposure',
        path: '/claims/exposure',
        permission: 'CLAIMS_EXPOSURE',
        icon: 'M2.5 13.5h11M4 11V7M8 11V4M12 11V9',
      },
    ],
  },
  {
    label: 'Data',
    items: [
      {
        label: 'Customers',
        path: '/customers',
        permission: 'CUSTOMERS_VIEW',
        icon: 'M3 13c0-2.2 2.2-3.5 5-3.5s5 1.3 5 3.5M8 7.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5',
      },
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
        label: 'Webhooks',
        path: '/webhooks',
        permission: 'WEBHOOKS_MANAGE',
        icon: 'M5.5 10.5a2.5 2.5 0 1 1-2.2-3.7L6 2.5M10.5 5.5a2.5 2.5 0 1 1 1.3 4.6H6.5M8 13.5a2.5 2.5 0 1 1 3.6-2.2L9 7',
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
