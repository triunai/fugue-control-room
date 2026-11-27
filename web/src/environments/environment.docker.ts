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
