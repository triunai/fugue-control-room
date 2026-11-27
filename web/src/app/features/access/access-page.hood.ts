import { HoodNote } from '../hood/hood-note';

export const ACCESS_HOOD: HoodNote = {
  why: [
    'Users, groups and per-user overrides. Precedence is deny over allow over role.',
    'Menus and buttons follow the permission codes in /me, and the API checks every call again.',
  ],
  decisions: [
    { label: 'D-017 Precedence deny > allow > role', path: 'docs/decisions.md' },
    { label: 'D-036 Override If-Match', path: 'docs/decisions.md' },
    { label: 'D-044 /me drives permission checks', path: 'docs/decisions.md' },
    { label: 'ADR-008 Auth and identity model', path: 'docs/adr/ADR-008-auth-and-identity-model.md' },
  ],
  guards: ['403 permission', '412 stale override', 'deny beats allow'],
};
