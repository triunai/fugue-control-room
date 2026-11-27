import { HoodNote } from '../hood/hood-note';

export const CLAIMS_CALENDAR_HOOD: HoodNote = {
  why: [
    'The API has no SLA field: due dates are a console rule (SLA_DAYS in sla.ts: pickup 2, decision 14, answer 7, settle 7 days), computed in the browser from each claim’s own dates.',
    'Closed claims have nothing due and are skipped; an open claim past its due instant is drawn outlined as overdue.',
  ],
  how: [
    'Reads only: GET /claims, one open status at a time, paged, oldest first; "Assigned to me" (staff) adds the assigned=me filter. No command is sent from this page.',
    'The list needs CLAIMS_READ and is scoped by the same query as the queue: staff with CLAIMS_ALL see every open claim, anyone else only their own.',
    'Clicking an event opens the claim; clicking a day lists what falls due on it, in your local time zone.',
  ],
  adrs: [
    { label: 'ADR-008 Ownership by scoping', path: 'docs/adr/ADR-008-auth-and-identity-model.md' },
  ],
  tests: [
    { layer: 'web', path: 'web/src/app/features/claims/sla.spec.ts', name: 'Submitted: pickup 2 days after submit', asserts: 'A submitted claim is due for pickup two days after it was submitted.' },
    { layer: 'web', path: 'web/src/app/features/claims/sla.spec.ts', name: 'makes one coloured event per open claim, flags overdue, skips closed claims', asserts: 'One event per open claim, overdue ones flagged, closed claims left out.' },
    { layer: 'web', path: 'web/src/app/features/claims/sla.spec.ts', name: 'lists the claims due on that local day with what is due', asserts: 'Picking a local day lists the claims due on it and what is due.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimsContractTests.cs', name: 'A_claimant_lists_only_their_own_claims_newest_first_and_staff_list_all_oldest_first', asserts: 'The list behind the calendar is scoped: a claimant sees only their own claims, staff see all.' },
    { layer: 'e2e', path: 'web/e2e/07-grid.spec.ts', name: 'the SLA calendar lists open claims on their due dates and opens a claim', asserts: 'Open claims appear on their due dates and clicking one opens the claim.' },
  ],
  decisions: [
    { label: 'D-059 FullCalendar SLA calendar, lazy-loaded', path: 'docs/decisions.md' },
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
    { label: 'The claim and its state machine', path: 'docs/CLAIMS.md#2-the-claim-and-its-state-machine' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
      code: `[HttpGet]
[HasPermission(Permissions.ClaimsRead)]`,
      note: 'Without CLAIMS_READ the list behind the calendar is refused with 403.',
    },
    {
      status: '404',
      name: 'outside your scope',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimRepository.cs',
      code: `scope.All ? query
: scope.ClaimantId is { } owner ? query.Where(c => c.ClaimantUserId == owner)
: query.Where(_ => false);`,
      note: 'A claim outside your scope is simply absent from the calendar (404 on its own page).',
    },
  ],
};
