import { HoodNote } from '../hood/hood-note';

export const ACCESS_HOOD: HoodNote = {
  why: [
    'Users, groups and per-user overrides. Precedence is deny over allow over role.',
    'Menus and buttons follow the permission codes in /me, and the API checks every call again.',
  ],
  flow: [
    {
      layer: 'web',
      text: 'Setting an override sends PUT /overrides/{userId}/{permission}, with If-Match when the override already exists.',
      path: 'web/src/app/api/access.api.ts',
    },
    {
      layer: 'api',
      text: 'OverridesController is a one-liner behind OVERRIDES_MANAGE; [IfMatch(Required = false)] because a new override has no version.',
      path: 'src/Fugue.Api/Controllers/OverridesController.cs',
    },
    {
      layer: 'handler',
      text: 'SetOverrideHandler refuses changing your own overrides (409 override.self_target) and an expiry already in the past.',
      path: 'src/Fugue.Application/Features/Overrides/Commands/ChangeOverride.cs',
    },
    {
      layer: 'db',
      text: 'OverrideAdmin checks the version (missing 428, stale 412), pins xmin and saves through EF.',
      path: 'src/Fugue.Infrastructure/Identity/OverrideAdmin.cs',
    },
    {
      layer: 'audit',
      text: 'The grant_audit row (before and after) is in the same SaveChanges as the override: they commit together.',
      path: 'src/Fugue.Infrastructure/Identity/OverrideAdmin.cs',
    },
    {
      layer: 'api',
      text: 'The user’s very next request sees it: permissions are looked up per request in OnTokenValidated, not read from the token.',
      path: 'src/Fugue.Api/Auth/JwtBearerSetup.cs',
    },
  ],
  how: [
    'Reads users, groups and roles; changes go to PUT or DELETE /overrides/{userId}/{permission}, needing OVERRIDES_MANAGE.',
    'Effective = (role ∪ group-role ∪ allow overrides) − deny overrides − off modules, in one SQL statement.',
    'An override with Until is temporary access (a vendor, a contractor): it stops counting once Until passes.',
    'You cannot change your own overrides (409); the override and its grant-audit row commit together.',
  ],
  adrs: [
    { label: 'ADR-008 Auth and identity model', path: 'docs/adr/ADR-008-auth-and-identity-model.md' },
    { label: 'ADR-006 Data access: xmin is the ETag', path: 'docs/adr/ADR-006-ef-core-reads-dapper-writes.md' },
  ],
  tests: [
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/OverridesTests.cs',
      name: 'Changing_an_existing_override_needs_the_current_etag',
      asserts:
        'Changing an override without If-Match is 428, with a stale one 412, and the refused write leaves no grant_audit row.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/EffectivePermissionTests.cs',
      name: 'A_deny_override_removes_a_role_grant',
      asserts: 'An unexpired deny override removes a permission the user’s role grants.',
    },
    {
      layer: 'api',
      path: 'bruno/fugue/overrides/03-override-takes-effect.bru',
      name: "The override takes effect on the user's very next request",
      asserts: 'After an override is set, the user’s very next request already sees the effect.',
    },
    {
      layer: 'web',
      path: 'web/src/app/features/access/roles-matrix.spec.ts',
      name: 'renders roles as columns, a dot only where a role grants the permission',
      asserts: 'The matrix shows roles as columns, with a dot only where a role grants the permission.',
    },
    {
      layer: 'arch',
      path: 'tests/Fugue.ArchTests/FitnessTests.cs',
      name: 'Fitness_Auth_every_HasPermission_literal_exists_in_the_seeded_catalog',
      asserts: 'Every HasPermission code in the controllers exists in the seeded catalog, so a typo fails the build.',
    },
  ],
  decisions: [
    { label: 'D-017 Precedence deny > allow > role', path: 'docs/decisions.md' },
    { label: 'D-036 Override If-Match', path: 'docs/decisions.md' },
    { label: 'D-044 /me drives permission checks', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Controllers/OverridesController.cs',
      code: `[HasPermission(Permissions.OverridesManage)]
[IfMatch(Required = false)]`,
      note: 'Shown for PUT; DELETE also needs OVERRIDES_MANAGE.',
    },
    {
      status: '412',
      name: 'stale override',
      path: 'src/Fugue.Infrastructure/Identity/OverrideAdmin.cs',
      code: `if (expected != existing.RowVersion)
{
    return Result.Fail<OverrideDto>(OverrideErrors.VersionMismatch());
}`,
      note: 'The override changed since you read it: reload, then retry.',
    },
    {
      status: 'rule',
      name: 'deny beats allow',
      path: 'src/Fugue.Infrastructure/Persistence/Sql/identity/user-access.sql',
      code: `AND p.id IN (SELECT permission_id FROM granted)
AND p.id NOT IN (SELECT permission_id FROM denied)`,
      note: 'A permission is effective only if granted and not denied; an unexpired deny always wins.',
    },
  ],
};
