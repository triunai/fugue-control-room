import { HoodNote } from '../hood/hood-note';

export const CLAIMS_QUEUE_HOOD: HoodNote = {
  why: [
    'Open claims, longest-waiting first, in keyset pages ("Load more"). Tab counts come from one grouped counts call that follows the same filters.',
    'It refreshes on focus and every 15 s. Reserves show in each claim’s own currency and are never added across currencies.',
  ],
  how: [
    'Calls GET /claims (keyset paged, filters in the query) and GET /claims/counts for the tabs; both need CLAIMS_READ.',
    'Staff with CLAIMS_ALL see every claim, oldest first; everyone else is scoped to their own claims by the same query.',
    'Money is a bigint in minor units plus a currency code, shown as the claim’s own currency.',
  ],
  decisions: [
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
    { label: 'D-053 Claims platform shape', path: 'docs/decisions.md' },
    { label: 'D-057 Money: ISO code, two decimals', path: 'docs/decisions.md' },
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
  ],
};
