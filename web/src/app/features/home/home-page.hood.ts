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
  tests: [
    { layer: 'web', path: 'web/src/app/features/home/home-logic.spec.ts', name: 'picks the persona from permission codes, broadest first', asserts: 'The landing persona (admin, manager, officer, claimant) comes from permission codes, broadest first.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Identity/MeTests.cs', name: 'Overrides_are_applied_deny_beats_allow_beats_role_and_an_expired_one_is_ignored', asserts: '/me lists effective permission codes: deny beats allow beats role, and an expired override is ignored.' },
    { layer: 'api', path: 'bruno/fugue/me/01-me-as-admin.bru', name: 'Me as admin (identity, permissions, enabled modules)', asserts: '/me as admin answers 200 with identity, permission codes and enabled modules.' },
    { layer: 'arch', path: 'tests/Fugue.ArchTests/FitnessTests.cs', name: 'Fitness_Auth_the_signed_in_only_endpoint_set_is_exactly_me', asserts: 'The only action open to any signed-in user without a permission is MeController.Get.' },
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
