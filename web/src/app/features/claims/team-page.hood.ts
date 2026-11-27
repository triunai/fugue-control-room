import { HoodNote } from '../hood/hood-note';

export const TEAM_HOOD: HoodNote = {
  why: [
    'Workload and exposure on one page: the team part needs CLAIMS_WORKLOAD, the money part CLAIMS_EXPOSURE (permission codes from /me).',
    'Money is per currency: each market settles in its own, so there is no grand total, and bars are scaled within one currency.',
  ],
  flow: [
    {
      layer: 'web',
      text:
        'The page reads permission codes from /me and shows the team part and the money part separately.',
      path: 'web/src/app/features/claims/team-page.ts',
    },
    {
      layer: 'api',
      text:
        'GET /claims/workload needs CLAIMS_WORKLOAD and GET /claims/exposure needs CLAIMS_EXPOSURE; both only read.',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
    },
    {
      layer: 'handler',
      text:
        'Each query handler asks the report port for its figures as of now.',
      path: 'src/Fugue.Application/Features/Claims/Queries/GetClaimExposure.cs',
    },
    {
      layer: 'db',
      text:
        'Workload is one SQL statement per officer: open claims, the oldest, decisions in 7 and 30 days, and the median days to decide.',
      path: 'src/Fugue.Infrastructure/Persistence/Sql/claims/workload-by-officer.sql',
    },
    {
      layer: 'db',
      text:
        'Exposure is grouped in the database by market, line and currency; the totals add rows of the same currency only.',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimReports.cs',
    },
    {
      layer: 'web',
      text:
        'Bars and totals are scaled within one currency; nothing is converted or added across currencies.',
      path: 'web/src/app/features/claims/team-logic.ts',
    },
  ],
  how: [
    'Calls GET /claims/workload (CLAIMS_WORKLOAD) and GET /claims/exposure (CLAIMS_EXPOSURE), both read-only.',
    'Exposure groups by market, line and currency; totals add rows of the same currency only.',
    'Amounts are bigint minor units plus an ISO currency code; the page formats them, the server never converts.',
    'The page shows or hides each part from the permission codes in /me (D-044); the API checks every call again.',
  ],
  adrs: [
    { label: 'ADR-012 OpenAPI contract and GET /me', path: 'docs/adr/ADR-012-openapi-contract-and-console-boundary.md' },
  ],
  tests: [
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimReportsTests.cs', name: 'Exposure_totals_are_per_currency_never_across_currencies', asserts: 'Exposure totals come per currency (HKD, SGD, THB); 150000 SGD plus 200000 THB is never one number.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimReportsTests.cs', name: 'Workload_needs_the_workload_permission_and_a_claimant_is_refused', asserts: 'A claimant gets 403 on /claims/workload; a manager gets 200.' },
    { layer: 'api', path: 'bruno/fugue/claims/24-exposure.bru', name: 'Exposure (manager): totals per currency, never mixed', asserts: 'Each currency appears once in the totals and every row carries its market’s currency.' },
    { layer: 'web', path: 'web/src/app/features/claims/team-logic.spec.ts', name: 'scales each bar within its own currency: a huge IDR amount does not shrink the SGD bars', asserts: 'Exposure bars are scaled within one currency, so a huge IDR amount does not shrink the SGD bars.' },
  ],
  decisions: [
    { label: 'Workload and exposure', path: 'docs/CLAIMS.md#6-workload-and-exposure' },
    { label: 'Money in minor units', path: 'docs/CLAIMS.md#7-money' },
    { label: 'D-057 Money: ISO code, two decimals', path: 'docs/decisions.md' },
    { label: 'D-044 /me drives permission checks', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
      code: `[HttpGet("workload")]
[HasPermission(Permissions.ClaimsWorkload)]
…
[HttpGet("exposure")]
[HasPermission(Permissions.ClaimsExposure)]`,
      note: 'Each half of the page has its own permission code.',
    },
    {
      status: 'money',
      name: 'never summed across currencies',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimReports.cs',
      code: `// Totals are per currency: a row is only ever added to rows of the SAME currency.
List<ExposureTotalDto> totals = [.. rows.GroupBy(r => r.Currency).OrderBy(g => g.Key, StringComparer.Ordinal)`,
      note: 'Not an HTTP error: there is simply no cross-currency total to read.',
    },
  ],
};
