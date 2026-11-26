import { DemoAccount } from './environment.types';

/**
 * Development build (ng serve). The seeded accounts and their shared DEVELOPMENT-ONLY password come from the API seeder
 * (runbook 05); they exist only when the API runs with --seed in Development. Never reuse the password.
 */
export const environment = {
  development: true,
  apiOrigin: 'http://localhost:5081',
  demoAccounts: [
    { email: 'admin@fugue.test', role: 'Admin', shows: 'everything, all rows, modules and webhooks' },
    { email: 'staff@fugue.test', role: 'Staff', shows: 'every customer, no platform settings' },
    { email: 'support@fugue.test', role: 'Group: Support', shows: 'staff rights arriving through a group' },
    { email: 'self@fugue.test', role: 'Self-service', shows: 'only their own customer' },
  ] as DemoAccount[],
  demoPassword: 'Fugue-Dev-Passw0rd!', // secrets-scan:ignore (seeded dev-only password, runbook 05)
};
