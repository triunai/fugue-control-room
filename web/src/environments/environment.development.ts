import { DemoAccount } from './environment.types';

/**
 * Development build (ng serve). The seeded accounts and their shared DEVELOPMENT-ONLY password come from the API seeder
 * (runbook 05); they exist only when the API runs with --seed in Development. Never reuse the password.
 */
export const environment = {
  development: true,
  apiOrigin: 'http://localhost:5081',
  demoAccounts: [
    {
      email: 'self@fugue.test',
      role: 'Claimant',
      shows: 'files and follows their own claims, answers what is asked of them',
    },
    {
      email: 'staff@fugue.test',
      role: 'Claims officer',
      shows: 'works the queue: assess, reserve, request info, decide',
    },
    { email: 'manager@fugue.test', role: 'Manager', shows: 'settles approved claims; team workload and exposure' },
    { email: 'admin@fugue.test', role: 'Admin', shows: 'everything: users, roles, audit log, modules, webhooks' },
  ] as DemoAccount[],
  demoPassword: 'Fugue-Dev-Passw0rd!', // secrets-scan:ignore (seeded dev-only password, runbook 05)
};
