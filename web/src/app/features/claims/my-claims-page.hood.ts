import { HoodNote } from '../hood/hood-note';

export const MY_CLAIMS_HOOD: HoodNote = {
  why: [
    'The server scopes the list to your own claims; the page sends no owner filter, so there is nothing to tamper with.',
    'A claim that is not yours is a 404, the same answer as one that does not exist.',
  ],
  how: [
    'Calls GET /claims, keyset paged, newest first for a claimant; it needs CLAIMS_READ.',
    'Scope comes from the token: your id, unless you hold CLAIMS_ALL. A claim outside it is 404, never 403.',
    'The internal reserve is never part of what a claimant receives.',
  ],
  adrs: [
    { label: 'ADR-008 Ownership by scoping', path: 'docs/adr/ADR-008-auth-and-identity-model.md' },
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
