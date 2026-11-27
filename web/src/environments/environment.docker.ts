import { DemoAccount } from './environment.types';

/**
 * Docker compose build (runbook 10): an optimised bundle served by nginx on the same origin as the API (apiOrigin ''),
 * with the Development affordances on because the compose API runs in Development with seeding. The seeded accounts and
 * their shared DEVELOPMENT-ONLY password come from the API seeder (runbook 05). Never reuse the password.
 */
export const environment = {
  development: true,
  apiOrigin: '',
  demoAccounts: [
    { email: 'admin@fugue.test', role: 'Admin', shows: 'everything, all rows, modules and webhooks' },
    { email: 'staff@fugue.test', role: 'Staff', shows: 'every customer, no platform settings' },
    { email: 'support@fugue.test', role: 'Group: Support', shows: 'staff rights arriving through a group' },
    { email: 'self@fugue.test', role: 'Self-service', shows: 'only their own customer' },
  ] as DemoAccount[],
  demoPassword: 'Fugue-Dev-Passw0rd!', // secrets-scan:ignore (seeded dev-only password, runbook 05)
};
