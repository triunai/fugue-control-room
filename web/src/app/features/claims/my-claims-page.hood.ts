import { HoodNote } from '../hood/hood-note';

export const MY_CLAIMS_HOOD: HoodNote = {
  why: [
    'The server scopes the list to your own claims; the page sends no owner filter, so there is nothing to tamper with.',
    'A claim that is not yours is a 404, the same answer as one that does not exist.',
  ],
  flow: [
    {
      layer: 'web',
      text:
        'The page sends no owner filter: it only asks for the list.',
      path: 'web/src/app/features/claims/my-claims-page.ts',
    },
    {
      layer: 'api',
      text:
        'GET /claims needs CLAIMS_READ and sends a ListClaimsQuery.',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
    },
    {
      layer: 'handler',
      text:
        'ClaimScope.From reads your id and CLAIMS_ALL from the token; without CLAIMS_ALL the scope is your own claims.',
      path: 'src/Fugue.Application/Features/Claims/ClaimScope.cs',
    },
    {
      layer: 'db',
      text:
        'An EF keyset read, newest first for a claimant, with the scope in the WHERE; the internal reserve is not in what a claimant receives.',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimRepository.cs',
    },
    {
      layer: 'web',
      text:
        'Opening a claim that is not yours is 404, the same answer as one that does not exist.',
      path: 'src/Fugue.Application/Features/Claims/Queries/GetClaimById.cs',
    },
  ],
  how: [
    'Calls GET /claims, keyset paged, newest first for a claimant; it needs CLAIMS_READ.',
    'Scope comes from the token: your id, unless you hold CLAIMS_ALL. A claim outside it is 404, never 403.',
    'The internal reserve is never part of what a claimant receives.',
  ],
  adrs: [
    { label: 'ADR-008 Ownership by scoping', path: 'docs/adr/ADR-008-auth-and-identity-model.md' },
  ],
  tests: [
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimsWorkflowTests.cs', name: 'Another_claimant_gets_404_on_the_claim_its_timeline_and_when_answering', asserts: 'Another claimant gets 404 claim.not_found on the claim, its timeline and an answer, like an unknown id; nothing changes.' },
    { layer: 'api', path: 'bruno/fugue/claims/04-other-claimant-gets-404.bru', name: 'Another claimant gets 404 on the claim (never 403)', asserts: 'Another claimant reading the claim gets 404 with code claim.not_found.' },
    { layer: 'web', path: 'web/src/app/features/claims/claim-record.spec.ts', name: 'never mentions the reserve to a claimant', asserts: 'For every status the claimant’s next-step card never mentions the reserve.' },
    { layer: 'e2e', path: 'web/e2e/claims.spec.ts', name: 'claimant reports, staff picks up, claimant sees it move', asserts: 'A claimant reports a claim and finds it in My claims; staff pick it up and the claimant sees it move in plain words.' },
  ],
  decisions: [
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
    { label: 'D-019 Ownership by scoping, 404 across owners', path: 'docs/decisions.md' },
    { label: 'D-056 Claims seam review', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '404',
      name: 'outside your scope',
      path: 'src/Fugue.Application/Features/Claims/ClaimScope.cs',
      code: `public static ClaimScope From(ICurrentUser user) =>
    new(user.HasPermission(Permissions.ClaimsAll), user.UserId is { } id ? UserId.From(id) : null);`,
      note: 'The scope is built from the caller, never from a request field.',
    },
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
      code: `[HttpGet]
[HasPermission(Permissions.ClaimsRead)]`,
      note: 'Without CLAIMS_READ the list is refused with 403.',
    },
  ],
};
