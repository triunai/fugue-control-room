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
