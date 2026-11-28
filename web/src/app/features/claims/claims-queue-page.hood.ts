import { HoodNote } from '../hood/hood-note';

export const CLAIMS_QUEUE_HOOD: HoodNote = {
  why: [
    'Open claims, longest-waiting first, in keyset pages ("Load more"). Tab counts come from one grouped counts call that follows the same filters.',
    'It refreshes on focus and every 15 s. Reserves show in each claim’s own currency and are never added across currencies.',
  ],
  flow: [
    {
      layer: 'web',
      text:
        'The page keeps its filters in the URL and refetches on focus and every 15 s.',
      path: 'web/src/app/features/claims/claims-query-params.ts',
    },
    {
      layer: 'api',
      text:
        'GET /claims needs CLAIMS_READ; the controller turns the query string into a ListClaimsQuery.',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
    },
    {
      layer: 'handler',
      text:
        "The caller's scope comes from the token: CLAIMS_ALL sees every claim, anyone else only their own (D-063).",
      path: 'src/Fugue.Application/Features/Claims/Helpers/ClaimScope.cs',
    },
    {
      layer: 'db',
      text:
        'An EF keyset read (no tracking), the scope and filters applied in the query; oldest first for staff. GET /claims/counts uses the same filter.',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimRepository.cs',
    },
    {
      layer: 'search',
      text:
        'Typing in the search box calls GET /claims/search, which asks Meilisearch with the scope as a filter in the engine query.',
      path: 'src/Fugue.Infrastructure/Search/MeilisearchClaimSearch.cs',
    },
    {
      layer: 'job',
      text:
        'The claim indexer keeps that index current from the claims table; if the engine is off the search answers 503 and this list stays.',
      path: 'src/Fugue.Infrastructure/Search/ClaimIndexer.cs',
    },
  ],
  how: [
    'Calls GET /claims (keyset paged, filters in the query) and GET /claims/counts for the tabs; both need CLAIMS_READ.',
    'Staff with CLAIMS_ALL see every claim, oldest first; everyone else is scoped to their own claims by the same query.',
    'The search box calls GET /claims/search (Meilisearch) with the scope filter applied server-side; if the engine is off (503) this list is the fallback.',
    'Money is a bigint in minor units plus a currency code, shown as the claim’s own currency.',
  ],
  adrs: [
    { label: 'ADR-008 Auth and identity model', path: 'docs/adr/ADR-008-auth-and-identity-model.md' },
    { label: 'ADR-006 Reads: EF keyset paging', path: 'docs/adr/ADR-006-ef-core-reads-dapper-writes.md' },
  ],
  tests: [
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimsContractTests.cs', name: 'A_claimant_lists_only_their_own_claims_newest_first_and_staff_list_all_oldest_first', asserts: 'A claimant lists only their own claims, newest first; staff list every claim, oldest first.' },
    { layer: 'api', path: 'bruno/fugue/claims/05-staff-queue-oldest-first.bru', name: 'Staff queue: unassigned Submitted claims, oldest first', asserts: 'Filters Submitted and unassigned, answers 200 and checks the submit times are in ascending order.' },
    { layer: 'web', path: 'web/src/app/features/claims/claims-query-params.spec.ts', name: 'round-trips a filter through the URL', asserts: 'A queue filter written to the URL reads back as the same filter.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimSearchTests.cs', name: 'A_claimants_search_never_returns_another_claimants_claim_and_staff_find_both', asserts: 'Search never returns another claimant’s claim; staff find both.' },
    { layer: 'arch', path: 'tests/Fugue.ArchTests/FitnessTests.cs', name: 'Fitness_Auth_every_controller_action_declares_HasPermission_Authorize_or_AllowAnonymous', asserts: 'Every controller action declares a permission, a sign-in requirement or AllowAnonymous, so none is open by accident.' },
  ],
  decisions: [
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
    { label: 'D-053 Claims platform shape', path: 'docs/decisions.md' },
    { label: 'D-057 Money: ISO code, two decimals', path: 'docs/decisions.md' },
    { label: 'D-063 Claims search, scope enforced server-side', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
      code: `[HttpGet]
[HasPermission(Permissions.ClaimsRead)]`,
      note: 'Without CLAIMS_READ the list is refused with 403.',
    },
    {
      status: '404',
      name: 'outside your scope',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimRepository.cs',
      code: `scope.All ? query
: scope.ClaimantId is { } owner ? query.Where(c => c.ClaimantUserId == owner)
: query.Where(_ => false);`,
      note: 'The same scope filter applies to the list; a claim outside it is simply absent (404 on its own page).',
    },
    {
      status: '200',
      name: 'search scope',
      path: 'src/Fugue.Infrastructure/Search/MeilisearchClaimSearch.cs',
      code: `internal static string? BuildFilter(ClaimSearchFilter f) =>
f.ClaimantUserId is { } claimant ? $"claimantUserId = {Quote(claimant.ToString())}" : null;`,
      note: 'Search carries this filter to the engine for anyone without CLAIMS_ALL, so another claimant\u2019s claim never comes back (no error, it is just not a hit).',
    },
  ],
};
