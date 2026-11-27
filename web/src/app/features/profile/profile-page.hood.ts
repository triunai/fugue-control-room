import { HoodNote } from '../hood/hood-note';

export const PROFILE_HOOD: HoodNote = {
  why: [
    'Who you are and what you may do, straight from GET /me: the same permission list the API enforces on every call.',
    'Effective = role grants (direct or through a group) + allow overrides − unexpired deny overrides − permissions of modules that are off.',
  ],
  flow: [
    {
      layer: 'web',
      text: 'AccessService reads GET /me as a TanStack query; nav items, route guards and buttons check its permission codes.',
      path: 'web/src/app/core/auth/access.service.ts',
    },
    {
      layer: 'api',
      text: 'On every request OnTokenValidated looks the permissions up fresh (never from the token) and adds them as claims.',
      path: 'src/Fugue.Api/Auth/JwtBearerSetup.cs',
    },
    {
      layer: 'api',
      text: 'MeController.Get needs only a valid token, and the answer is no-store.',
      path: 'src/Fugue.Api/Controllers/MeController.cs',
    },
    {
      layer: 'db',
      text: 'One SQL statement: role ids (direct and via active groups), grants plus allow overrides, minus unexpired denies, minus modules off.',
      path: 'src/Fugue.Infrastructure/Persistence/Sql/identity/user-access.sql',
    },
    {
      layer: 'handler',
      text: 'GetMeHandler adds the profile and the enabled modules; a user deactivated since the token was issued gets 401.',
      path: 'src/Fugue.Application/Features/Me/Queries/GetMe.cs',
    },
    {
      layer: 'web',
      text: 'This page also lists the customers you own: GET /customers, scoped by the API and filtered to your user id.',
      path: 'web/src/app/features/profile/profile-page.ts',
    },
  ],
  how: [
    'Calls GET /me: no permission needed, and the subject is always the caller, never an id from the request.',
    'Not cached: the response is no-store, and the API looks permissions up again on every request, so a change applies at once.',
    'Deny beats allow beats role; an expired override is ignored; a disabled module takes its permissions with it.',
  ],
  adrs: [
    { label: 'ADR-008 Auth and identity model', path: 'docs/adr/ADR-008-auth-and-identity-model.md' },
    {
      label: 'ADR-012 OpenAPI contract and console boundary',
      path: 'docs/adr/ADR-012-openapi-contract-and-console-boundary.md',
    },
  ],
  tests: [
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/MeTests.cs',
      name: 'Overrides_are_applied_deny_beats_allow_beats_role_and_an_expired_one_is_ignored',
      asserts: 'Deny beats allow beats role in /me, and an expired override is ignored.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/MeTests.cs',
      name: 'A_disabled_module_leaves_the_enabled_list_and_takes_its_permissions_with_it',
      asserts: 'A disabled module leaves the enabled list and its permissions leave the permission list.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/MeTests.cs',
      name: 'It_requires_a_token_is_never_cached_and_exposes_only_the_callers_own_fields',
      asserts: 'GET /me needs a token, is never cached and exposes only the caller’s own fields.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/MeTests.cs',
      name: 'A_user_deactivated_after_the_token_was_issued_gets_401_not_a_profile',
      asserts: 'A user deactivated after the token was issued gets 401, not a profile.',
    },
    {
      layer: 'web',
      path: 'web/src/app/core/auth/has-permission.directive.spec.ts',
      name: 're-evaluates when /me changes (a module switched off removes its codes)',
      asserts: 'A permission-gated element re-evaluates when /me changes, e.g. a module switched off.',
    },
  ],
  decisions: [
    { label: 'D-044 GET /me drives navigation and permission checks', path: 'docs/decisions.md' },
    { label: 'D-018 Permissions by uncached DB lookup, not token claims', path: 'docs/decisions.md' },
    { label: 'D-017 Precedence deny > allow > role', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '401',
      name: 'token only, no-store',
      path: 'src/Fugue.Api/Controllers/MeController.cs',
      code: `[HttpGet]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]`,
    },
    {
      status: 'rule',
      name: 'deny beats allow, modules off',
      path: 'src/Fugue.Infrastructure/Persistence/Sql/identity/user-access.sql',
      code: `AND p.id IN (SELECT permission_id FROM granted)
AND p.id NOT IN (SELECT permission_id FROM denied)
AND m.code <> ALL (@disabled)`,
      note: '@disabled is the configuration kill switch; the join on modules drops those switched off at runtime.',
    },
    {
      status: 'rule',
      name: 'fresh on every request',
      path: 'src/Fugue.Api/Auth/JwtBearerSetup.cs',
      code: `foreach (string permission in access.Permissions)
{
    identity.AddClaim(new Claim(FugueClaimTypes.Permission, permission));
}`,
      note: 'Any perm claim inside the token itself is removed first.',
    },
    {
      status: '401',
      name: 'deactivated since sign-in',
      path: 'src/Fugue.Application/Features/Me/Queries/GetMe.cs',
      code: `if (effective is null || profile is null)
{
    …
    return Result.Fail<MeDto>(FugueErrors.Unauthorized("auth.unauthenticated", "Authentication is required."));
}`,
    },
  ],
};
