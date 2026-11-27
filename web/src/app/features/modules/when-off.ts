/** Module ids as seeded by IdentityCatalog (Seed(1, n)); the audit log records a module switch against this id, not its code. */
export const MODULE_ENTITY_IDS: Readonly<Record<string, string>> = {
  SYSTEM: '01900000-0000-7000-8001-000000000001',
  CUSTOMERS: '01900000-0000-7000-8001-000000000002',
  IDENTITY: '01900000-0000-7000-8001-000000000003',
  AUDIT: '01900000-0000-7000-8001-000000000004',
  WEBHOOKS: '01900000-0000-7000-8001-000000000005',
};

/**
 * What switching a module off does, from the API as built: the module gate answers 404 `module.disabled` for every
 * endpoint of a gated controller (ModuleGateMiddleware), and the module's permissions leave /me. Writes to the audit log
 * and the webhook dispatcher are not gated.
 */
export const WHEN_OFF: Readonly<Record<string, string>> = {
  SYSTEM: 'Cannot be switched off. It holds sign-in, your own profile and module management itself.',
  CUSTOMERS:
    'The customer pages disappear and every customer endpoint answers 404 for everyone. Customers and their history stay stored.',
  IDENTITY:
    'Groups and permission overrides disappear, and their endpoints answer 404. Stored groups and overrides stay as they are.',
  AUDIT:
    'The audit log page and endpoint answer 404. Other modules keep writing audit rows, so nothing is lost while it is off.',
  WEBHOOKS:
    'The webhook pages, subscriptions API and dev sink answer 404. The dispatcher is not gated, so deliveries already queued keep going out.',
};

export const WHEN_OFF_FALLBACK = 'Its pages disappear and its endpoints answer 404 for everyone. Its data stays.';

export function whenOff(code: string): string {
  return WHEN_OFF[code] ?? WHEN_OFF_FALLBACK;
}
