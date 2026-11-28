/** The Help guide: what each module of the app does. Paths and permission codes mirror nav.ts / app.routes.ts. */
export interface GuideItem {
  readonly label: string;
  readonly text: string;
  /** An in-app route (a plain path, no ids), or an external static page when `external` is set. */
  readonly path?: string;
  readonly external?: boolean;
  /** Permission code(s), any of: when the user lacks them the item is shown dimmed. Absent: always available. */
  readonly permission?: string | readonly string[];
}
export interface GuideModule {
  readonly id: string;
  readonly title: string;
  /** The role that unlocks the module, as shown in "needs <role>". */
  readonly role: string;
  /** The module counts as the user's own when they hold any of these codes (same codes the nav filters on). */
  readonly gate: string | readonly string[];
  readonly items: readonly GuideItem[];
}

export const GUIDE: readonly GuideModule[] = [
  {
    id: 'claimant',
    title: 'Claimant',
    role: 'Claimant',
    gate: 'CLAIMS_SUBMIT',
    items: [
      { label: 'Report an incident', text: 'File a new claim in a few steps.', path: '/claims/new', permission: 'CLAIMS_SUBMIT' },
      { label: 'My claims', text: 'Follow your own claims and their status.', path: '/claims/mine', permission: 'CLAIMS_SUBMIT' },
      { label: 'Answer a question', text: 'Reply when the insurer asks you for more information, from the claim itself.', permission: 'CLAIMS_SUBMIT' },
      { label: 'Letters', text: 'Read the decision and information letters on your claim.', permission: 'CLAIMS_SUBMIT' },
    ],
  },
  {
    id: 'officer',
    title: 'Claims officer',
    role: 'Claims officer',
    gate: 'CLAIMS_HANDLE',
    items: [
      { label: 'Claims queue', text: 'Every open claim; pick one up to work it.', path: '/claims/queue', permission: 'CLAIMS_ALL' },
      { label: 'Claim record and Next step', text: 'The claim, its history, and the one action that moves it forward.', permission: 'CLAIMS_READ' },
      { label: 'Reserve', text: 'Set the money held back for the claim, from the claim record.', permission: 'CLAIMS_HANDLE' },
      { label: 'Request information', text: 'Ask the claimant a question and park the claim until they answer.', permission: 'CLAIMS_HANDLE' },
      { label: 'Approve or reject', text: 'Decide the claim; the decision letter is generated.', permission: 'CLAIMS_HANDLE' },
      { label: 'Assessment worksheet', text: 'The spreadsheet for the loss assessment, opened from a claim.', permission: 'CLAIMS_HANDLE' },
    ],
  },
  {
    id: 'manager',
    title: 'Manager',
    role: 'Manager',
    gate: ['CLAIMS_WORKLOAD', 'CLAIMS_EXPOSURE'],
    items: [
      { label: 'Team and exposure', text: 'Workload per officer and the money at stake across open claims.', path: '/claims/team', permission: ['CLAIMS_WORKLOAD', 'CLAIMS_EXPOSURE'] },
      { label: 'Settle', text: 'Settle an approved claim, from its record.', permission: 'CLAIMS_EXPOSURE' },
      { label: 'Workload', text: 'Who holds how many claims, on the Team and exposure page.', path: '/claims/team', permission: 'CLAIMS_WORKLOAD' },
    ],
  },
  {
    id: 'admin',
    title: 'Admin',
    role: 'Admin',
    gate: ['GROUPS_VIEW', 'OVERRIDES_VIEW', 'MODULES_VIEW', 'AUDIT_READ', 'WEBHOOKS_MANAGE'],
    items: [
      { label: 'Users & access', text: 'Users, roles, groups and per-user overrides.', path: '/access', permission: ['GROUPS_VIEW', 'OVERRIDES_VIEW'] },
      { label: 'Modules', text: 'Switch whole areas of the app on or off.', path: '/modules', permission: 'MODULES_VIEW' },
      { label: 'Audit log', text: 'Every change, who made it, and The Score trace of one request.', path: '/audit', permission: 'AUDIT_READ' },
      { label: 'Webhooks', text: 'Signed claim events delivered to outside systems.', path: '/webhooks', permission: 'WEBHOOKS_MANAGE' },
      { label: 'Import legacy claims', text: 'Bring claims over from an old spreadsheet export.', path: '/claims/import', permission: 'CLAIMS_HANDLE' },
    ],
  },
  {
    id: 'extras',
    title: 'Extras',
    role: 'any signed-in user',
    gate: [],
    items: [
      { label: 'Reserve sheet', text: 'All reserves in a spreadsheet grid, with XLSX export.', path: '/claims/sheet', permission: 'CLAIMS_HANDLE' },
      { label: 'SLA calendar', text: 'Claim deadlines on a calendar.', path: '/claims/calendar', permission: 'CLAIMS_READ' },
      { label: 'Keyword search', text: 'Press / anywhere to search claims by keyword; it tolerates typos.', permission: 'CLAIMS_READ' },
      { label: 'Under the hood', text: 'The right-hand panel showing the live API calls behind the page.' },
      { label: 'Changelog', text: 'What changed and the decisions behind it.', path: '/changelog', permission: ['CLAIMS_ALL', 'MODULES_VIEW'] },
      { label: 'Fugue, explained', text: 'The whole system on one page (opens in a new tab).', path: '/explained/index.html', external: true },
    ],
  },
];
