import { HoodNote } from '../hood/hood-note';

export const HOME_HOOD: HoodNote = {
  why: [
    'The landing is chosen from permission codes, not role names: admin, manager, officer, claimant. Anyone else goes to their first nav item.',
    'Each persona gets one primary action and reads only what its scope allows.',
  ],
  how: [
    'Calls GET /me for the permission codes, then GET /claims with the persona’s filters (status, assigned).',
    'The page never checks a role name; the API reads the same permission codes from the token on every call (D-044).',
    'A claimant’s list is scoped server-side to their own claims; staff with CLAIMS_ALL see the queue.',
  ],
  adrs: [
    { label: 'ADR-012 OpenAPI contract and GET /me', path: 'docs/adr/ADR-012-openapi-contract-and-console-boundary.md' },
    { label: 'ADR-008 Auth and identity model', path: 'docs/adr/ADR-008-auth-and-identity-model.md' },
  ],
  decisions: [
    { label: 'D-044 /me drives navigation and checks', path: 'docs/decisions.md' },
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
    { label: 'D-053 Claims platform shape', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
      code: `[HttpGet]
[HasPermission(Permissions.ClaimsRead)]`,
      note: 'Without CLAIMS_READ the claim lists are refused with 403.',
    },
    {
      status: '404',
      name: 'outside your scope',
      path: 'src/Fugue.Application/Features/Claims/ClaimScope.cs',
      code: `public static ClaimScope From(ICurrentUser user) =>
    new(user.HasPermission(Permissions.ClaimsAll), user.UserId is { } id ? UserId.From(id) : null);`,
      note: 'The scope comes from the caller, never from a request field.',
    },
  ],
};
