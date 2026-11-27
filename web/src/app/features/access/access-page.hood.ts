import { HoodNote } from '../hood/hood-note';

export const ACCESS_HOOD: HoodNote = {
  why: [
    'Users, groups and per-user overrides. Precedence is deny over allow over role.',
    'Menus and buttons follow the permission codes in /me, and the API checks every call again.',
  ],
  how: [
    'Reads users, groups and roles; changes go to PUT or DELETE /overrides/{userId}/{permission}, needing OVERRIDES_MANAGE.',
    'Changing an existing override needs If-Match (ETag = row version); a stale one is 412, a missing one 428.',
    'Effective permissions are one SQL statement: role grants plus allow overrides, minus unexpired deny overrides.',
    'You cannot change your own overrides (409); the override and its grant-audit row commit together.',
  ],
  adrs: [
    { label: 'ADR-008 Auth and identity model', path: 'docs/adr/ADR-008-auth-and-identity-model.md' },
    { label: 'ADR-006 Data access: xmin is the ETag', path: 'docs/adr/ADR-006-ef-core-reads-dapper-writes.md' },
  ],
  tests: [
    { layer: 'integration', path: 'tests/Fugue.Tests/Identity/OverridesTests.cs', name: 'Changing_an_existing_override_needs_the_current_etag', asserts: 'Changing an override without If-Match is 428, with a stale one 412, and the refused write leaves no grant_audit row.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Identity/EffectivePermissionTests.cs', name: 'A_deny_override_removes_a_role_grant', asserts: 'An unexpired deny override removes a permission the user’s role grants.' },
    { layer: 'api', path: 'bruno/fugue/overrides/03-override-takes-effect.bru', name: 'The override takes effect on the user\'s very next request', asserts: 'After an override is set, the user’s very next request already sees the effect.' },
    { layer: 'web', path: 'web/src/app/features/access/roles-matrix.spec.ts', name: 'renders roles as columns, a dot only where a role grants the permission', asserts: 'The matrix shows roles as columns, with a dot only where a role grants the permission.' },
    { layer: 'arch', path: 'tests/Fugue.ArchTests/FitnessTests.cs', name: 'Fitness_Auth_every_HasPermission_literal_exists_in_the_seeded_catalog', asserts: 'Every HasPermission code in the controllers exists in the seeded catalog, so a typo fails the build.' },
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
